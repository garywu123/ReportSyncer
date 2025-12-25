// ============================================================================
// File: SchemaAnalysisResult.cs
// Author: Gary Wu
// Date: 2025-12-25
// Project: ReportSyncer
// Description: Data container returned by ISchemaService.AnalyzeJobAsync (Step 1).
// ============================================================================

using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Dependency;

namespace ReportSyncer.Core.Schema.Services;

/// <summary>
/// Result container produced by the schema analysis facade. This type is a
/// pure data holder and contains the inspected snapshots, produced mapping,
/// and execution plan for the job.
/// </summary>
public sealed record SchemaAnalysisResult(
    SchemaSnapshot SourceSnapshot,
    SchemaSnapshot TargetSnapshot,
    SchemaMappingResult Mapping,
    ExecutionPlan ExecutionPlan);
