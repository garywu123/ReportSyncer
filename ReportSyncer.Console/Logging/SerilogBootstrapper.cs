// ============================================================================
// File: SerilogBootstrapper.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-05
// Description: Initializes Serilog logger with unified pipeline: Ring, File, and optional Console sinks.
// ============================================================================

using ReportSyncer.Console.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Bootstraps Serilog logger with Ring, File, and optional Console sinks.
/// </summary>
/// <remarks>
/// Creates a single unified logging pipeline where all logs flow through Serilog.
/// This is the ONLY place where Serilog logger should be created.
/// </remarks>
public static class SerilogBootstrapper
{
    /// <summary>
    /// Result of logger initialization.
    /// </summary>
    /// <param name="Logger">The configured Serilog logger.</param>
    /// <param name="LogFilePath">Path to the JSONL log file (if file logging enabled).</param>
    /// <param name="RingBufferStore">The ring buffer store for UI Area C (if ring logging enabled).</param>
    public sealed record LoggerSetup(
        ILogger Logger,
        string? LogFilePath,
        RingBufferLogStore? RingBufferStore);

    /// <summary>
    /// Initializes the Serilog logger with all configured sinks.
    /// </summary>
    /// <param name="loggingSettings">Logging configuration settings.</param>
    /// <param name="uiSettings">UI configuration settings.</param>
    /// <param name="now">Current timestamp for log file naming.</param>
    /// <returns>A <see cref="LoggerSetup"/> containing the logger, log file path, and ring buffer store.</returns>
    /// <remarks>
    /// <para>
    /// This method creates a single Serilog pipeline with:
    /// - RingBuffer sink (for UI Area C) — always safe, never throws
    /// - File sink (JSONL, async) — if enabled
    /// - Console sink (human-readable) — if enabled (default off when UI is enabled)
    /// </para>
    /// <para>
    /// The RingBuffer sink is bounded. On overflow, oldest entries are dropped (FIFO).
    /// File sink provides the complete audit trail.
    /// Console sink should be disabled during UI mode to avoid polluting TUI output.
    /// </para>
    /// </remarks>
    public static LoggerSetup Initialize(
        HostLoggingSettings loggingSettings,
        HostUiSettings uiSettings,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(loggingSettings);
        ArgumentNullException.ThrowIfNull(uiSettings);

        // Step 1: Create logger configuration
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("JobId", "")
            .Enrich.WithProperty("Table", "")
            .Enrich.WithProperty("Kind", "")
            .Enrich.WithProperty("Phase", "Bootstrap");

        // Step 2: Ring buffer sink (always enabled by default, safe)
        RingBufferLogStore? ringBufferStore = null;
        if (loggingSettings.Ring!.Enabled!.Value)
        {
            var capacity = loggingSettings.Ring.Capacity!.Value;
            var minLevel = ParseLogLevel(loggingSettings.Ring.MinLevel!);
            
            ringBufferStore = new RingBufferLogStore(capacity);
            var ringBufferFormatter = new MessageTemplateTextFormatter(
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}", null);
            var ringBufferSink = new RingBufferSink(ringBufferStore, ringBufferFormatter);
            
            loggerConfig = loggerConfig.WriteTo.Sink(
                ringBufferSink,
                restrictedToMinimumLevel: minLevel);
        }

        // Step 3: File sink (JSONL, async)
        string? logFilePath = null;
        if (loggingSettings.File!.Enabled!.Value)
        {
            var directory = loggingSettings.File.Directory!;
            var prefix = loggingSettings.File.FileNamePrefix!;
            var retentionCount = loggingSettings.File.RetentionCount!.Value;

            // Best-effort log retention cleanup
            var (cleanupSuccess, deletedCount, cleanupError) = LogRetentionCleaner.BestEffortCleanup(
                directory, prefix, retentionCount);

            if (!cleanupSuccess)
            {
                // Log to stderr as logger not yet initialized
                System.Console.Error.WriteLine($"Warning: Log retention cleanup failed: {cleanupError}");
            }

            // Generate log file path
            logFilePath = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);

            // Ensure directory exists
            var logDirectory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            loggerConfig = loggerConfig.WriteTo.Async(a => a.File(
                new RenderedCompactJsonFormatter(),
                logFilePath,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                rollingInterval: RollingInterval.Infinite, // One file per process run
                rollOnFileSizeLimit: false,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(1)));
        }

        // Step 4: Console sink (optional, default off when UI enabled)
        if (loggingSettings.Console!.Enabled!.Value)
        {
            var minLevel = ParseLogLevel(loggingSettings.Console.MinLevel!);
            loggerConfig = loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: minLevel);
        }

        // Step 5: Create the logger and set it as the global logger
        var logger = loggerConfig.CreateLogger();
        Log.Logger = logger;

        // Log initialization info
        logger.Information("Serilog pipeline initialized");
        if (logFilePath != null)
        {
            logger.Information("Log file: {LogFilePath}", logFilePath);
        }
        if (ringBufferStore != null)
        {
            logger.Information("Ring buffer capacity: {Capacity}", loggingSettings.Ring.Capacity);
        }

        return new LoggerSetup(logger, logFilePath, ringBufferStore);
    }

    /// <summary>
    /// Parses log level string to Serilog LogEventLevel.
    /// </summary>
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
