// ============================================================================
// File: PreFlightValidator.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25
// Description: Implements pre-flight validation orchestration for sync jobs.
// ============================================================================

using DotNetToolkit.Logging;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Coordinates schema analysis and translates failures into domain exceptions.
/// </summary>
public sealed class PreFlightValidator : IPreFlightValidator
{
    private readonly ISchemaService _schemaService;
    private readonly ILogService _log;

    public PreFlightValidator(ISchemaService schemaService, ILogService log)
    {
        _schemaService = schemaService ?? throw new ArgumentNullException(nameof(schemaService));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PreFlightResult> ValidateAsync(
        SyncConfiguration effectiveConfig,
        SyncJobConfig job,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(effectiveConfig);
        ArgumentNullException.ThrowIfNull(job);

        ct.ThrowIfCancellationRequested();

        var analysisResult = await _schemaService.AnalyzeJobAsync(effectiveConfig, job, ct)
            .ConfigureAwait(false);

        if (analysisResult.IsSuccess)
        {
            var result = new PreFlightResult(job.Name, effectiveConfig.Run.DryRun, analysisResult.Value);
            _log.LogInformation($"Preflight passed for job {job.Name}.");
            return result;
        }

        var reason = analysisResult.Error ?? "Schema analysis failed.";
        var message = $"Job='{job.Name}' preflight failed. Reason: {reason}";
        _log.LogError($"Preflight failed for job {job.Name}: {reason}");
        throw new SchemaMismatchException(message);
    }
}
