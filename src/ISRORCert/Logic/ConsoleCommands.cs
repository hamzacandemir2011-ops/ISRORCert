using System.Text;

using ISRORCert.Model;

using Microsoft.Extensions.Logging;

namespace ISRORCert.Logic
{
    /// <summary>
    /// Commands an operator can type in the Certification console. Returns the text to print.
    /// </summary>
    internal class ConsoleCommands
    {
        private readonly ILogger _logger;
        private readonly CertificationManager _certificationManager;
        private readonly SessionRegistry _sessionRegistry;
        private readonly TimeProvider _timeProvider;

        public const string Help =
            "Commands:\n" +
            "  help                 show this help\n" +
            "  status               show the status summary now\n" +
            "  sessions             list the connected modules\n" +
            "  reload               reload the topology from the database (connections stay open)\n" +
            "  kick <ip|id|body>    disconnect connections by IP, session id prefix or server body ID";

        public ConsoleCommands(ILogger<ConsoleCommands> logger, CertificationManager certificationManager,
            SessionRegistry sessionRegistry, TimeProvider timeProvider)
        {
            _logger = logger;
            _certificationManager = certificationManager;
            _sessionRegistry = sessionRegistry;
            _timeProvider = timeProvider;
        }

        public async Task<string> ExecuteAsync(string? line, CancellationToken cancellationToken = default)
        {
            var parts = (line ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return "";

            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                case "?":
                    return Help;

                case "status":
                    if (_certificationManager.Identity is null)
                        return "Certification data is not loaded.";
                    return StatusReport.Build(_certificationManager, _sessionRegistry.Snapshot(), _timeProvider.GetUtcNow());

                case "sessions":
                    return Sessions();

                case "reload":
                    _logger.LogInformation("Reload requested from the console");
                    return await _certificationManager.RefreshAsync(cancellationToken).ConfigureAwait(false)
                        ? $"Reloaded: {_certificationManager.ServerBodies.Count} server bodies, {_certificationManager.ServerCords.Count} cords."
                        : "Reload failed, the current data is kept. See the log for details.";

                case "kick":
                    return parts.Length < 2 ? "Usage: kick <ip|session id prefix|server body ID>" : Kick(parts[1]);

                default:
                    return $"Unknown command '{parts[0]}'. Type 'help' for the list of commands.";
            }
        }

        private string Sessions()
        {
            var sessions = _sessionRegistry.Snapshot();
            if (sessions.Count == 0)
                return "No connections.";

            var now = _timeProvider.GetUtcNow();
            var output = new StringBuilder();
            output.AppendLine($"{sessions.Count} connection(s):");
            foreach (var session in sessions)
            {
                var body = session.Body is null ? "not certified" : $"ServerBody#{session.Body} ({session.Body.State})";
                var seconds = (long)(now - session.ConnectedAt).TotalSeconds;
                output.AppendLine($"  {ShortId(session.Guid)}  {session.EndPoint?.ToString() ?? "?",-21}  {body}, {seconds}s");
            }
            return output.ToString().TrimEnd();
        }

        private string Kick(string target)
        {
            var sessions = _sessionRegistry.Snapshot()
                .Where(session => Matches(session, target))
                .ToList();

            if (sessions.Count == 0)
                return $"No connection matches '{target}'.";

            foreach (var session in sessions)
            {
                _logger.LogWarning("Kicking {Guid} ({EndPoint}) from the console", session.Guid, session.EndPoint);
                session.Context?.Disconnect();
            }

            return $"Disconnected {sessions.Count} connection(s).";
        }

        private static bool Matches(SessionRegistry.Session session, string target)
        {
            if (session.EndPoint is not null &&
                (session.EndPoint.Address.ToString() == target || session.EndPoint.ToString() == target))
                return true;

            if (short.TryParse(target, out var bodyId) && session.Body?.ID == bodyId)
                return true;

            // Session id prefix (at least 4 characters, so a short number isn't taken as an id)
            return target.Length >= 4 && session.Guid.ToString("N").StartsWith(target, StringComparison.OrdinalIgnoreCase);
        }

        private static string ShortId(Guid guid) => guid.ToString("N")[..8];
    }
}
