

// ReSharper disable MemberCanBePrivate.Global

namespace ReportSyncer.Core.Schema;

/// <summary>
/// Represents table-level schema information: columns, PK column names and FKs.
/// Immutable after construction.
/// </summary>
public sealed class TableSchema(
    TableIdentifier                  table,
    IReadOnlyList<ColumnSchema>      columns,
    IReadOnlyList<string>?           primaryKeyColumns,
    IReadOnlyList<ForeignKeySchema>? foreignKeys)
{
    public TableIdentifier Table { get; } =
        table ?? throw new ArgumentNullException(nameof(table));

    public IReadOnlyList<ColumnSchema>     Columns           { get; } = columns           ?? [];
    public IReadOnlyList<string>           PrimaryKeyColumns { get; } = primaryKeyColumns ?? [];
    public IReadOnlyList<ForeignKeySchema> ForeignKeys       { get; } = foreignKeys       ?? [];

    /// <summary>
    /// Returns the column with the specified name, or null when not found.
    /// Name comparison is <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <param name="name">Column name to look up.</param>
    /// <returns>Matching <see cref="ColumnSchema"/> or null.</returns>
    public ColumnSchema? GetColumn(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : Columns.FirstOrDefault(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, name)
            );
    }

    /// <summary>
    /// Attempts to get a column by name using case-insensitive comparison.
    /// </summary>
    /// <param name="name">Column name to locate.</param>
    /// <param name="column">Out parameter set to the found column or null.</param>
    /// <returns>True when the column was found.</returns>
    public bool TryGetColumn(string name, out ColumnSchema? column)
    {
        column = GetColumn(name);
        return column is not null;
    }
}