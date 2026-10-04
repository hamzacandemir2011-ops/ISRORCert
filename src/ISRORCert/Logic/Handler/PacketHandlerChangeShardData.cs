using ISRORCert.Model;
using ISRORCert.Network;
using ISRORCert.Network.SecurityApi;

using Microsoft.Extensions.Logging;

using System;
using System.Linq;

namespace ISRORCert.Logic.Handler
{
    internal class PacketHandlerChangeShardData : IPacketHandler
    {
        private readonly CertificationManager _certificationManager;
        private readonly ILogger _logger;

        public PacketHandlerChangeShardData(ILogger<PacketHandlerChangeShardData> logger, PacketHandlerManager packetHandlerManager, CertificationManager certificationManager)
        {
            _logger = logger;
            _certificationManager = certificationManager;
            packetHandlerManager[0x6310] = OnChangeShardDataReq;
        }

        private bool OnChangeShardDataReq(AsyncContext context, Packet packet, int relayID)
        {
            // The packet is read here, on the network thread; only the DB update runs in the background
            // so a slow database doesn't block the connection. The ack is sent once the update is done.
            var shardID = packet.ReadShort();

            if (!_certificationManager.TryGetShard(shardID, out var shard))
                return RouteChangeShardDataFailed(context, relayID);

            var shardDataType = (ShardDataType)packet.ReadByte();
            if (shardDataType == ShardDataType.Name)
            {
                var newName = packet.ReadString();
                RunInBackground(context, relayID, async () =>
                {
                    if (!await _certificationManager.UpdateShardNameAsync(shard, newName).ConfigureAwait(false))
                        return RouteChangeShardDataFailed(context, relayID);

                    var ack = new Packet(0xA310);
                    ack.WriteByte(1); // result
                    ack.WriteShort(shardID);
                    ack.WriteByte((byte)shardDataType);
                    ack.WriteString(newName);
                    return RelayRouter.RouteRelayAck(context, ack, relayID);
                });
                return true;
            }
            else if (shardDataType == ShardDataType.MaxUser)
            {
                var newMaxUser = packet.ReadShort();
                RunInBackground(context, relayID, async () =>
                {
                    if (!await _certificationManager.UpdateShardMaxUserAsync(shard, newMaxUser).ConfigureAwait(false))
                        return RouteChangeShardDataFailed(context, relayID);

                    var ack = new Packet(0xA310);
                    ack.WriteByte(1); // result
                    ack.WriteShort(shardID);
                    ack.WriteByte((byte)shardDataType);
                    ack.WriteShort(newMaxUser);
                    return RelayRouter.RouteRelayAck(context, ack, relayID);
                });
                return true;
            }
            return RouteChangeShardDataFailed(context, relayID);
        }

        private void RunInBackground(AsyncContext context, int relayID, Func<Task<bool>> work)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await work().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Change shard data failed for {Guid}", context.Guid);
                    if (context.Connected)
                        RouteChangeShardDataFailed(context, relayID);
                }
            });
        }

        private static bool RouteChangeShardDataFailed(AsyncContext context, int relayID)
        {
            var ack = new Packet(0xA310);
            ack.WriteByte(2); // result
            return RelayRouter.RouteRelayAck(context, ack, relayID);
        }
    }
}
