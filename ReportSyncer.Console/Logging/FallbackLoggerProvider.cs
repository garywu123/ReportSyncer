// ============================================================================
// File: FallbackLoggerProvider.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-05
// Description: Bridges Microsoft.Extensions.Logging to FallbackLogger for bootstrap phase.
// ============================================================================

using Microsoft.Extensions.Logging;

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Logging provider that bridges Microsoft.Extensions.Logging to FallbackLogger.
/// </summary>
/// <remarks>
/// This provider is used only during bootstrap phase before Serilog is initialized.
/// It allows Core services (like ConfigurationProvider) to use ILogger while
/// the actual logging is handled by FallbackLogger.
/// </remarks>
public sealed class FallbackLoggerProvider : ILoggerProvider
{
    private readonly FallbackLogger _fallbackLogger;

    public FallbackLoggerProvider(FallbackLogger fallbackLogger)
    {
        _fallbackLogger = fallbackLogger ?? throw new ArgumentNullException(nameof(fallbackLogger));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FallbackLoggerAdapter(_fallbackLogger, categoryName);
    }

    public void Dispose()
    {
        // FallbackLogger is owned by the caller, don't dispose it here
    }

    /// <summary>
    /// Adapter that implements Microsoft.Extensions.Logging.ILogger using FallbackLogger.
    /// </summary>
    private sealed class FallbackLoggerAdapter : ILogger
    {
        private readonly FallbackLogger _fallbackLogger;
        private readonly string _categoryName;

        public FallbackLoggerAdapter(FallbackLogger fallbackLogger, string categoryName)
        {
            _fallbackLogger = fallbackLogger;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            // No-op for fallback logger
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Only log Information and above during bootstrap
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var formattedMessage = $"[{_categoryName}] {message}";

            if (logLevel >= LogLevel.Error)
            {
                if (exception != null)
                {
                    _fallbackLogger.Error(formattedMessage, exception);
                }
                else
                {
                    _fallbackLogger.Error(formattedMessage);
                }
            }
            else
            {
                _fallbackLogger.Information(formattedMessage);
            }
        }
    }
}
