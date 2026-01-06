// ============================================================================
// File: HostAppSettings.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Models for optional appsettings.json configuration.
// ============================================================================

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Root model for appsettings.json in the Console Host.
/// </summary>
/// <param name="Run">Optional run-level settings.</param>
/// <param name="Ui">Optional UI settings.</param>
/// <param name="Logging">Optional logging settings.</param>
/// <param name="Progress">Optional progress reporting settings.</param>
/// <remarks>
/// All fields are nullable to allow appsettings.json to be absent or incomplete.
/// Null values indicate the setting was not provided, allowing the resolver to apply defaults.
/// </remarks>
public sealed record HostAppSettings(
    HostRunSettings? Run, 
    HostUiSettings? Ui, 
    HostLoggingSettings? Logging,
    HostProgressSettings? Progress);

/// <summary>
/// Host-level run settings from appsettings.json.
/// </summary>
/// <param name="JobConfigPath">Optional path to the YAML job configuration file.</param>
/// <param name="DryRun">Optional default dry-run mode.</param>
/// <remarks>
/// <para>
/// These are *defaults* provided by the appsettings.json file. 
/// CLI arguments have higher priority and will override these values.
/// </para>
/// <para>
/// Null values indicate the setting was not provided in appsettings.json.
/// </para>
/// </remarks>
public sealed record HostRunSettings(string? JobConfigPath, bool? DryRun);

/// <summary>
/// Host-level UI settings from appsettings.json.
/// </summary>
/// <param name="Enabled">Whether UI mode is enabled. Default: true.</param>
/// <param name="AreaC">Optional Area C settings.</param>
public sealed record HostUiSettings(bool? Enabled, HostUiAreaCSettings? AreaC);

/// <summary>
/// Area C UI settings (recent log lines display).
/// </summary>
/// <param name="MaxLines">Maximum number of recent log lines to keep in memory. Must be positive. Default: 200.</param>
public sealed record HostUiAreaCSettings(int? MaxLines);

/// <summary>
/// Host-level logging settings from appsettings.json.
/// </summary>
/// <param name="File">Optional file logging settings.</param>
/// <param name="Ring">Optional ring buffer logging settings.</param>
/// <param name="Console">Optional console logging settings.</param>
/// <param name="ProgressThrottleMs">Minimum milliseconds between progress log writes. Must be positive. Default: 5000.</param>
public sealed record HostLoggingSettings(
    HostLoggingFileSettings? File,
    HostLoggingRingSettings? Ring,
    HostLoggingConsoleSettings? Console,
    int? ProgressThrottleMs);

/// <summary>
/// File logging settings.
/// </summary>
/// <param name="Enabled">Whether file logging is enabled. Default: true.</param>
/// <param name="Directory">Directory for log files. Default: "logs".</param>
/// <param name="FileNamePrefix">Prefix for log file names. Default: "reportsyncer".</param>
/// <param name="RetentionCount">Number of log files to retain. Must be positive. Default: 20.</param>
public sealed record HostLoggingFileSettings(
    bool? Enabled,
    string? Directory, 
    string? FileNamePrefix, 
    int? RetentionCount);

/// <summary>
/// Ring buffer logging settings (for UI Area C).
/// </summary>
/// <param name="Enabled">Whether ring buffer logging is enabled. Default: true.</param>
/// <param name="Capacity">Maximum number of log lines to keep in ring buffer. Must be positive. Default: 2000.</param>
/// <param name="MinLevel">Minimum log level for ring buffer. Default: "Information".</param>
public sealed record HostLoggingRingSettings(
    bool? Enabled,
    int? Capacity,
    string? MinLevel);

/// <summary>
/// Console logging settings.
/// </summary>
/// <param name="Enabled">Whether console logging is enabled. Default: true (disabled when UI is enabled).</param>
/// <param name="MinLevel">Minimum log level for console. Default: "Information".</param>
public sealed record HostLoggingConsoleSettings(
    bool? Enabled,
    string? MinLevel);

/// <summary>
/// Progress reporting settings.
/// </summary>
/// <param name="FileLogIntervalMs">Minimum milliseconds between InProgress events in file logs. Must be positive. Default: 5000.</param>
/// <remarks>
/// This setting controls throttling of InProgress events in the text log file.
/// Terminal events (Started/Completed/Failed/Skipped) are never throttled.
/// </remarks>
public sealed record HostProgressSettings(
    int? FileLogIntervalMs);
