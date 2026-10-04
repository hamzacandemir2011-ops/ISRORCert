using ISRORCert.Database;
using ISRORCert.Model;
using ISRORCert.Network;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace ISRORCert.Services
{
    internal class CertificationService : IHostedService
    {
        private readonly ILogger _logger;

        private readonly IOptions<CertificationConfig> _options;
        private readonly AsyncServer _server;
        private readonly IAsyncInterface _serverInterface;
        private readonly CertificationManager _certificationManager;
        private readonly IDbAdapter _adapter;
        private readonly IHostApplicationLifetime _lifetime;

        public CertificationService(ILogger<CertificationService> logger,
                                    IOptions<CertificationConfig> options,
                                    IAsyncInterface serverInterface,
                                    AsyncServer server,
                                    CertificationManager certificationManager,
                                    IDbAdapter adapter,
                                    IHostApplicationLifetime lifetime)
        {
            _logger = logger;
            _options = options;
            _serverInterface = serverInterface;
            _server = server;
            _certificationManager = certificationManager;
            _adapter = adapter;
            _lifetime = lifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _adapter.ConnectionString = _options.Value.DbConfig;
            if (!await _certificationManager.RefreshAsync(cancellationToken))
            {
                Fail("Certification data could not be loaded, shutting down.");
                return;
            }

            try
            {
                CreateListener();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to start the listener");
                Fail("Certification listener could not be started, shutting down.");
            }
        }

        // Without data or a listener the process would sit idle forever, so stop it with a non-zero exit code.
        private void Fail(string message)
        {
            _logger.LogCritical(message);
            Environment.ExitCode = 1;
            _lifetime.StopApplication();
        }

        private void CreateListener()
        {
            ArgumentNullException.ThrowIfNull(_certificationManager.Identity?.Machine);

            var host = _certificationManager.Identity.Machine.PublicIP;
            var port = _certificationManager.Identity.ListenerPort;

            _server.Accept(host, port, 128, _serverInterface);
            _logger.LogInformation("Listening on {host}:{port}", host, port);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
