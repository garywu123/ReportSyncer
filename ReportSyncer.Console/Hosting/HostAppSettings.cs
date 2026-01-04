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
/// <remarks>
/// All fields are nullable to allow appsettings.json to be absent or incomplete.
/// Null values indicate the setting was not provided, allowing the resolver to apply defaults.
/// </remarks>
public sealed record HostAppSettings(HostRunSettings? Run);

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
