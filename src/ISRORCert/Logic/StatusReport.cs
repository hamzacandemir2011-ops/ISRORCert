using System.Text;

using ISRORCert.Model;

namespace ISRORCert.Logic
{
    /// <summary>
    /// Builds a human readable summary of the connected modules and the state of every server body / cord.
    /// </summary>
    internal static class StatusReport
    {
        public static string Build(CertificationManager manager, IReadOnlyList<SessionRegistry.Session> sessions, DateTimeOffset now)
        {
            var report = new StringBuilder();
            report.AppendLine($"Status: {sessions.Count} connection(s), {manager.ServerBodies.Count} server bodies, {manager.ServerCords.Count} cords");

            foreach (var session in sessions)
            {
                var uptime = now - session.ConnectedAt;
                var body = session.Body is null ? "not certified yet" : $"{nameof(ServerBody)}#{session.Body}";
                report.AppendLine($"  connection {session.EndPoint?.ToString() ?? "?"} ({body}), connected {FormatDuration(uptime)}");
            }

            var byState = manager.ServerBodies
                .GroupBy(p => p.State)
                .OrderBy(p => p.Key)
                .Select(p => $"{p.Key}={p.Count()}");
            report.AppendLine($"  bodies by state: {string.Join(", ", byState)}");

            foreach (var body in manager.ServerBodies.Where(p => p.State != ServerBodyState.ServiceRunning).OrderBy(p => p.ID))
                report.AppendLine($"  not running: {nameof(ServerBody)}#{body} is '{body.State}'");

            if (manager.ServerCords.Count > 0)
            {
                var cordsByState = manager.ServerCords
                    .GroupBy(p => p.State)
                    .OrderBy(p => p.Key)
                    .Select(p => $"{p.Key}={p.Count()}");
                report.AppendLine($"  cords by state: {string.Join(", ", cordsByState)}");
            }

            return report.ToString().TrimEnd();
        }

        private static string FormatDuration(TimeSpan duration) =>
            duration.TotalDays >= 1
                ? $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m ago"
                : duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}h {duration.Minutes}m ago"
                    : $"{(int)duration.TotalMinutes}m {duration.Seconds}s ago";
    }
}
