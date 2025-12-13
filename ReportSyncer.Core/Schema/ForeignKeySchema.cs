using System;
using System.Collections.Generic;

namespace ReportSyncer.Core.Schema
{
    /// <summary>
    /// Pair mapping from child (from) column to parent (to) column.
    /// </summary>
    public sealed record ColumnPair(string FromColumn, string ToColumn)
    {
        public ColumnPair
        {
            if (string.IsNullOrWhiteSpace(FromColumn)) throw new ArgumentException("FromColumn must be provided", nameof(FromColumn));
            if (string.IsNullOrWhiteSpace(ToColumn)) throw new ArgumentException("ToColumn must be provided", nameof(ToColumn));
        }
    }

    /// <summary>
    /// Represents a foreign key relationship between two tables.
    /// </summary>
    public sealed record ForeignKeySchema(
        string Name,
        TableIdentifier FromTable,
        TableIdentifier ToTable,
        IReadOnlyList<ColumnPair> ColumnPairs,
        bool IsCascadeDelete)
    {
        public ForeignKeySchema
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("FK name must be provided", nameof(Name));
            FromTable ??= throw new ArgumentNullException(nameof(FromTable));
            ToTable ??= throw new ArgumentNullException(nameof(ToTable));
            if (ColumnPairs is null || ColumnPairs.Count == 0) throw new ArgumentException("ColumnPairs must contain at least one mapping", nameof(ColumnPairs));
        }
    }
}
