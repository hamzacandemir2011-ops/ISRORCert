using System.Net;
using ISRORCert.Logic;
using ISRORCert.Model;
using ISRORCert.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISRORCert.Tests;

public class StatusReportTests
{
    private static async Task<CertificationManager> LoadedManager()
    {
        var manager = new CertificationManager(NullLogger<CertificationManager>.Instance, CertificationData.CreateAdapter(),
            new CertificationSerializerNew());
        Assert.True(await manager.RefreshAsync());
        return manager;
    }

    [Fact]
    public async Task Build_ListsConnectionsAndBodyStates()
    {
        var manager = await LoadedManager();
        Assert.True(manager.TryGetServerBody(6, out var agent));
        agent.State = ServerBodyState.Loading;

        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var sessions = new List<SessionRegistry.Session>
        {
            new() { Guid = Guid.NewGuid(), EndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.5"), 5000), ConnectedAt = now.AddHours(-2).AddMinutes(-5), Body = agent },
            new() { Guid = Guid.NewGuid(), EndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.6"), 5001), ConnectedAt = now.AddSeconds(-30) },
        };

        var report = StatusReport.Build(manager, sessions, now);

        Assert.Contains("Status: 2 connection(s), 6 server bodies, 0 cords", report);
        Assert.Contains("connection 10.0.0.5:5000 (ServerBody#6 - AgentServer), connected 2h 5m ago", report);
        Assert.Contains("connection 10.0.0.6:5001 (not certified yet), connected 0m 30s ago", report);
        Assert.Contains("bodies by state: Blind=4, Loading=1, ServiceRunning=1", report);
        Assert.Contains("not running: ServerBody#6 - AgentServer is 'Loading'", report);
        Assert.DoesNotContain("not running: ServerBody#1", report); // the Certification body itself is running
    }

    [Fact]
    public async Task Build_WithoutConnections()
    {
        var manager = await LoadedManager();

        var report = StatusReport.Build(manager, [], DateTimeOffset.UtcNow);

        Assert.StartsWith("Status: 0 connection(s), 6 server bodies, 0 cords", report);
    }
}
