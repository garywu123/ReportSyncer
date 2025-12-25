namespace ReportSyncer.Core.Schema
{
    /// <summary>
    /// Identifies a table by schema and name. Comparison is case-insensitive
    /// to match typical SQL Server behavior, but schema vs name differences
    /// are preserved (e.g. dbo.Customer != report.Customer).
    /// </summary>
    public sealed class TableIdentifier : IEquatable<TableIdentifier>
    {
        public string SchemaName { get; }
        public string TableName { get; }

        public TableIdentifier(string schemaName, string tableName)
        {
            if (string.IsNullOrWhiteSpace(schemaName)) throw new ArgumentException("SchemaName must be provided", nameof(schemaName));
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("TableName must be provided", nameof(tableName));
            SchemaName = schemaName;
            TableName = tableName;
        }

        public override string ToString() => $"{SchemaName}.{TableName}";

        public bool Equals(TableIdentifier? other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            return StringComparer.OrdinalIgnoreCase.Equals(SchemaName, other.SchemaName)
                    && StringComparer.OrdinalIgnoreCase.Equals(TableName, other.TableName);
        }

        public override bool Equals(object? obj) => Equals(obj as TableIdentifier);

        public override int GetHashCode()
        {
            unchecked
            {
                var hc = StringComparer.OrdinalIgnoreCase.GetHashCode(SchemaName);
                hc = (hc * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(TableName);
                return hc;
            }
        }

        /// <summary>
        /// Parses a table identifier from a string of the form "schema.table" or "table".
        /// When schema is omitted the default schema "dbo" is used.
        /// </summary>
        /// <param name="s">Input identifier string.</param>
        /// <returns>Parsed <see cref="TableIdentifier"/>.</returns>
        /// <exception cref="ArgumentException">When the input is null/empty or not a valid identifier.</exception>
        public static TableIdentifier Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Table identifier must be provided", nameof(s));
            var parts = s.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return new TableIdentifier("dbo", parts[0]);
            }
            if (parts.Length == 2)
            {
                return new TableIdentifier(parts[0], parts[1]);
            }
            throw new ArgumentException($"Invalid table identifier: {s}", nameof(s));
        }
    }
}

