using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Core.Schema;

/// <summary>
/// Abstraction for retrieving schema metadata for a set of tables from a connection.
/// Implementations are responsible for translating DB metadata into the in-memory
/// domain model types (`TableSchema`, `ColumnSchema`, `ForeignKeySchema`).
/// </summary>
public interface ISchemaInspector
{
    /// <summary>
    /// Inspect the specified tables on the provided connection and return a <see cref="SchemaSnapshot"/>.
    /// </summary>
    /// <param name="connection">Connection configuration (not a YAML dependency; passed from upper layers).</param>
    /// <param name="tables">Tables to inspect (only these tables must be returned).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SchemaSnapshot> InspectAsync(ConnectionConfig connection, IEnumerable<TableIdentifier> tables, CancellationToken ct = default);
}