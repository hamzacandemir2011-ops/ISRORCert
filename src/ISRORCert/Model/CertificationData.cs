using Microsoft.Extensions.Logging;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;

namespace ISRORCert.Model
{
    /// <summary>
    /// One consistent, linked copy of everything loaded from the certification database.
    /// A reload builds a new instance and swaps it in at once, so readers never see half-updated data.
    /// The runtime state of bodies/cords (State) and shard name/max user still change in place.
    /// </summary>
    internal class CertificationData
    {
        private readonly ILogger _logger;

        private readonly List<Content> _contentList;
        private readonly List<Module> _moduleList;
        private readonly List<Division> _divisionList;
        private readonly List<Farm> _farmList;
        private readonly List<FarmContent> _farmContentList;
        private readonly List<Shard> _shardList;
        private readonly List<ServerMachine> _serverMachineList;
        private readonly List<ServerBody> _serverBodyList;
        private readonly List<ServerCord> _serverCordList;

        private readonly Dictionary<string, Module> _moduleByName;

        private readonly Dictionary<byte, Content> _contentByID;
        private readonly Dictionary<byte, Module> _moduleByID;
        private readonly Dictionary<byte, Division> _divisonByID;
        private readonly Dictionary<byte, Farm> _farmByID;
        private readonly Dictionary<short, Shard> _shardByID;
        private readonly Dictionary<int, ServerMachine> _serverMachineByID;
        private readonly Dictionary<short, ServerBody> _serverBodyByID;
        private readonly Dictionary<int, ServerCord> _serverCordyByID;

        public IReadOnlyList<Content> Content => _contentList;
        public IReadOnlyList<Module> Modules => _moduleList;
        public IReadOnlyList<Division> Divisions => _divisionList;
        public IReadOnlyList<Farm> Farms => _farmList;
        public IReadOnlyList<FarmContent> FarmContent => _farmContentList;
        public IReadOnlyList<Shard> Shards => _shardList;
        public IReadOnlyList<ServerMachine> ServerMachines => _serverMachineList;
        public IReadOnlyList<ServerBody> ServerBodies => _serverBodyList;
        public IReadOnlyList<ServerCord> ServerCords => _serverCordList;

        /// <summary>
        /// The Certification server body (this server). Null only for <see cref="Empty"/>.
        /// </summary>
        public ServerBody? Identity { get; }

        public static CertificationData Empty { get; } = new();

        private CertificationData()
        {
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            _contentList = new(); _moduleList = new(); _divisionList = new(); _farmList = new(); _farmContentList = new();
            _shardList = new(); _serverMachineList = new(); _serverBodyList = new(); _serverCordList = new();
            _moduleByName = new(); _contentByID = new(); _moduleByID = new(); _divisonByID = new(); _farmByID = new();
            _shardByID = new(); _serverMachineByID = new(); _serverBodyByID = new(); _serverCordyByID = new();
        }

        private CertificationData(ILogger logger, List<Content> contentList, List<Module> moduleList, List<Division> divisionList,
            List<Farm> farmList, List<FarmContent> farmContentList, List<Shard> shardList, List<ServerMachine> serverMachineList,
            List<ServerBody> serverBodyList, List<ServerCord> serverCordList)
        {
            _logger = logger;
            _contentList = contentList;
            _moduleList = moduleList;
            _divisionList = divisionList;
            _farmList = farmList;
            _farmContentList = farmContentList;
            _shardList = shardList;
            _serverMachineList = serverMachineList;
            _serverBodyList = serverBodyList;
            _serverCordList = serverCordList;

            _contentByID = _contentList.ToDictionary(p => p.Id);
            _moduleByID = _moduleList.ToDictionary(p => p.Id);
            _moduleByName = _moduleList.ToDictionary(p => p.Name);
            _divisonByID = _divisionList.ToDictionary(p => p.Id);
            _farmByID = _farmList.ToDictionary(p => p.Id);
            _shardByID = _shardList.ToDictionary(p => p.ID);
            _serverMachineByID = _serverMachineList.ToDictionary(p => p.Id);
            _serverBodyByID = _serverBodyList.ToDictionary(p => p.ID);
            _serverCordyByID = _serverCordList.ToDictionary(p => p.ID);

            Link();

            if (_moduleByName.TryGetValue("Certification", out var certificationModule))
                Identity = _serverBodyList.SingleOrDefault(p => p.ModuleID == certificationModule.Id);
        }

