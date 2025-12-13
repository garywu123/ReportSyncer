using System;

namespace ReportSyncer.Core.Schema

/// <summary>
/// Identifies a table by schema and name. Comparison is case-insensitive
/// to match typical SQL Server behavior, but schema vs name differences
/// are preserved (e.g. dbo.Customer != report.Customer).
/// </summary>
public sealed record TableIdentifier(string SchemaName, string TableName)
{
    public TableIdentifier
    {
        if (string.IsNullOrWhiteSpace(SchemaName)) throw new ArgumentException("SchemaName must be provided", nameof(SchemaName));
        if (string.IsNullOrWhiteSpace(TableName)) throw new ArgumentException("TableName must be provided", nameof(TableName));
    }

    public override string ToString() => $"{SchemaName}.{TableName}";

    public virtual bool Equals(TableIdentifier? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return StringComparer.OrdinalIgnoreCase.Equals(SchemaName, other.SchemaName)
                && StringComparer.OrdinalIgnoreCase.Equals(TableName, other.TableName);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hc = StringComparer.OrdinalIgnoreCase.GetHashCode(SchemaName);
            hc = (hc * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(TableName);
            return hc;
        }
    }
}

