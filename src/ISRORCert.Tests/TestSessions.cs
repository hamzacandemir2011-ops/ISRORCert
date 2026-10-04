using System.Net;
using System.Net.Sockets;
using ISRORCert.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISRORCert.Tests;

/// <summary>
/// Builds AsyncContexts backed by an unconnected socket, for tests that need sessions without a real connection.
/// </summary>
internal static class TestSessions
{
    private sealed class NoopInterface : IAsyncInterface
    {
        public bool OnConnect(AsyncContext context) => true;
        public bool OnReceive(AsyncContext context, byte[] buffer, int count) => true;
        public void OnDisconnect(AsyncContext context) { }
        public void OnError(AsyncContext context) { }
        public void OnTick(AsyncContext context) { }
    }

    public static AsyncContext CreateContext()
    {
        var server = new AsyncServer(NullLogger<AsyncServer>.Instance);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        return new AsyncState(server, socket, AsyncOperation.Accept, new NoopInterface()).Context;
    }

    public static IPEndPoint EndPoint(string ip, int port = 5000) => new(IPAddress.Parse(ip), port);
}
