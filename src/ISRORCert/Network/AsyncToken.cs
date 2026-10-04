using System.Net.Sockets;

namespace ISRORCert.Network
{
    public class AsyncToken
    {
        public required Socket Socket { get; init; }
        public required IAsyncInterface Interface { get; init; }
    }
}
