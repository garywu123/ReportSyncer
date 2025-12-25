// ============================================================================
// File: ISchemaService.cs
// Author: Gary Wu
// Date: 2025-12-25
// Project: ReportSyncer
// Description: Facade interface for schema analysis for a sync job (Step 1).
// ============================================================================

using DotNetToolkit.General;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Core.Schema.Services;

/// <summary>
/// Facade contract that analyzes a sync job's schemas, mappings and execution plan.
/// This interface is intentionally minimal for the Step 1 implementation and
/// returns a <see cref="Result{T}"/> wrapper to represent success or failure.
/// </summary>
public interface ISchemaService
{
    /// <summary>
    /// Analyze a single <see cref="SyncJobConfig"/> using the provided effective
    /// <see cref="SyncConfiguration"/>. The returned <see cref="Result{T}"/>
    /// contains a <see cref="SchemaAnalysisResult"/> on success or an error
    /// message on failure.
    /// </summary>
    Task<Result<SchemaAnalysisResult>> AnalyzeJobAsync(
        SyncConfiguration effectiveConfig,
        SyncJobConfig job,
        CancellationToken ct);
}
