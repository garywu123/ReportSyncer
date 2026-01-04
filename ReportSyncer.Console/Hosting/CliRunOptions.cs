// ============================================================================
// File: CliRunOptions.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Parsed CLI argument model for run options.
// ============================================================================

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Represents the parsed CLI arguments for a run invocation.
/// </summary>
/// <param name="ConfigPath">The path to the job config YAML file, if provided via --config.</param>
/// <param name="DryRun">The dry-run override, if provided via --dry-run.</param>
/// <remarks>
/// <para>
/// Null values indicate the argument was not provided on the command line.
/// This allows the resolver to fall back to appsettings.json or defaults.
/// </para>
/// <para>
/// This type is the direct output of <see cref="CliArgumentParser"/> and represents
/// only what was explicitly provided by the user via CLI arguments.
/// </para>
/// </remarks>
public sealed record CliRunOptions(string? ConfigPath, bool? DryRun);
