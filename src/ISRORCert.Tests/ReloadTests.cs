using ISRORCert.Logic;
using ISRORCert.Model;
using ISRORCert.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISRORCert.Tests;

public class ReloadTests
{
    private static CertificationManager CreateManager(FakeDbAdapter adapter) =>
        new(NullLogger<CertificationManager>.Instance, adapter, new CertificationSerializerNew());

    private static void AddBody(FakeDbAdapter adapter, short id, short port) =>
        adapter.Tables["_GetServerBodyList"].Rows.Add(id, DBNull.Value, DBNull.Value, (short)64, 1, (byte)6, (byte)0, (short)1, port);

    [Fact]
    public async Task Reload_AddsNewBodies_AndKeepsRuntimeStates()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        Assert.True(await manager.RefreshAsync());
        Assert.True(manager.TryGetServerBody(6, out var agent));
        agent.State = ServerBodyState.Loading;

        var reloads = 0;
        manager.Reloaded += (_, _) => reloads++;
        AddBody(adapter, 7, 15885);

        Assert.True(await manager.RefreshAsync());

        Assert.Equal(1, reloads);
        Assert.Equal(7, manager.ServerBodies.Count);
        Assert.True(manager.TryGetServerBody(6, out var reloadedAgent));
        Assert.NotSame(agent, reloadedAgent);
        Assert.Equal(ServerBodyState.Loading, reloadedAgent.State);
        Assert.True(manager.TryGetServerBody(7, out var added));
        Assert.Equal(ServerBodyState.Blind, added.State);
        Assert.Equal(ServerBodyState.ServiceRunning, manager.Identity!.State);
        Assert.True(manager.TryGetCertifiableServerBody("AgentServer", CertificationData.PublicIp, 15885, out var certifiable));
        Assert.Equal(7, certifiable.ID);
    }

    [Fact]
    public async Task FirstLoad_DoesNotRaiseReloaded()
    {
        var manager = CreateManager(CertificationData.CreateAdapter());
        var reloads = 0;
        manager.Reloaded += (_, _) => reloads++;

        Assert.True(await manager.RefreshAsync());

        Assert.Equal(0, reloads);
    }

    [Fact]
    public async Task Reload_InvalidData_KeepsCurrentSnapshot()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        Assert.True(await manager.RefreshAsync());
        var before = manager.Current;

        // Without the Certification module the new data is unusable.
        var modules = adapter.Tables["_GetModuleList"];
        modules.Rows.RemoveAt(0);
        Assert.False(await manager.RefreshAsync());
        Assert.Same(before, manager.Current);

        // Duplicate IDs are rejected too.
        modules.Rows.InsertAt(modules.NewRow(), 0);
        modules.Rows[0].ItemArray = [(byte)1, "Certification"];
        AddBody(adapter, 6, 1);
        Assert.False(await manager.RefreshAsync());
        Assert.Same(before, manager.Current);
    }

    [Fact]
    public async Task Snapshot_TakenBeforeReload_StaysConsistent()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        Assert.True(await manager.RefreshAsync());
        var snapshot = manager.Current;

        AddBody(adapter, 7, 15885);
        Assert.True(await manager.RefreshAsync());

        Assert.Equal(6, snapshot.ServerBodies.Count);
        Assert.Equal(7, manager.Current.ServerBodies.Count);
    }

    [Fact]
    public async Task Reload_RemapsSessionBodies()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        Assert.True(await manager.RefreshAsync());
        var registry = new SessionRegistry(manager);

        var agentContext = TestSessions.CreateContext();
        registry.Add(agentContext, TestSessions.EndPoint("10.0.0.5"), DateTimeOffset.UtcNow);
        Assert.True(manager.TryGetServerBody(6, out var agent));
        registry.SetCertifiedBody(agentContext, agent);

        var globalContext = TestSessions.CreateContext();
        registry.Add(globalContext, TestSessions.EndPoint("10.0.0.5", 5001), DateTimeOffset.UtcNow);
        Assert.True(manager.TryGetServerBody(2, out var global));
        registry.SetCertifiedBody(globalContext, global);

        // Body 2 (GlobalManager) is removed from the database, body 6 stays.
        var bodies = adapter.Tables["_GetServerBodyList"];
        bodies.Rows.RemoveAt(1);
        Assert.True(await manager.RefreshAsync());

        Assert.True(registry.TryGet(agentContext.Guid, out var agentSession));
        Assert.True(manager.TryGetServerBody(6, out var reloadedAgent));
        Assert.Same(reloadedAgent, agentSession.Body);

        Assert.True(registry.TryGet(globalContext.Guid, out var globalSession));
        Assert.Null(globalSession.Body);
    }
}
