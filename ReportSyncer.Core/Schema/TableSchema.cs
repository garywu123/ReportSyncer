

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

    public ColumnSchema? GetColumn(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : Columns.FirstOrDefault(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, name)
            );
    }
}