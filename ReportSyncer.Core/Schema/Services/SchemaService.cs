// ============================================================================
// File: SchemaService.cs
// Author: Gary Wu
// Date: 2025-12-25
// Project: ReportSyncer
// Description: Schema facade implementation skeleton (Step 2).
// ============================================================================

using DotNetToolkit.General;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Dependency;

namespace ReportSyncer.Core.Schema.Services;

/// <summary>
/// Facade service that coordinates schema inspection, mapping and dependency planning.
/// This file contains only the constructor wiring and a placeholder implementation
/// for <see cref="ISchemaService.AnalyzeJobAsync"/> as required by Step 2.
/// </summary>
public sealed class SchemaService(
    ISchemaInspector inspector,
    ISchemaMapper mapper,
    IDependencyResolver resolver) : ISchemaService
{
    private readonly ISchemaInspector _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
    private readonly ISchemaMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private readonly IDependencyResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>
    /// Analyze the schema surface for a single <see cref="SyncJobConfig"/>.
    /// This method performs the initial steps of the Schema Facade: it
    /// - extracts the enabled table tasks from the job,
    /// - prepares inspection requests and invokes the source <see cref="ISchemaInspector"/>.
    ///
    /// The full pipeline (target inspection, mapping and dependency resolution)
    /// is implemented in subsequent steps; this method currently implements
    /// Steps 3 and 4 of the design document and returns a failure result for
    /// the unimplemented remainder.
    ///
    /// Error handling rules:
    /// - This method never throws for expected failures; it returns <see cref="Result{T}.Fail"/>.
    /// - Error messages include the job name and any table identifiers when applicable.
    /// </summary>
    /// <param name="effectiveConfig">Validated effective <see cref="SyncConfiguration"/>.</param>
    /// <param name="job">The <see cref="SyncJobConfig"/> to analyze.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{SchemaAnalysisResult}"/> containing a success value when the
    /// analysis completes, or a failure with a human-readable error message.
    /// </returns>
    public async Task<Result<SchemaAnalysisResult>> AnalyzeJobAsync(
        SyncConfiguration effectiveConfig,
        SyncJobConfig job,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(effectiveConfig);
        ArgumentNullException.ThrowIfNull(job);

        // Step 3: Extract enabled table tasks from the job.
        // - Only tables where TableTaskConfig.Enabled == true are considered.
        // - Build sourceTables and targetTables lists used by inspectors and dependency resolver.
        var enabledTasks = job.Tables?.Where(t => t.Enabled).ToList() ?? new List<TableTaskConfig>();

        if (enabledTasks.Count == 0)
        {
            return Result<SchemaAnalysisResult>.Fail($"No enabled table tasks found for job '{job.Name}'.");
        }

        // Unique identifier collections for inspection.
        List<TableIdentifier> sourceTables = new List<TableIdentifier>();
        List<TableIdentifier> targetTables = new List<TableIdentifier>();

        try
        {
            foreach (var tt in enabledTasks)
            {
                var s = TableIdentifier.Parse(tt.Source);
                var t = TableIdentifier.Parse(tt.Target);
                if (!sourceTables.Contains(s)) sourceTables.Add(s);
                if (!targetTables.Contains(t)) targetTables.Add(t);
            }
        }
        catch (Exception ex)
        {
            // Parsing TableIdentifier failed — return failure with job context.
            return Result<SchemaAnalysisResult>.Fail($"Invalid table identifier in job '{job.Name}': {ex.Message}");
        }

        // Step 4: Call source inspector with the minimal inspection level required
        // for mapping (ExistenceOnly). Any inspector exception is caught and
        // converted into a Result.Fail that includes the job name for context.
        var sourceConnection = effectiveConfig.Connections.FirstOrDefault(c => c.Name == job.SourceConnection);
        if (sourceConnection == null)
            return Result<SchemaAnalysisResult>.Fail($"Source connection '{job.SourceConnection}' not found for job '{job.Name}'.");

        var sourceRequest = new SchemaInspectionRequest
        {
            Connection = sourceConnection,
            Tables = sourceTables,
            Role = SchemaRole.Source,
            Level = SchemaInspectionLevel.ExistenceOnly
        };

        SchemaSnapshot sourceSnapshot;

        try
        {
            // Invoke inspector — this may throw for transient DB errors; catch and return a failure.
            sourceSnapshot = await _inspector.InspectAsync(sourceRequest, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Include job name in the error per Rule 4.
            return Result<SchemaAnalysisResult>.Fail($"Source schema inspection failed for job '{job.Name}': {ex.Message}");
        }

        // Step 5: Inspect Target (Full) — required for FK metadata used by mapping and dependency resolver.
        var targetConnection = effectiveConfig.Connections.FirstOrDefault(c => c.Name == job.TargetConnection);
        if (targetConnection == null)
            return Result<SchemaAnalysisResult>.Fail($"Target connection '{job.TargetConnection}' not found for job '{job.Name}'.");

        var targetRequest = new SchemaInspectionRequest
        {
            Connection = targetConnection,
            Tables = targetTables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        SchemaSnapshot targetSnapshot;
        try
        {
            // Target inspection must be Full to include FK/PK metadata.
            targetSnapshot = await _inspector.InspectAsync(targetRequest, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<SchemaAnalysisResult>.Fail($"Target schema inspection failed for job '{job.Name}': {ex.Message}");
        }

        // Step 6: Call SchemaMapper to produce column/table mappings.
        Result<SchemaMappingResult> mappingResult;
        try
        {
            mappingResult = _mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConnection, targetConnection, effectiveConfig.SchemaPolicy);
        }
        catch (Exception ex)
        {
            // Mapper should not throw, but guard defensively.
            return Result<SchemaAnalysisResult>.Fail($"Schema mapping threw an exception for job '{job.Name}': {ex.Message}");
        }

        if (mappingResult.IsFailure)
        {
            // Preserve mapper's error text and include job context.
            var err = mappingResult.Error ?? "Schema mapping failed";
            return Result<SchemaAnalysisResult>.Fail($"Schema mapping failed for job '{job.Name}': {err}");
        }

        var mapping = mappingResult.Value;

        // Step 7: Dependency resolution — build execution plan from target snapshot and selected target tables.
        Result<ExecutionPlan> planResult;
        try
        {
            planResult = _resolver.BuildExecutionPlan(targetSnapshot, targetTables);
        }
        catch (Exception ex)
        {
            return Result<SchemaAnalysisResult>.Fail($"Dependency resolution threw an exception for job '{job.Name}': {ex.Message}");
        }

        if (planResult.IsFailure)
        {
            var perr = planResult.Error ?? "Dependency resolution failed";
            return Result<SchemaAnalysisResult>.Fail($"Dependency resolution failed for job '{job.Name}': {perr}");
        }

        var executionPlan = planResult.Value;

        // All steps succeeded — assemble and return the analysis result.
        var result = new SchemaAnalysisResult(sourceSnapshot, targetSnapshot, mapping, executionPlan);
        return Result<SchemaAnalysisResult>.Ok(result);
    }
}
