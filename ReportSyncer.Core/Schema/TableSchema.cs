using System;
using System.Collections.Generic;
using System.Linq;

namespace ReportSyncer.Core.Schema
{
    /// <summary>
    /// Represents table-level schema information: columns, PK column names and FKs.
    /// Immutable after construction.
    /// </summary>
    public sealed record TableSchema(
        TableIdentifier Table,
        IReadOnlyList<ColumnSchema> Columns,
        IReadOnlyList<string> PrimaryKeyColumns,
        IReadOnlyList<ForeignKeySchema> ForeignKeys)
    {
        public TableSchema
        {
            Table ??= throw new ArgumentNullException(nameof(Table));
            Columns = Columns ?? Array.Empty<ColumnSchema>();
            PrimaryKeyColumns = PrimaryKeyColumns ?? Array.Empty<string>();
            ForeignKeys = ForeignKeys ?? Array.Empty<ForeignKeySchema>();
        }

        public ColumnSchema? GetColumn(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Columns.FirstOrDefault(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, name));
        }
    }
}
