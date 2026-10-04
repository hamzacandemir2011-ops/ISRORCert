using ISRORCert.Logic;
using ISRORCert.Model;
using ISRORCert.Model.Serialization;
using ISRORCert.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ISRORCert.Tests;

public class ConsoleCommandsTests
{
    private static (ConsoleCommands Commands, CertificationManager Manager, SessionRegistry Registry, FakeDbAdapter Adapter) Create()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = new CertificationManager(NullLogger<CertificationManager>.Instance, adapter, new CertificationSerializerNew());
        var registry = new SessionRegistry(manager);
        var commands = new ConsoleCommands(NullLogger<ConsoleCommands>.Instance, manager, registry, TimeProvider.System);
        return (commands, manager, registry, adapter);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("HELP")]
    [InlineData("?")]
    public async Task Help_ListsCommands(string command)
    {
        var (commands, _, _, _) = Create();
        Assert.Equal(ConsoleCommands.Help, await commands.ExecuteAsync(command));
    }

    [Fact]
    public async Task EmptyAndUnknownCommands()
    {
        var (commands, _, _, _) = Create();
        Assert.Equal("", await commands.ExecuteAsync("   "));
        Assert.Equal("", await commands.ExecuteAsync(null));
        Assert.Contains("Unknown command 'foo'", await commands.ExecuteAsync("foo bar"));
    }

    [Fact]
    public async Task Status_BeforeAndAfterLoad()
    {
        var (commands, manager, _, _) = Create();
        Assert.Equal("Certification data is not loaded.", await commands.ExecuteAsync("status"));

        Assert.True(await manager.RefreshAsync());
        Assert.StartsWith("Status: 0 connection(s), 6 server bodies", await commands.ExecuteAsync("status"));
    }

    [Fact]
    public async Task Sessions_ListsConnections()
    {
        var (commands, manager, registry, _) = Create();
        Assert.True(await manager.RefreshAsync());
        Assert.Equal("No connections.", await commands.ExecuteAsync("sessions"));

        var context = TestSessions.CreateContext();
        registry.Add(context, TestSessions.EndPoint("10.0.0.5", 5000), DateTimeOffset.UtcNow);
        Assert.True(manager.TryGetServerBody(6, out var agent));
        registry.SetCertifiedBody(context, agent);

        var output = await commands.ExecuteAsync("sessions");
        Assert.Contains("1 connection(s):", output);
        Assert.Contains(context.Guid.ToString("N")[..8], output);
        Assert.Contains("10.0.0.5:5000", output);
        Assert.Contains("ServerBody#6 - AgentServer (Blind)", output);
    }

    [Fact]
    public async Task Reload_ReportsSuccessAndFailure()
    {
        var (commands, manager, _, adapter) = Create();
        Assert.True(await manager.RefreshAsync());

        Assert.Equal("Reloaded: 6 server bodies, 0 cords.", await commands.ExecuteAsync("reload"));

        adapter.FailingProcedures.Add("_GetShardList");
        Assert.StartsWith("Reload failed", await commands.ExecuteAsync("reload"));
    }

    [Fact]
    public async Task Kick_WithoutMatch()
    {
        var (commands, manager, registry, _) = Create();
        Assert.True(await manager.RefreshAsync());
        registry.Add(TestSessions.CreateContext(), TestSessions.EndPoint("10.0.0.5"), DateTimeOffset.UtcNow);

        Assert.StartsWith("Usage: kick", await commands.ExecuteAsync("kick"));
        Assert.Equal("No connection matches '1.2.3.4'.", await commands.ExecuteAsync("kick 1.2.3.4"));
        Assert.Equal("No connection matches '6'.", await commands.ExecuteAsync("kick 6")); // not certified as body 6
    }

    [Fact]
    public async Task Kick_MatchesByIpBodyAndIdPrefix()
    {
        var (commands, manager, registry, _) = Create();
        Assert.True(await manager.RefreshAsync());
        var context = TestSessions.CreateContext();
        registry.Add(context, TestSessions.EndPoint("10.0.0.5"), DateTimeOffset.UtcNow);
        Assert.True(manager.TryGetServerBody(6, out var agent));
        registry.SetCertifiedBody(context, agent);

        // The socket isn't connected, so Disconnect is a no-op; this checks the matching.
        Assert.Equal("Disconnected 1 connection(s).", await commands.ExecuteAsync("kick 10.0.0.5"));
        Assert.Equal("Disconnected 1 connection(s).", await commands.ExecuteAsync("kick 6"));
        Assert.Equal("Disconnected 1 connection(s).", await commands.ExecuteAsync($"kick {context.Guid.ToString("N")[..6]}"));
    }

    [Fact]
    public async Task Service_ReadsCommandsUntilInputEnds()
    {
        var (commands, manager, _, _) = Create();
        Assert.True(await manager.RefreshAsync());
        var input = new StringReader("help\n\nstatus\n");
        var output = new StringWriter();
        var service = new ConsoleCommandService(NullLogger<ConsoleCommandService>.Instance,
            Options.Create(new CertificationConfig()), commands, input, output);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        var text = output.ToString();
        Assert.Contains(ConsoleCommands.Help, text.ReplaceLineEndings("\n"));
        Assert.Contains("Status: 0 connection(s), 6 server bodies", text);
    }

    [Fact]
    public async Task Service_Disabled_DoesNotReadInput()
    {
        var (commands, _, _, _) = Create();
        var output = new StringWriter();
        var service = new ConsoleCommandService(NullLogger<ConsoleCommandService>.Instance,
            Options.Create(new CertificationConfig { ConsoleCommands = false }), commands, new StringReader("help\n"), output);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("", output.ToString());
    }
}
