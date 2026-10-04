using System.Net;
using ISRORCert.Logic.Handler;
using ISRORCert.Model;
using ISRORCert.Network;
using ISRORCert.Network.SecurityApi;

using Microsoft.Extensions.Logging;

namespace ISRORCert.Logic
{
    internal class CertificationInterface : IAsyncInterface
    {
        private readonly ILogger _logger;
        private readonly PacketHandlerManager _packetHandlerManager;
        private readonly IEnumerable<IPacketHandler> _packetHandlers;
        private readonly CertificationManager _certificationManager;
        private readonly SessionRegistry _sessionRegistry;
        private readonly TimeProvider _timeProvider;

        public CertificationInterface(ILogger<CertificationInterface> logger, PacketHandlerManager packetHandlerManager, IEnumerable<IPacketHandler> packetHandlers, CertificationManager certificationManager,
            SessionRegistry sessionRegistry, TimeProvider timeProvider)
        {
            _sessionRegistry = sessionRegistry;
            _timeProvider = timeProvider;
            _logger = logger;
            _packetHandlerManager = packetHandlerManager;
            _packetHandlers = packetHandlers;
            _certificationManager = certificationManager;
        }

        public bool OnConnect(AsyncContext context)
        {
            if (context?.State?.EndPoint is not IPEndPoint ipEndPoint)
                return false;

            var clientAddress = ipEndPoint.Address;
            if (!_certificationManager.ServerMachines.Any(p => clientAddress.Equals(p.PublicIPAddress) || clientAddress.Equals(p.PrivateIPAddress)))
            {
                _logger.LogCritical("Blocked a connection attempt from {clientAddress}!", clientAddress);
                return false;
            }

            context.Connected = true;
            _sessionRegistry.Add(context, ipEndPoint, _timeProvider.GetUtcNow());
            _logger.LogInformation("Connected: {guid} from {endPoint}", context.Guid, ipEndPoint);
            return true;
        }

        public bool OnReceive(AsyncContext context, byte[] buffer, int count)
        {
            try
            {
                context.Security.Recv(buffer, 0, count);
                var packets = context.Security.TransferIncoming();
                if (packets == null)
                    return true;

                foreach (Packet packet in packets)
                {
#if DEBUG
                    byte[] payload = packet.GetBytes();
                    Console.WriteLine($"Receive [{context.Guid}][{packet.Opcode:X4}][{payload.Length} bytes]{(packet.Encrypted ? "[Encrypted]" : "")}{(packet.Massive ? "[Massive]" : "")}{Environment.NewLine}{payload.HexDump()}{Environment.NewLine}");
#endif

                    if (packet.Opcode == 0x5000 || packet.Opcode == 0x9000)
                        continue;

                    if (!_packetHandlerManager.Handle(context, packet))
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receive error");
                return false;
            }

            return true;
        }

        public void OnDisconnect(AsyncContext context)
        {
            context.Connected = false;
            _sessionRegistry.Remove(context);
            _logger.LogInformation($"Disconnected: {context.Guid}");
        }

        public void OnError(AsyncContext context)
        {
            if (context == null)
                return;

            _logger.LogInformation($"Disconnected (error): {context.Guid}");
            context.Connected = false;
            _sessionRegistry.Remove(context);
        }

        public void OnTick(AsyncContext context)
        {
            if (!context.Connected)
                return;

            var buffers = context.Security.TransferOutgoing();
            if (buffers == null)
                return;

            foreach (var buffer in buffers)
            {
                var packet = buffer.Value;
#if DEBUG
                byte[] payload = packet.GetBytes();
                Console.WriteLine($"Send [{context.Guid}][{packet.Opcode:X4}][{payload.Length} bytes]{(packet.Encrypted ? "[Encrypted]" : "")}{(packet.Massive ? "[Massive]" : "")}{Environment.NewLine}{payload.HexDump()}{Environment.NewLine}");
#endif
                context.Send(buffer.Key.Buffer, 0, buffer.Key.Size);
            }
        }
    }
}
