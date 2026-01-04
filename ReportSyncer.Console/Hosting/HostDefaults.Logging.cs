// ============================================================================
// File: HostDefaults.Logging.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Default values for logging and UI settings.
// ============================================================================

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Default values for host-level logging and UI configuration.
/// </summary>
public static class HostLoggingDefaults
{
    /// <summary>
    /// Default directory for log files.
    /// </summary>
    public const string LogDirectory = "logs";

    /// <summary>
    /// Default prefix for log file names.
    /// </summary>
    public const string LogFileNamePrefix = "reportsyncer";

    /// <summary>
    /// Default number of log files to retain.
    /// </summary>
    public const int LogRetentionCount = 20;

    /// <summary>
    /// Default minimum milliseconds between progress log writes.
    /// </summary>
    public const int ProgressThrottleMs = 5000;

    /// <summary>
    /// Default maximum number of recent log lines to keep in memory for UI Area C.
    /// </summary>
    public const int UiAreaCMaxLines = 200;

    /// <summary>
    /// Applies defaults to HostLoggingSettings.
    /// </summary>
    /// <param name="settings">The settings to apply defaults to (can be null).</param>
    /// <returns>A non-null HostLoggingSettings with defaults applied.</returns>
    public static HostLoggingSettings ApplyLoggingDefaults(HostLoggingSettings? settings)
    {
        var file = settings?.File;
        var effectiveFile = new HostLoggingFileSettings(
            Directory: file?.Directory ?? LogDirectory,
            FileNamePrefix: file?.FileNamePrefix ?? LogFileNamePrefix,
            RetentionCount: file?.RetentionCount is > 0 ? file.RetentionCount : LogRetentionCount
        );

        var effectiveProgressThrottleMs = settings?.ProgressThrottleMs is > 0 
            ? settings.ProgressThrottleMs.Value 
            : ProgressThrottleMs;

        return new HostLoggingSettings(effectiveFile, effectiveProgressThrottleMs);
    }

    /// <summary>
    /// Applies defaults to HostUiSettings.
    /// </summary>
    /// <param name="settings">The settings to apply defaults to (can be null).</param>
    /// <returns>A non-null HostUiSettings with defaults applied.</returns>
    public static HostUiSettings ApplyUiDefaults(HostUiSettings? settings)
    {
        var maxLines = settings?.AreaC?.MaxLines is > 0 
            ? settings.AreaC.MaxLines.Value 
            : UiAreaCMaxLines;

        return new HostUiSettings(new HostUiAreaCSettings(maxLines));
    }
}
