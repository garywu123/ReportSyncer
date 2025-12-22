// ============================================================================
// File: ISchemaMapper.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Interface for the schema mapper used during pre-flight validation.
// ============================================================================

using DotNetToolkit.General;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Maps source and target snapshots according to a job configuration and schema policy.
/// </summary>
public interface ISchemaMapper
{
    /// <summary>
    /// Produces a <see cref="SchemaMappingResult"/> for the provided job and snapshots.
    /// </summary>
    /// <param name="sourceSnapshot">Snapshot of source schemas.</param>
    /// <param name="targetSnapshot">Snapshot of target schemas.</param>
    /// <param name="job">Sync job configuration driving which tables to map.</param>
    /// <param name="sourceConnection">Source connection metadata.</param>
    /// <param name="targetConnection">Target connection metadata.</param>
    /// <param name="schemaPolicy">Schema validation policy.</param>
    /// <returns>Result containing the <see cref="SchemaMappingResult"/> on success or failure.</returns>
    Result<SchemaMappingResult> MapJob(
        SchemaSnapshot sourceSnapshot,
        SchemaSnapshot targetSnapshot,
        SyncJobConfig job,
        ConnectionConfig sourceConnection,
        ConnectionConfig targetConnection,
        SchemaPolicyConfig schemaPolicy
    );
}
