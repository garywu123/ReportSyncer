// ============================================================================
// File: SerilogBootstrapper.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Initializes Serilog logger with Console, File, and RingBuffer sinks.
// ============================================================================

using ReportSyncer.Console.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Bootstraps Serilog logger with Console, JSONL file, and RingBuffer sinks.
/// </summary>
public static class SerilogBootstrapper
{
    /// <summary>
    /// Result of logger initialization.
    /// </summary>
    /// <param name="Logger">The configured Serilog logger.</param>
    /// <param name="LogFilePath">Path to the JSONL log file.</param>
    /// <param name="RingBufferStore">The ring buffer store for UI Area C.</param>
    public sealed record LoggerSetup(
        ILogger Logger,
        string LogFilePath,
        RingBufferLogStore RingBufferStore);

    /// <summary>
    /// Initializes the Serilog logger with all configured sinks.
    /// </summary>
    /// <param name="loggingSettings">Logging configuration settings.</param>
    /// <param name="uiSettings">UI configuration settings.</param>
    /// <param name="now">Current timestamp for log file naming.</param>
    /// <returns>A <see cref="LoggerSetup"/> containing the logger, log file path, and ring buffer store.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// - Performs best-effort log retention cleanup
    /// - Creates a new JSONL log file for this process run
    /// - Configures Console sink (human-readable)
    /// - Configures File sink (JSONL, async)
    /// - Configures RingBuffer sink (for UI Area C)
    /// - Enriches logs with default structured fields (JobId, Table, Kind, Phase)
    /// </para>
    /// </remarks>
    public static LoggerSetup Initialize(
        HostLoggingSettings loggingSettings,
        HostUiSettings uiSettings,
        DateTimeOffset now)
    {
        if (loggingSettings == null)
        {
            throw new ArgumentNullException(nameof(loggingSettings));
        }

        if (uiSettings == null)
        {
            throw new ArgumentNullException(nameof(uiSettings));
        }

        // Step 1: Best-effort log retention cleanup
        var directory = loggingSettings.File!.Directory!;
        var prefix = loggingSettings.File.FileNamePrefix!;
        var retentionCount = loggingSettings.File.RetentionCount!.Value;

        var (cleanupSuccess, deletedCount, cleanupError) = LogRetentionCleaner.BestEffortCleanup(
            directory, prefix, retentionCount);

        if (!cleanupSuccess)
        {
            // Log to stderr as logger not yet initialized
            System.Console.Error.WriteLine($"Warning: Log retention cleanup failed: {cleanupError}");
        }

        // Step 2: Generate log file path
        var logFilePath = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);

        // Ensure directory exists
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Step 3: Create RingBufferLogStore
        var maxLines = uiSettings.AreaC!.MaxLines!.Value;
        var ringBufferStore = new RingBufferLogStore(maxLines);

        // Step 4: Configure Serilog
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("JobId", "")
            .Enrich.WithProperty("Table", "")
            .Enrich.WithProperty("Kind", "")
            .Enrich.WithProperty("Phase", "Bootstrap");

        // Console sink (human-readable)
        loggerConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        // File sink (JSONL, async)
        loggerConfig.WriteTo.Async(a => a.File(
            new CompactJsonFormatter(),
            logFilePath,
            restrictedToMinimumLevel: LogEventLevel.Debug,
            rollingInterval: RollingInterval.Infinite, // One file per process run
            rollOnFileSizeLimit: false,
            shared: false,
            flushToDiskInterval: TimeSpan.FromSeconds(1)));

        // RingBuffer sink (human-readable for UI)
        var ringBufferFormatter = new MessageTemplateTextFormatter(
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}", null);
        var ringBufferSink = new RingBufferSink(ringBufferStore, ringBufferFormatter);
        loggerConfig.WriteTo.Sink(ringBufferSink);

        var logger = loggerConfig.CreateLogger();

        // Log initialization info
        logger.Information("Log file initialized at {LogFilePath}", logFilePath);
        if (cleanupSuccess && deletedCount > 0)
        {
            logger.Information("Cleaned up {DeletedCount} old log file(s)", deletedCount);
        }

        return new LoggerSetup(logger, logFilePath, ringBufferStore);
    }
}
