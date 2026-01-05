// ============================================================================
// File: FallbackLogger.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-05
// Description: Simple fallback logger for bootstrap phase before Serilog is initialized.
// ============================================================================

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Simple fallback logger used during bootstrap phase.
/// </summary>
/// <remarks>
/// This logger is intentionally minimal and does not depend on any logging framework.
/// It writes to both console and a simple text file for bootstrap-phase diagnostics.
/// Once Serilog is initialized in Program.cs, this logger is no longer used.
/// </remarks>
public sealed class FallbackLogger : IDisposable
{
    private readonly StreamWriter? _fileWriter;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new fallback logger.
    /// </summary>
    /// <param name="logFilePath">Optional path to write log entries to a file. If null, creates a default bootstrap log file in the logs directory.</param>
    public FallbackLogger(string? logFilePath = null)
    {
        // Generate default path if not provided
        if (string.IsNullOrEmpty(logFilePath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            logFilePath = Path.Combine(logsDirectory, $"bootstrap-{timestamp}.log");
        }

        try
        {
            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _fileWriter = new StreamWriter(logFilePath, append: true)
            {
                AutoFlush = true
            };
        }
        catch
        {
            // Best-effort file logging; if it fails, continue with console-only
            _fileWriter = null;
        }
    }

    /// <summary>
    /// Logs an information message.
    /// </summary>
    public void Information(string message)
    {
        Log("INFO", message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void Error(string message)
    {
        Log("ERROR", message);
    }

    /// <summary>
    /// Logs an error message with exception.
    /// </summary>
    public void Error(string message, Exception ex)
    {
        Log("ERROR", $"{message}: {ex.Message}");
    }

    private void Log(string level, string message)
    {
        if (_disposed) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var formattedMessage = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            // Write to console
            System.Console.WriteLine(formattedMessage);

            // Write to file (best-effort)
            try
            {
                _fileWriter?.WriteLine(formattedMessage);
            }
            catch
            {
                // Ignore file write errors in fallback logger
            }
        }
    }

    /// <summary>
    /// Disposes the fallback logger and closes the file writer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _fileWriter?.Dispose();
            _disposed = true;
        }
    }
}
