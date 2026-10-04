using System.Net;
using System.Net.Sockets;
using ISRORCert.Database;
using ISRORCert.Logic;
using ISRORCert.Logic.Handler;
using ISRORCert.Model;
using ISRORCert.Model.Serialization;
using ISRORCert.Network;
using ISRORCert.Network.SecurityApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISRORCert.Tests;

/// <summary>
/// Runs the real AsyncServer + CertificationInterface on loopback and talks to it like a server module would,
/// using the same Security class (handshake, blowfish) on the client side.
/// </summary>
public class NetworkIntegrationTests
{
    private static ServiceProvider BuildServices(FakeDbAdapter adapter)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<IDbAdapter>(adapter);
        services.AddSingleton<ICertificationSerializer, CertificationSerializerNew>();
        services.AddSingleton<AsyncServer>();
        services.AddSingleton<IAsyncInterface, CertificationInterface>();
        services.AddSingleton<CertificationManager>();
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PacketHandlerManager>();
        services.AddSingleton<IPacketHandler, PacketHandlerSetupCord>();
        services.AddSingleton<IPacketHandler, PacketHandlerCertificate>();
        services.AddSingleton<IPacketHandler, PacketHandlerNotify>();
        services.AddSingleton<IPacketHandler, PacketHandlerRelay>();
        services.AddSingleton<IPacketHandler, PacketHandlerChangeShardData>();
        return services.BuildServiceProvider();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Pumps the client socket and the server tick until a packet with the given opcode arrives.
    /// </summary>
    private static async Task<Packet> ReceiveAsync(Socket client, Security security, AsyncServer server, ushort opcode, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            server.Tick();

            foreach (var outgoing in security.TransferOutgoing() ?? [])
                await client.SendAsync(new ArraySegment<byte>(outgoing.Key.Buffer, 0, outgoing.Key.Size), SocketFlags.None, cancellationToken);

            if (client.Available > 0)
            {
                var read = await client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                security.Recv(buffer, 0, read);
                foreach (var packet in security.TransferIncoming() ?? [])
                {
                    if (packet.Opcode == opcode)
                        return packet;
                }
            }

            await Task.Delay(5, cancellationToken);
        }
    }

    [Fact]
    public async Task ModuleCanHandshakeAndGetCertified()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var services = BuildServices(CertificationData.CreateAdapter(privateIp: "127.0.0.1"));
        var manager = services.GetRequiredService<CertificationManager>();
        Assert.True(await manager.RefreshAsync());
        services.GetServices<IPacketHandler>().ToList(); // handlers register their opcodes when created

        var server = services.GetRequiredService<AsyncServer>();
        var port = FreePort();
        server.Accept("127.0.0.1", port, 4, services.GetRequiredService<IAsyncInterface>());

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);
        var security = new Security();

        // Certificate request, as sent by an AgentServer
        var request = new Packet(0x6003);
        request.WriteString("AgentServer");
        request.WriteString("127.0.0.1");
        request.WriteUShort(15884);
        security.Send(request);

        var ack = await ReceiveAsync(client, security, server, 0xA003, cts.Token);
        Assert.Equal(1, ack.ReadByte()); // success

        var session = Assert.Single(services.GetRequiredService<SessionRegistry>().Snapshot());
        Assert.Equal((short)6, session.Body?.ID);
        Assert.Equal(1, server.ConnectionCount);

        client.Shutdown(SocketShutdown.Both);
        client.Close();
        for (var i = 0; i < 200 && services.GetRequiredService<SessionRegistry>().Snapshot().Count > 0; i++)
            await Task.Delay(10, cts.Token);
        Assert.Empty(services.GetRequiredService<SessionRegistry>().Snapshot());
    }

    [Fact]
    public async Task UnknownIp_IsRejected()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var services = BuildServices(CertificationData.CreateAdapter()); // no machine on 127.0.0.1
        var manager = services.GetRequiredService<CertificationManager>();
        Assert.True(await manager.RefreshAsync());
        services.GetServices<IPacketHandler>().ToList();

        var server = services.GetRequiredService<AsyncServer>();
        var port = FreePort();
        server.Accept("127.0.0.1", port, 4, services.GetRequiredService<IAsyncInterface>());

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);

        // The server closes the connection right away: the read returns 0 bytes.
        var read = await client.ReceiveAsync(new byte[16], SocketFlags.None, cts.Token);
        Assert.Equal(0, read);
        Assert.Equal(0, server.ConnectionCount);
        Assert.Empty(services.GetRequiredService<SessionRegistry>().Snapshot());
    }
}
