// ============================================================================
// File: Program.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Console application entry point for ReportSyncer.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportSyncer.Console.ExitCodes;
using ReportSyncer.Console.Hosting;
using ReportSyncer.Console.Logging;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Console;

/// <summary>
/// Entry point for the ReportSyncer Console application.
/// </summary>
internal class Program
{
    /// <summary>
    /// Main entry point for the console application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code as specified in docs/60_Console Host Spec.md Section 8.</returns>
    public static async Task<int> Main(string[] args)
        => await RunAsync(args, CancellationToken.None);

    /// <summary>
    /// Internal entry point for testing and orchestration.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="ct">Cancellation token (can be used for testing; Ctrl+C sets up its own).</param>
    /// <returns>Exit code as specified in docs/60_Console Host Spec.md Section 8.</returns>
    internal static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        HostPhase phase = HostPhase.Bootstrap;
        DotNetToolkit.Logging.ILogService? log = null;
        RingBufferLogStore? ringBuffer = null;
        string? logFilePath = null;

        try
        {
            // Step 1: Load optional appsettings.json
            var appSettings = LoadAppSettings();

            // Step 2: Apply defaults to logging and UI settings
            var effectiveLogging = HostLoggingDefaults.ApplyLoggingDefaults(appSettings.Logging);
            var effectiveUi = HostLoggingDefaults.ApplyUiDefaults(appSettings.Ui);

            // Step 3: Initialize Serilog logger
            var loggerSetup = SerilogBootstrapper.Initialize(
                effectiveLogging, 
                effectiveUi, 
                DateTimeOffset.Now);
            
            ringBuffer = loggerSetup.RingBufferStore;
            logFilePath = loggerSetup.LogFilePath;

            // Step 4: Parse CLI arguments
            var (cliOk, cliOptions, cliError) = CliArgumentParser.TryParse(args);
            if (!cliOk)
            {
                System.Console.Error.WriteLine(cliError);
                loggerSetup.Logger.Error("CLI argument parsing failed: {Error}", cliError);
                return (int)ExitCodeMapper.FromUsageError(); // Exit 2: InvalidArguments
            }

            // Step 5: Resolve effective options (CLI > appsettings > defaults)
            var (resolveOk, effectiveOptions, resolveError) = RunOptionsResolver.Resolve(cliOptions!, appSettings);
            if (!resolveOk)
            {
                System.Console.Error.WriteLine(resolveError);
                loggerSetup.Logger.Error("Run options resolution failed: {Error}", resolveError);
                return (int)ExitCodeMapper.FromUsageError(); // Exit 2: InvalidArguments
            }

            // Step 6: Build RuntimeOverrides from resolved options
            RuntimeOverrides? overrides = effectiveOptions!.DryRunOverride.HasValue
                ? new RuntimeOverrides { DryRun = effectiveOptions.DryRunOverride.Value }
                : null;

            var configPath = effectiveOptions.JobConfigPath;

            // Set up cancellation
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            System.Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Build composition root (still in Bootstrap phase)
            var root = new ConsoleCompositionRoot();
            var host = await root.BuildAsync(configPath, overrides, cts.Token);
            
            // Get log service from DI container
            log = host.Services.GetRequiredService<DotNetToolkit.Logging.ILogService>();

            loggerSetup.Logger.Information("Loaded configuration from: {ConfigPath}", configPath);
            loggerSetup.Logger.Information("Found {JobCount} job(s)", host.EffectiveConfig.SyncJobs.Count);

            System.Console.WriteLine($"Loaded configuration from: {configPath}");
            System.Console.WriteLine($"Found {host.EffectiveConfig.SyncJobs.Count} job(s)");

            // Move to Execution phase (preflight is handled within RunAllJobsAsync for now)
            phase = HostPhase.Execution;

            // Run all jobs (for now - will be refined with CLI job selection)
            var results = await host.RunAllJobsAsync(cts.Token);

            // Determine exit code based on results
            var exitCode = DetermineExitCode(results, cts.Token.IsCancellationRequested);

            if (exitCode == ExitCode.Success)
            {
                loggerSetup.Logger.Information("Completed successfully (exit={ExitCode})", (int)exitCode);
                System.Console.WriteLine($"Completed successfully (exit={(int)exitCode})");
            }

            return (int)exitCode;
        }
        catch (Exception ex)
        {
            var code = ExitCodeMapper.FromException(ex, phase);
            var summary = FailureSummaryWriter.BuildSummary(code, ex.Message);

            if (log is not null)
            {
                FailureSummaryWriter.Write(log, code, summary, ex);
            }
            else
            {
                // Fallback if log service not yet available
                System.Console.Error.WriteLine($"ERROR: {summary} (exit={(int)code})");
            }

            return (int)code;
        }
    }

    /// <summary>
    /// Determines the process exit code based on job results.
    /// </summary>
    /// <remarks>
    /// This method aggregates results from all jobs and returns the worst (most severe) exit code.
    /// </remarks>
    private static ExitCode DetermineExitCode(
        IReadOnlyList<Core.Sync.Contracts.JobResult> results,
        bool wasCancelled)
    {
        if (wasCancelled)
        {
            return ExitCode.Cancelled;
        }

        var worstCode = ExitCode.Success;

        foreach (var result in results)
        {
            var jobCode = result.Status switch
            {
                Core.Sync.Contracts.JobStatus.FailedPreFlight => ExitCode.PreflightFailed,
                Core.Sync.Contracts.JobStatus.FailedExecution => ExitCode.ExecutionFailed,
                Core.Sync.Contracts.JobStatus.Succeeded => ExitCode.Success,
                Core.Sync.Contracts.JobStatus.SkippedDryRun => ExitCode.Success,
                Core.Sync.Contracts.JobStatus.Cancelled => ExitCode.Cancelled,
                _ => ExitCode.UnhandledFatal // Unknown status
            };

            worstCode = ExitCodeCombiner.CombineWorst(worstCode, jobCode);
        }

        return worstCode;
    }

    /// <summary>
    /// Loads optional appsettings.json configuration.
    /// </summary>
    /// <returns>
    /// A <see cref="HostAppSettings"/> instance with values from appsettings.json if present,
    /// or with null fields if the file doesn't exist or sections are missing.
    /// </returns>
    private static HostAppSettings LoadAppSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(HostDefaults.AppSettingsFileName, optional: true, reloadOnChange: false)
            .Build();

        // Load run settings
        var runSection = configuration.GetSection("run");
        var jobConfigPath = runSection["jobConfigPath"];
        var dryRunValue = runSection["dryRun"];

        bool? dryRun = null;
        if (!string.IsNullOrEmpty(dryRunValue) && bool.TryParse(dryRunValue, out var parsed))
        {
            dryRun = parsed;
        }

        var runSettings = new HostRunSettings(jobConfigPath, dryRun);

        // Load UI settings with defaults
        var uiSection = configuration.GetSection("ui");
        var areaCSection = uiSection.GetSection("areaC");
        var maxLinesValue = areaCSection["maxLines"];
        
        int? maxLines = null;
        if (!string.IsNullOrEmpty(maxLinesValue) && int.TryParse(maxLinesValue, out var maxLinesParsed) && maxLinesParsed > 0)
        {
            maxLines = maxLinesParsed;
        }

        var areaCSettings = maxLines.HasValue ? new HostUiAreaCSettings(maxLines) : null;
        var uiSettings = areaCSettings != null ? new HostUiSettings(areaCSettings) : null;

        // Load logging settings with defaults
        var loggingSection = configuration.GetSection("logging");
        var fileSection = loggingSection.GetSection("file");
        
        var directory = fileSection["directory"];
        var fileNamePrefix = fileSection["fileNamePrefix"];
        var retentionCountValue = fileSection["retentionCount"];
        
        int? retentionCount = null;
        if (!string.IsNullOrEmpty(retentionCountValue) && int.TryParse(retentionCountValue, out var retentionParsed) && retentionParsed > 0)
        {
            retentionCount = retentionParsed;
        }

        var fileSettings = (!string.IsNullOrEmpty(directory) || !string.IsNullOrEmpty(fileNamePrefix) || retentionCount.HasValue)
            ? new HostLoggingFileSettings(directory, fileNamePrefix, retentionCount)
            : null;

        var progressThrottleMsValue = loggingSection["progressThrottleMs"];
        int? progressThrottleMs = null;
        if (!string.IsNullOrEmpty(progressThrottleMsValue) && int.TryParse(progressThrottleMsValue, out var throttleParsed) && throttleParsed > 0)
        {
            progressThrottleMs = throttleParsed;
        }

        var loggingSettings = (fileSettings != null || progressThrottleMs.HasValue)
            ? new HostLoggingSettings(fileSettings, progressThrottleMs)
            : null;

        return new HostAppSettings(runSettings, uiSettings, loggingSettings);
    }
}
