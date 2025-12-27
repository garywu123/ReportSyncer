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
    /// <summary>
    /// Builds a command that estimates the number of rows for the given table execution context.
    /// </summary>
    /// <param name="ctx">Execution context describing the target table and any filters.</param>
    /// <returns>A <see cref="DbCommandSpec"/> which will return the row-count when executed.</returns>
    DbCommandSpec BuildCountEstimate(TableExecutionContext ctx);

    /// <summary>
    /// Builds a DELETE command for the specified delete command context.
    /// </summary>
    /// <param name="ctx">Context describing the target table and filters to apply for deletion.</param>
    /// <returns>A <see cref="DbCommandSpec"/> representing the DELETE statement and its parameters.</returns>
    /// <exception cref="ConfigurationException">Thrown when the provided context is invalid for delete operations.</exception>
    DbCommandSpec BuildDelete(DeleteCommandContext ctx);

    /// <summary>
    /// Builds a SELECT command to read source rows for an insert operation.
    /// </summary>
    /// <param name="ctx">Context describing the source table, mapping and filters.</param>
    /// <returns>A <see cref="DbCommandSpec"/> that selects the source rows to be used for inserts.</returns>
    DbCommandSpec BuildSelectSource(InsertCommandContext ctx);

    /// <summary>
    /// Builds an INSERT command to write rows to the target table according to the provided mapping.
    /// </summary>
    /// <param name="ctx">Context describing the target table, mapping and contextual values.</param>
    /// <returns>A <see cref="DbCommandSpec"/> representing the INSERT statement and its parameters.</returns>
    DbCommandSpec BuildInsertTarget(InsertCommandContext ctx);
}
