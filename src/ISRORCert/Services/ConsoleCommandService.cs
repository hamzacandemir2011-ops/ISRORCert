using ISRORCert.Logic;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISRORCert.Services
{
    /// <summary>
    /// Reads operator commands from the console (see <see cref="ConsoleCommands"/>).
    /// Stops quietly when there is no console input (e.g. running as a service).
    /// </summary>
    internal class ConsoleCommandService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly ConsoleCommands _commands;
        private readonly bool _enabled;
        private readonly TextReader _input;
        private readonly TextWriter _output;

        public ConsoleCommandService(ILogger<ConsoleCommandService> logger, IOptions<CertificationConfig> options, ConsoleCommands commands)
            : this(logger, options, commands, Console.In, Console.Out)
        {
        }

        internal ConsoleCommandService(ILogger<ConsoleCommandService> logger, IOptions<CertificationConfig> options, ConsoleCommands commands,
            TextReader input, TextWriter output)
        {
            _logger = logger;
            _commands = commands;
            _enabled = options.Value.ConsoleCommands;
            _input = input;
            _output = output;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
                return;

            await Task.Yield(); // don't block host startup

            while (!stoppingToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    // Console reads can't always be cancelled, so stop waiting for them on shutdown.
                    var read = _input.ReadLineAsync(stoppingToken).AsTask();
                    await Task.WhenAny(read, Task.Delay(Timeout.Infinite, stoppingToken)).ConfigureAwait(false);
                    if (stoppingToken.IsCancellationRequested)
                        return;
                    line = await read.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (line is null)
                    return; // no console input

                try
                {
                    var result = await _commands.ExecuteAsync(line, stoppingToken).ConfigureAwait(false);
                    if (result.Length > 0)
                        await _output.WriteLineAsync(result).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Console command '{Command}' failed", line);
                }
            }
        }
    }
}
