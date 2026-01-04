// ============================================================================
// File: HostDefaults.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Host-level default constants for the Console application.
// ============================================================================

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Default values and constants for Console Host configuration.
/// </summary>
/// <remarks>
/// These constants define the standard file names and fallback paths used by the Console Host.
/// They are used when neither CLI arguments nor appsettings.json provide an explicit value.
/// </remarks>
public static class HostDefaults
{
    /// <summary>
    /// The fixed name of the optional appsettings file.
    /// </summary>
    public const string AppSettingsFileName = "appsettings.json";

    /// <summary>
    /// The default job configuration file path when no other source specifies one.
    /// </summary>
    public const string DefaultJobConfigPath = "./job.yaml";
}
