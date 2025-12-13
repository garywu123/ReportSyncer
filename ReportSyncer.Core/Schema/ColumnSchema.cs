using System;

namespace ReportSyncer.Core.Schema
{
    /// <summary>
    /// Represents a single column's schema metadata.
    /// Immutable once constructed.
    /// </summary>
    public sealed record ColumnSchema(
        string Name,
        Type ClrType,
        string DbType,
        bool IsNullable,
        bool IsIdentity,
        bool IsPrimaryKeyPart,
        int? MaxLength)
    {
        public ColumnSchema
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Column name must be provided", nameof(Name));
            ClrType ??= typeof(object);
            if (string.IsNullOrWhiteSpace(DbType)) DbType = string.Empty;
        }
    }
}
