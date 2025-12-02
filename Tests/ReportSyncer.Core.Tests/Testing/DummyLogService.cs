using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetToolkit.Logging;

namespace ReportSyncer.Core.Tests.Testing
{
    /// <summary>
    /// Minimal ILogService implementation for unit tests that performs no real logging.
    /// </summary>
    public class DummyLogService : ILogService
    {
        public void LogVerbose(string message) { }
        public void LogDebug(string message) { }
        public void LogInformation(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? ex = null) { }
        public void LogCritical(string message, Exception? ex = null) { }

        public Task LogVerboseAsync(string message, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogDebugAsync(string message, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogInformationAsync(string message, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogWarningAsync(string message, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogErrorAsync(string message, Exception? ex = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogCriticalAsync(string message, Exception? ex = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
