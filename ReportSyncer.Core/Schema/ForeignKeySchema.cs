namespace ReportSyncer.Core.Schema;

/// <summary>
/// Pair mapping from child (from) column to parent (to) column.
/// </summary>
public sealed class ColumnPair
{
    public string FromColumn { get; }
    public string ToColumn   { get; }

    public ColumnPair(string fromColumn, string toColumn)
    {
        if (string.IsNullOrWhiteSpace(fromColumn)) throw new ArgumentException("FromColumn must be provided", nameof(fromColumn));
        if (string.IsNullOrWhiteSpace(toColumn)) throw new ArgumentException("ToColumn must be provided", nameof(toColumn));
        FromColumn = fromColumn;
        ToColumn = toColumn;
    }
}

/// <summary>
/// Represents a foreign key relationship between two tables.
/// </summary>
public sealed class ForeignKeySchema
{
    public string                    Name            { get; }
    public TableIdentifier           FromTable       { get; }
    public TableIdentifier           ToTable         { get; }
    public IReadOnlyList<ColumnPair> ColumnPairs     { get; }
    public bool                      IsCascadeDelete { get; }

    public ForeignKeySchema(string                    name,
                            TableIdentifier           fromTable,
                            TableIdentifier           toTable,
                            IReadOnlyList<ColumnPair> columnPairs,
                            bool                      isCascadeDelete)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("FK name must be provided", nameof(name));
        FromTable = fromTable ?? throw new ArgumentNullException(nameof(fromTable));
        ToTable = toTable     ?? throw new ArgumentNullException(nameof(toTable));
        if (columnPairs is null || columnPairs.Count == 0) throw new ArgumentException("ColumnPairs must contain at least one mapping", nameof(columnPairs));

        Name = name;
        ColumnPairs = columnPairs;
        IsCascadeDelete = isCascadeDelete;
    }
}