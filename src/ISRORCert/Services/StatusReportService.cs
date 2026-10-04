using ISRORCert.Logic;
using ISRORCert.Model;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISRORCert.Services
{
    /// <summary>
    /// Periodically logs <see cref="StatusReport"/>. Disabled when CertificationConfig:StatusIntervalSeconds is 0.
    /// </summary>
    internal class StatusReportService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly CertificationManager _certificationManager;
        private readonly SessionRegistry _sessionRegistry;
        private readonly TimeProvider _timeProvider;
        private readonly int _intervalSeconds;

        public StatusReportService(ILogger<StatusReportService> logger, IOptions<CertificationConfig> options,
            CertificationManager certificationManager, SessionRegistry sessionRegistry, TimeProvider timeProvider)
        {
            _logger = logger;
            _certificationManager = certificationManager;
            _sessionRegistry = sessionRegistry;
            _timeProvider = timeProvider;
            _intervalSeconds = options.Value.StatusIntervalSeconds;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_intervalSeconds <= 0)
                return;

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds), _timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (_certificationManager.Identity is null)
                    continue; // not loaded yet

                _logger.LogInformation(StatusReport.Build(_certificationManager, _sessionRegistry.Snapshot(), _timeProvider.GetUtcNow()));
            }
        }
    }
}
