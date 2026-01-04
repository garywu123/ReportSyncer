// ============================================================================
// File: RunOptionsResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Resolves effective run options with CLI > appsettings > defaults priority.
// ============================================================================

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// The final effective run options after merging all configuration sources.
/// </summary>
/// <param name="JobConfigPath">The resolved path to the job configuration YAML file (never null).</param>
/// <param name="DryRunOverride">
/// The resolved dry-run override, if any. 
/// Null means no override was provided (Core should use its YAML or internal default).
/// </param>
/// <remarks>
/// This is the output of the configuration resolution process and represents
/// the final values that will be used to initialize the Core orchestrator.
/// </remarks>
public sealed record EffectiveRunOptions(string JobConfigPath, bool? DryRunOverride);

/// <summary>
/// Resolves CLI and appsettings into a single effective configuration.
/// </summary>
/// <remarks>
/// <para>
/// Resolution priority (highest to lowest):
/// <list type="number">
///   <item>CLI arguments</item>
///   <item>appsettings.json values</item>
///   <item>Host defaults</item>
/// </list>
/// </para>
/// <para>
/// This resolver does NOT validate file existence or load YAML content.
/// It only performs priority-based merging of configuration sources.
/// </para>
/// </remarks>
public static class RunOptionsResolver
{
    /// <summary>
    /// Resolves effective run options from CLI and appsettings sources.
    /// </summary>
    /// <param name="cli">The parsed CLI arguments.</param>
    /// <param name="appSettings">The loaded appsettings.json model.</param>
    /// <returns>
    /// A tuple indicating success or failure:
    /// <list type="bullet">
    ///   <item><c>Ok</c>: true if resolution succeeded, false otherwise.</item>
    ///   <item><c>Options</c>: the resolved options if successful, otherwise null.</item>
    ///   <item><c>Error</c>: an error message if resolution failed, otherwise null.</item>
    /// </list>
    /// </returns>
    /// <example>
    /// <code><![CDATA[
    /// var (ok, options, error) = RunOptionsResolver.Resolve(cliOptions, appSettings);
    /// if (!ok)
    /// {
    ///     Console.Error.WriteLine(error);
    ///     return 1;
    /// }
    /// // Use options...
    /// ]]></code>
    /// </example>
    public static (bool Ok, EffectiveRunOptions? Options, string? Error) Resolve(
        CliRunOptions cli,
        HostAppSettings appSettings)
    {
        // Resolve JobConfigPath: CLI > appsettings > defaults
        string jobConfigPath;
        if (!string.IsNullOrWhiteSpace(cli.ConfigPath))
        {
            jobConfigPath = cli.ConfigPath;
        }
        else if (!string.IsNullOrWhiteSpace(appSettings.Run?.JobConfigPath))
        {
            jobConfigPath = appSettings.Run.JobConfigPath;
        }
        else
        {
            jobConfigPath = HostDefaults.DefaultJobConfigPath;
        }

        // Resolve DryRunOverride: CLI > appsettings > null (no override)
        bool? dryRunOverride;
        if (cli.DryRun.HasValue)
        {
            dryRunOverride = cli.DryRun.Value;
        }
        else if (appSettings.Run?.DryRun.HasValue == true)
        {
            dryRunOverride = appSettings.Run.DryRun.Value;
        }
        else
        {
            dryRunOverride = null;
        }

        return (true, new EffectiveRunOptions(jobConfigPath, dryRunOverride), null);
    }
}
