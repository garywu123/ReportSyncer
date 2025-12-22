namespace ReportSyncer.Core.Schema;

/// <summary>
/// Represents a single column's schema metadata.
/// Immutable once constructed.
/// </summary>
public sealed class ColumnSchema
{
    public string Name             { get; }
    public Type   ClrType          { get; }
    public string DbType           { get; }
    public bool   IsNullable       { get; }
    public bool   IsIdentity       { get; }
    public bool   IsPrimaryKeyPart { get; }
    public int?   MaxLength        { get; }
    public bool   IsComputed       { get; }
    public bool   IsRowVersion     { get; }

    public ColumnSchema(string name,
                        Type   clrType,
                        string dbType,
                        bool   isNullable,
                        bool   isIdentity,
                        bool   isPrimaryKeyPart,
                        int?   maxLength,
                        bool   isComputed = false,
                        bool   isRowVersion = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name must be provided", nameof(name));

        Name = name;
        ClrType = clrType ?? typeof(object);
        DbType = string.IsNullOrWhiteSpace(dbType) ? string.Empty : dbType;
        IsNullable = isNullable;
        IsIdentity = isIdentity;
        IsPrimaryKeyPart = isPrimaryKeyPart;
        MaxLength = maxLength;
        IsComputed = isComputed;
        IsRowVersion = isRowVersion;
    }
}