// ============================================================================
// File: PreFlightValidator.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25
// Description: Implements pre-flight validation orchestration for sync jobs.
// ============================================================================

using Microsoft.Extensions.Logging;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Coordinates schema analysis and translates failures into domain exceptions.
/// </summary>
/// <param name="schemaService">The schema analysis service used to inspect source and target schemas.</param>
/// <param name="logger">The logging service used for informational and error messages.</param>
public sealed class PreFlightValidator(ISchemaService schemaService, ILogger<PreFlightValidator> logger) : IPreFlightValidator
{
    private readonly ISchemaService _schemaService = schemaService ?? throw new ArgumentNullException(nameof(schemaService));
    private readonly ILogger<PreFlightValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes pre-flight validation for the supplied <paramref name="job"/> against the
    /// provided <paramref name="effectiveConfig"/>. If schema analysis indicates incompatibilities
    /// a <see cref="SchemaMismatchException"/> is thrown.
    /// </summary>
    /// <param name="effectiveConfig">The effective runtime configuration to validate against.</param>
    /// <param name="job">The job configuration to validate.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> used to cancel the validation.</param>
    /// <returns>A <see cref="PreFlightResult"/> containing the pre-flight outcome.</returns>
    /// <exception cref="SchemaMismatchException">Thrown when schema analysis reports incompatibilities.</exception>
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
            _logger.LogInformation("Preflight passed for job {JobName}", job.Name);
            return result;
        }

        var reason = analysisResult.Error ?? "Schema analysis failed.";
        var message = $"Job='{job.Name}' preflight failed. Reason: {reason}";
        _logger.LogError("Preflight failed for job {JobName}: {Reason}", job.Name, reason);
        throw new SchemaMismatchException(message);
    }
}
