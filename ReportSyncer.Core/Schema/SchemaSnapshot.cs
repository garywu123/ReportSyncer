namespace ReportSyncer.Core.Schema;

/// <summary>
/// Role of the snapshot (source or target) so callers can differentiate semantics.
/// </summary>
public enum SchemaRole
{
    Source,
    Target
}

/// <summary>
/// Immutable snapshot of a set of table schemas for a given role.
/// </summary>
public sealed class SchemaSnapshot
{
    private readonly IReadOnlyDictionary<TableIdentifier, TableSchema> _tables;

    public SchemaSnapshot(IEnumerable<TableSchema> tables, SchemaRole role)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        Role = role;
        // TableIdentifier implements case-insensitive equality for names.
        _tables = tables.ToDictionary(t => t.Table, t => t);
    }

    public SchemaRole Role { get; }

    public IReadOnlyCollection<TableSchema> Tables => _tables.Values.ToList();

    public bool TryGetTable(TableIdentifier id, out TableSchema? table) => _tables.TryGetValue(id, out table);

    public TableSchema GetRequiredTable(TableIdentifier id)
    {
        if (!_tables.TryGetValue(id, out var t)) throw new KeyNotFoundException($"Table not found: {id}");
        return t;
    }
}