using ISRORCert.Database;
using ISRORCert.Model;
using ISRORCert.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISRORCert.Tests;

public class CertificationManagerTests
{
    private static CertificationManager CreateManager(FakeDbAdapter adapter) =>
        new(NullLogger<CertificationManager>.Instance, adapter, new CertificationSerializerNew());

    [Fact]
    public async Task Refresh_LoadsAndLinksTopology()
    {
        var manager = CreateManager(CertificationData.CreateAdapter());

        Assert.True(await manager.RefreshAsync());
        Assert.NotNull(manager.Identity);
        Assert.Equal(1, manager.Identity!.ID);
        Assert.Equal(ServerBodyState.ServiceRunning, manager.Identity.State);

        Assert.Equal(2, manager.Divisions[0].ManagerBodyID);
        Assert.Equal(3, manager.ServerMachines[0].ManagerBodyID);
        Assert.Equal(4, manager.Farms[0].ManagerBodyID);
        Assert.Equal(5, manager.Shards[0].ManageBodyID);
    }

    [Fact]
    public async Task Refresh_CanRunTwiceWithoutDuplicates()
    {
        var manager = CreateManager(CertificationData.CreateAdapter());

        Assert.True(await manager.RefreshAsync());
        Assert.True(await manager.RefreshAsync());
        Assert.Equal(6, manager.ServerBodies.Count);
    }

    [Fact]
    public async Task Refresh_Failure_KeepsPreviousData()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        Assert.True(await manager.RefreshAsync());

        adapter.FailingProcedures.Add("_GetShardList");
        Assert.False(await manager.RefreshAsync());
        Assert.Single(manager.Shards);
        Assert.Equal(6, manager.ServerBodies.Count);
    }

    [Fact]
    public async Task Refresh_MissingGlobalManagerModule_DoesNotCrash()
    {
        var manager = CreateManager(CertificationData.CreateAdapter(includeGlobalManagerModule: false));

        Assert.True(await manager.RefreshAsync());
        Assert.Equal(0, manager.Divisions[0].ManagerBodyID);
    }

    [Theory]
    [InlineData(CertificationData.PublicIp)]
    [InlineData(CertificationData.PrivateIp)]
    public async Task TryGetCertifiableServerBody_FindsBodyByAddressAndPort(string address)
    {
        var manager = CreateManager(CertificationData.CreateAdapter());
        await manager.RefreshAsync();

        Assert.True(manager.TryGetCertifiableServerBody("AgentServer", address, 15884, out var body));
        Assert.Equal(6, body.ID);
    }

    [Fact]
    public async Task TryGetCertifiableServerBody_RejectsWrongAddressPortOrModule()
    {
        var manager = CreateManager(CertificationData.CreateAdapter());
        await manager.RefreshAsync();

        Assert.False(manager.TryGetCertifiableServerBody("AgentServer", "1.2.3.4", 15884, out _));
        Assert.False(manager.TryGetCertifiableServerBody("AgentServer", CertificationData.PublicIp, 1, out _));
        Assert.False(manager.TryGetCertifiableServerBody("UnknownModule", CertificationData.PublicIp, 0, out _));
        Assert.False(manager.TryGetCertifiableServerBody("Certification", CertificationData.PublicIp, 0, out _));
    }

    [Fact]
    public async Task TryGetCertifiableServerBody_UnknownMachine_ReturnsFalseInsteadOfThrowing()
    {
        var manager = CreateManager(CertificationData.CreateAdapter(agentMachineId: 999));
        await manager.RefreshAsync();

        Assert.False(manager.TryGetCertifiableServerBody("AgentServer", CertificationData.PublicIp, 15884, out _));
    }

    [Fact]
    public async Task UpdateShard_SavesAndUpdatesMemory()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        await manager.RefreshAsync();
        Assert.True(manager.TryGetShard(64, out var shard));

        Assert.True(await manager.UpdateShardNameAsync(shard, "NewName"));
        Assert.True(await manager.UpdateShardMaxUserAsync(shard, 2000));
        Assert.Equal("NewName", shard.Name);
        Assert.Equal(2000, shard.MaxUser);
        Assert.Equal(new[] { "_UpdateShardName", "_UpdateShardMaxUser" }, adapter.ExecutedProcedures);
    }

    [Fact]
    public async Task UpdateShard_DbFailure_KeepsOldValues()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        await manager.RefreshAsync();
        Assert.True(manager.TryGetShard(64, out var shard));

        adapter.FailingProcedures.Add("_UpdateShardName");
        adapter.FailingProcedures.Add("_UpdateShardMaxUser");

        Assert.False(await manager.UpdateShardNameAsync(shard, "NewName"));
        Assert.False(await manager.UpdateShardMaxUserAsync(shard, 2000));
        Assert.Equal("Shard", shard.Name);
        Assert.Equal(1000, shard.MaxUser);
    }

    [Fact]
    public async Task UpdateShard_SameValue_IsRejectedWithoutDbCall()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        await manager.RefreshAsync();
        Assert.True(manager.TryGetShard(64, out var shard));

        Assert.False(await manager.UpdateShardNameAsync(shard, "Shard"));
        Assert.False(await manager.UpdateShardMaxUserAsync(shard, 1000));
        Assert.Empty(adapter.ExecutedProcedures);
    }

    [Fact]
    public async Task UpdateShard_ConcurrentSameValue_OnlyOneWins()
    {
        var adapter = CertificationData.CreateAdapter();
        var manager = CreateManager(adapter);
        await manager.RefreshAsync();
        Assert.True(manager.TryGetShard(64, out var shard));

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => manager.UpdateShardNameAsync(shard, "NewName"))));

        Assert.Single(results, true);
        Assert.Single(adapter.ExecutedProcedures);
        Assert.Equal("NewName", shard.Name);
    }
}
