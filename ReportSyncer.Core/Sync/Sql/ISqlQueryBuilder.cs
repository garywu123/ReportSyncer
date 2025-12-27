// ============================================================================
// File: ISqlQueryBuilder.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Contract for building SQL command specs for sync operations.
// ============================================================================

using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync.Sql;

/// <summary>
/// Builds provider-specific SQL command specifications for sync operations.
/// </summary>
public interface ISqlQueryBuilder
{
    DbCommandSpec BuildCountEstimate(TableExecutionContext ctx);

    DbCommandSpec BuildDelete(DeleteCommandContext ctx);

    DbCommandSpec BuildSelectSource(InsertCommandContext ctx);

    DbCommandSpec BuildInsertTarget(InsertCommandContext ctx);
}