        /// <summary>
        /// Builds and links a snapshot. Returns null (and logs why) if it has no Certification module/body.
        /// </summary>
        public static CertificationData? Create(ILogger logger, List<Content> contentList, List<Module> moduleList, List<Division> divisionList,
            List<Farm> farmList, List<FarmContent> farmContentList, List<Shard> shardList, List<ServerMachine> serverMachineList,
            List<ServerBody> serverBodyList, List<ServerCord> serverCordList)
        {
            var data = new CertificationData(logger, contentList, moduleList, divisionList, farmList, farmContentList, shardList,
                serverMachineList, serverBodyList, serverCordList);

            if (!data._moduleByName.ContainsKey("Certification"))
            {
                logger.LogCritical("Failed to find Certification module");
                return null;
            }

            if (data.Identity is null)
            {
                logger.LogCritical("Failed to find Certification serverBody");
                return null;
            }

            return data;
        }

        private void Link()
        {
            foreach (var item in _serverBodyList)
            {
                // Division
                Division? division = null;
                if (item.DivisionID.HasValue && !_divisonByID.TryGetValue(item.DivisionID ?? 0, out division))
                    _logger.LogError($"Cannot find {nameof(Division)}#{item.DivisionID} for {nameof(ServerBody)}#{item.ID}");

                item.Division = division;

                // Farm
                Farm? farm = null;
                if (item.FarmID.HasValue && !_farmByID.TryGetValue(item.FarmID.Value, out farm))
                    _logger.LogError($"Cannot find {nameof(Farm)}#{item.FarmID} for {nameof(ServerBody)}#{item.ID}");

                item.Farm = farm;

                // Shard
                Shard? shard = null;
                if (item.ShardID.HasValue && !_shardByID.TryGetValue(item.ShardID.Value, out shard))
                    _logger.LogError($"Cannot find {nameof(Shard)}#{item.ShardID} for {nameof(ServerBody)}#{item.ID}");

                item.Shard = shard;

                // Shard
                ServerBody? certifier = null;
                if (item.CertifierID.HasValue && !_serverBodyByID.TryGetValue(item.CertifierID.Value, out certifier))
                    _logger.LogError($"Cannot find Certifier#{item.CertifierID} for {nameof(ServerBody)}#{item.ID}");

                item.Certifier = certifier;

                // ServerMachine
                if (!_serverMachineByID.TryGetValue(item.MachineID, out var machine))
                    _logger.LogError($"Cannot find {nameof(ServerMachine)}#{item.MachineID} for {nameof(ServerBody)}#{item.ID}");

                item.Machine = machine;

                // Module
                if (!_moduleByID.TryGetValue(item.ModuleID, out var module))
                    _logger.LogError($"Cannot find {nameof(Module)}#{item.ModuleID} for {nameof(ServerBody)}#{item.ID}");

                item.Module = module;
            }

            foreach (var item in _serverCordList)
            {
                // Outlet
                if (!_serverBodyByID.TryGetValue(item.OutletID, out var outlet))
                    _logger.LogError($"Cannot find Outlet#{item.OutletID} for {nameof(ServerCord)}#{item.ID}");

                item.Outlet = outlet;

                // Inlet
                if (!_serverBodyByID.TryGetValue(item.InletID, out var inlet))
                    _logger.LogError($"Cannot find Inlet#{item.InletID} for {nameof(ServerCord)}#{item.ID}");

                item.Inlet = inlet;
            }

            foreach (var item in _farmList)
            {
                // Division
                Division? division = null;
                if (!_divisonByID.TryGetValue(item.DivisionID, out division))
                    _logger.LogError($"Cannot find {nameof(Division)}#{item.DivisionID} for {nameof(Farm)}#{item.Id}");

                item.Division = division;
            }

            foreach (var item in _farmContentList)
            {
                // Farm
                if (!_farmByID.TryGetValue(item.FarmID, out var farm))
                    _logger.LogError($"Cannot find {nameof(Farm)}#{item.FarmID} for {nameof(FarmContent)}#{item.ID}");

                item.Farm = farm;

                // Content
                if (!_contentByID.TryGetValue(item.ContentID, out var content))
                    _logger.LogError($"Cannot find {nameof(Content)}#{item.ContentID} for {nameof(FarmContent)}#{item.ID}");

                item.Content = content;
            }

            foreach (var item in _shardList)
            {
                // Farm
                if (!_farmByID.TryGetValue(item.FarmID, out var farm))
                    _logger.LogError($"Cannot find {nameof(Farm)}#{item.FarmID} for {nameof(Shard)}#{item.ID}");

                item.Farm = farm;

                // Content
                if (!_contentByID.TryGetValue(item.ContentID, out var content))
                    _logger.LogError($"Cannot find {nameof(Content)}#{item.ContentID} for {nameof(Shard)}#{item.ID}");

                item.Content = content;
            }

            foreach (var item in _serverMachineList)
            {
                // Division
                Division? division = null;
                if (item.DivisionID.HasValue && !_divisonByID.TryGetValue(item.DivisionID ?? 0, out division))
                    _logger.LogError($"Cannot find {nameof(Division)}#{item.DivisionID} for {nameof(ServerMachine)}#{item.Id}");

                item.Division = division;

                if (!IPAddress.TryParse(item.PublicIP, out var publicAddress))
                {
                    if(_logger.IsEnabled(LogLevel.Error))
                        _logger.LogError($"Failed to parse {nameof(item.PublicIP)} for {nameof(ServerMachine)}#{item.Id}.");
                }
                item.PublicIPAddress = publicAddress;

                if (!IPAddress.TryParse(item.PrivateIP, out var privateAddress))
                {
                    if(_logger.IsEnabled(LogLevel.Error))
                        _logger.LogError($"Failed to parse {nameof(item.PrivateIP)} for {nameof(ServerMachine)}#{item.Id}.");
                }
                item.PrivateIPAddress = privateAddress;
            }

            if (!TryGetModule("GlobalManager", out var globalModule))
                _logger.LogCritical("Cannot find GlobalManager module");

            if (!TryGetModule("MachineManager", out var machineModule))
                _logger.LogCritical("Cannot find MachineManager module");

            if (!TryGetModule("FarmManager", out var farmModule))
                _logger.LogCritical("Cannot find FarmManager module");

            if (!TryGetModule("SR_ShardManager", out var shardModule))
                _logger.LogCritical("Cannot find ShardManager module");

            foreach (var item in _serverBodyList)
            {
                if (item.Module is null)
                    continue;

                if (item.Module == globalModule)
                {
                    if (item.Division is null)
                        _logger.LogError($"GlobalManager {nameof(ServerBody)}#{item.ID} has no {nameof(Division)}");
                    else
                        item.Division.ManagerBodyID = item.ID;
                }

                if (item.Module == machineModule)
                {
                    if (item.Machine is null)
                        _logger.LogError($"MachineManager {nameof(ServerBody)}#{item.ID} has no {nameof(ServerMachine)}");
                    else
                        item.Machine.ManagerBodyID = item.ID;
                }

                if (item.Module == farmModule)
                {
                    if (item.Farm is null)
                        _logger.LogError($"FarmManager {nameof(ServerBody)}#{item.ID} has no {nameof(Farm)}");
                    else
                        item.Farm.ManagerBodyID = item.ID;
                }

                if (item.Module == shardModule)
                {
                    if (item.Shard is null)
                        _logger.LogError($"ShardManager {nameof(ServerBody)}#{item.ID} has no {nameof(Shard)}");
                    else
                        item.Shard.ManageBodyID = item.ID;
                }
            }
        }
        public bool TryGetCertifiableServerBody(string moduleName, string moduleAddress, ushort modulePort, [MaybeNullWhen(false)] out ServerBody serverBody)
        {
            serverBody = null;

            if (Identity is null)
                return false;

            if (!TryGetModule(moduleName, out var module))
                return false;

            foreach (var item in _serverBodyList)
            {
                if (item == Identity)
                    continue; // don't certify ourself

                if (item.CertifierID != Identity.ID)
                    continue; // we're not the certifier

                if (item.ModuleID != module.Id)
                    continue; // wrong module

                var machine = item.Machine;
                if (machine is null)
                    continue; // unknown machine, already reported while linking

                if (moduleAddress != machine.PublicIP && moduleAddress != machine.PrivateIP)
                    continue;

                if (modulePort != 0 && modulePort != item.ListenerPort)
                    continue;

                serverBody = item;
                return true;
            }
            return false;
        }

        public bool TryGetModule(string moduleName, [MaybeNullWhen(false)] out Module module) => _moduleByName.TryGetValue(moduleName, out module);
        public bool TryGetServerBody(short id, [MaybeNullWhen(false)] out ServerBody serverBody) => _serverBodyByID.TryGetValue(id, out serverBody);
        public bool TryGetServerCord(int id, [MaybeNullWhen(false)] out ServerCord serverCord) => _serverCordyByID.TryGetValue(id, out serverCord);
        public bool TryGetShard(short id, [MaybeNullWhen(false)] out Shard shard) => _shardByID.TryGetValue(id, out shard);
    }
}
