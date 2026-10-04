using ISRORCert.Database;
using ISRORCert.Model.Serialization;

using Microsoft.Extensions.Logging;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ISRORCert.Model
{
    internal class CertificationManager
    {
        private readonly ILogger _logger;
        private readonly IDbAdapter _adapter;

        private volatile CertificationData _data = CertificationData.Empty;

        // Only one refresh at a time.
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        /// <summary>
        /// The current snapshot. Take it once and use that instance when several reads must be consistent
        /// (e.g. while serializing a certificate), because a reload can swap it at any time.
        /// </summary>
        public CertificationData Current => _data;

        public IReadOnlyList<Content> Content => _data.Content;
        public IReadOnlyList<Module> Modules => _data.Modules;
        public IReadOnlyList<Division> Divisions => _data.Divisions;
        public IReadOnlyList<Farm> Farms => _data.Farms;
        public IReadOnlyList<FarmContent> FarmContent => _data.FarmContent;
        public IReadOnlyList<Shard> Shards => _data.Shards;
        public IReadOnlyList<ServerMachine> ServerMachines => _data.ServerMachines;
        public IReadOnlyList<ServerBody> ServerBodies => _data.ServerBodies;
        public IReadOnlyList<ServerCord> ServerCords => _data.ServerCords;

        public ServerBody? Identity => _data.Identity;

        /// <summary>
        /// Raised after a successful refresh that replaced existing data (not on the first load), with the old and new snapshot.
        /// </summary>
        public event Action<CertificationData, CertificationData>? Reloaded;

        public CertificationManager(ILogger<CertificationManager> logger, IDbAdapter adapter, ICertificationSerializer certificationSerializer)
        {
            _logger = logger;
            _adapter = adapter;
        }

        /// <summary>
        /// Loads the certification data from the database. Can be called again while running (reload):
        /// the new data replaces the old one atomically, keeping the runtime state of the bodies and cords
        /// that still exist. If anything fails, the current data is kept.
        /// </summary>
        public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task<bool> RefreshCoreAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Querying certification data...");

            var contentList = new List<Content>();
            var moduleList = new List<Module>();
            var divisionList = new List<Division>();
            var farmList = new List<Farm>();
            var farmContentList = new List<FarmContent>();
            var shardList = new List<Shard>();
            var serverMachineList = new List<ServerMachine>();
            var serverBodyList = new List<ServerBody>();
            var serverCordList = new List<ServerCord>();

            var results = await Task.WhenAll(
                _adapter.GetDataTableAsync(contentList, "_GetContentList", cancellationToken),
                _adapter.GetDataTableAsync(moduleList, "_GetModuleList", cancellationToken),
                _adapter.GetDataTableAsync(divisionList, "_GetDivisionList", cancellationToken),
                _adapter.GetDataTableAsync(farmList, "_GetFarmList", cancellationToken),
                _adapter.GetDataTableAsync(farmContentList, "_GetFarmContentList", cancellationToken),
                _adapter.GetDataTableAsync(shardList, "_GetShardList", cancellationToken),
                _adapter.GetDataTableAsync(serverMachineList, "_GetServerMachineList", cancellationToken),
                _adapter.GetDataTableAsync(serverBodyList, "_GetServerBodyList", cancellationToken),
                _adapter.GetDataTableAsync(serverCordList, "_GetServerCordList", cancellationToken));

            if(results.Any(p => p == false))
            {
                _logger.LogCritical("Failed to query certification data...");
                return false;
            }

            CertificationData? data;
            try
            {
                data = CertificationData.Create(_logger, contentList, moduleList, divisionList, farmList, farmContentList,
                    shardList, serverMachineList, serverBodyList, serverCordList);
            }
            catch (ArgumentException ex) // duplicate IDs in the database
            {
                _logger.LogCritical(ex, "Invalid certification data");
                return false;
            }

            if (data is null)
                return false;

            var previous = _data;
            var isReload = previous.Identity is not null;
            if (isReload)
                CarryOverRuntimeState(previous, data);

            data.Identity!.State = ServerBodyState.ServiceRunning;
            _data = data;

            if (isReload)
            {
                LogChanges(previous, data);
                _logger.LogInformation("Certification data reloaded");
                Reloaded?.Invoke(previous, data);
            }
            else
            {
                _logger.LogInformation("Certification successfully refreshed");
                Print(data, 0, data.Identity);
            }
            return true;
        }

        /// <summary>
        /// States are reported by the modules at runtime and aren't stored in the database, so keep them for
        /// the bodies and cords that still exist after a reload.
        /// </summary>
        private static void CarryOverRuntimeState(CertificationData previous, CertificationData next)
        {
            foreach (var body in next.ServerBodies)
            {
                if (previous.TryGetServerBody(body.ID, out var old))
                    body.State = old.State;
            }

            foreach (var cord in next.ServerCords)
            {
                if (previous.TryGetServerCord(cord.ID, out var old))
                {
                    cord.State = old.State;
                    cord.SessionId = old.SessionId;
                }
            }
        }

        private void LogChanges(CertificationData previous, CertificationData next)
        {
            var oldBodies = previous.ServerBodies.Select(p => p.ID).ToHashSet();
            var newBodies = next.ServerBodies.Select(p => p.ID).ToHashSet();
            foreach (var body in next.ServerBodies.Where(p => !oldBodies.Contains(p.ID)))
                _logger.LogInformation($"Reload: added {nameof(ServerBody)}#{body}");
            foreach (var body in previous.ServerBodies.Where(p => !newBodies.Contains(p.ID)))
                _logger.LogWarning($"Reload: removed {nameof(ServerBody)}#{body}");

            var oldIdentity = previous.Identity!;
            var newIdentity = next.Identity!;
            if (oldIdentity.ID != newIdentity.ID || oldIdentity.ListenerPort != newIdentity.ListenerPort ||
                oldIdentity.Machine?.PublicIP != newIdentity.Machine?.PublicIP)
            {
                _logger.LogWarning("Reload: the Certification server's own address/port changed in the database. " +
                                   "The listener keeps using the old one until you restart.");
            }
        }

        private static void Print(CertificationData data, int indent, ServerBody body)
        {
            var prefix = new string(' ', indent);
            Console.WriteLine($"{prefix}{body}");
            foreach (var item in data.ServerBodies.Where(p => p.CertifierID == body.ID))
                Print(data, indent + 3, item);
        }

        public bool TryGetCertifiableServerBody(string moduleName, string moduleAddress, ushort modulePort, [MaybeNullWhen(false)] out ServerBody serverBody) =>
            _data.TryGetCertifiableServerBody(moduleName, moduleAddress, modulePort, out serverBody);

        public bool TryGetModule(string moduleName, [MaybeNullWhen(false)] out Module module) => _data.TryGetModule(moduleName, out module);
        public bool TryGetServerBody(short id, [MaybeNullWhen(false)] out ServerBody serverBody) => _data.TryGetServerBody(id, out serverBody);
        public bool TryGetServerCord(int id, [MaybeNullWhen(false)] out ServerCord serverCord) => _data.TryGetServerCord(id, out serverCord);
        public bool TryGetShard(short id, [MaybeNullWhen(false)] out Shard shard) => _data.TryGetShard(id, out shard);

        // Serializes shard updates, so two concurrent requests can't interleave the DB write and the in-memory change.
        private readonly SemaphoreSlim _shardUpdateLock = new(1, 1);

        public async Task<bool> UpdateShardNameAsync(Shard shard, string newName, CancellationToken cancellationToken = default)
        {
            await _shardUpdateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (shard.Name == newName)
                    return false;

                if (!await _adapter.TryExecuteAsync("_UpdateShardName", cancellationToken,
                        _adapter.GetInputParameter("@nID", shard.ID),
                        _adapter.GetInputParameter("@szName", newName)).ConfigureAwait(false))
                {
                    _logger.LogError($"Failed to save {nameof(Shard)}#{shard} name: {shard.Name} -> {newName}");
                    return false;
                }

                _logger.LogInformation($"Changed {nameof(Shard)}#{shard} name: {shard.Name} -> {newName}");
                shard.Name = newName;
                return true;
            }
            finally
            {
                _shardUpdateLock.Release();
            }
        }

        public async Task<bool> UpdateShardMaxUserAsync(Shard shard, short newMaxUser, CancellationToken cancellationToken = default)
        {
            await _shardUpdateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (shard.MaxUser == newMaxUser)
                    return false;

                if (!await _adapter.TryExecuteAsync("_UpdateShardMaxUser", cancellationToken,
                        _adapter.GetInputParameter("@nID", shard.ID),
                        _adapter.GetInputParameter("@nMaxUser", newMaxUser)).ConfigureAwait(false))
                {
                    _logger.LogError($"Failed to save {nameof(Shard)}#{shard} max user: {shard.MaxUser} -> {newMaxUser}");
                    return false;
                }

                _logger.LogInformation($"Changed {nameof(Shard)}#{shard} max user: {shard.MaxUser} -> {newMaxUser}");
                shard.MaxUser = newMaxUser;
                return true;
            }
            finally
            {
                _shardUpdateLock.Release();
            }
        }

    }
}
