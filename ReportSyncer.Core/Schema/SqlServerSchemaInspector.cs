using System.Data;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;

// ReSharper disable UnusedMember.Local

namespace ReportSyncer.Core.Schema;

/// <summary>
/// SQL Server implementation of <see cref="ISchemaInspector"/>.
/// Uses a factory delegate to obtain an <see cref="IDbContext"/> for the provided <see cref="ConnectionConfig"/>.
/// <para>
/// Attention: If the supplied <paramref name="tables"/> list is empty the inspector returns an empty
/// <see cref="SchemaSnapshot"/> with <see cref="SchemaRole.Source"/>. When one or more tables are inspected
/// the returned snapshot uses <see cref="SchemaRole.Target"/> by convention. Callers should interpret the
/// reported <see cref="SchemaRole"/> appropriately when merging or comparing snapshots.
/// </para>
/// </summary>
public sealed class SqlServerSchemaInspector(
    Func<ConnectionConfig, IDbContext> dbContextFactory)
    : ISchemaInspector
{
    private readonly Func<ConnectionConfig, IDbContext> _dbContextFactory =
        dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));

    public async Task<SchemaSnapshot> InspectAsync(ConnectionConfig             connection,
                                                   IEnumerable<TableIdentifier> tables,
                                                   CancellationToken            ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tables);

        var requested = tables.ToList();
        // Attention: When no tables are requested we return an empty snapshot
        // with Role = SchemaRole.Source. This represents a deliberate "no-op"
        // inspection result and should be treated differently from an inspected
        // target schema that contains tables.
        if (requested.Count == 0)
            return new SchemaSnapshot(Array.Empty<TableSchema>(), SchemaRole.Source);

        try
        {
            using var ctx = _dbContextFactory(connection);

            var tableSchemas = new List<TableSchema>(requested.Count);

            foreach (var t in requested)
            {
                // 先加载 column 信息，column 信息包含了 PK 信息
                var cols = await LoadColumnsAsync(ctx, t, ct).ConfigureAwait(false);
                if (cols == null || cols.Count == 0)
                    throw new SchemaMismatchException(
                        $"Table not found or has no columns: {t}"
                    );

                var pkCols = cols.Where(c => c.IsPrimaryKeyPart).Select(c => c.Name).ToList();

                // create table schema with empty FK list for now; we'll attach FKs later
                var tableSchema = new TableSchema(
                    t, cols, pkCols, Array.Empty<ForeignKeySchema>()
                );

                tableSchemas.Add(tableSchema);
            }

            // load foreign keys for all involved tables and attach to corresponding TableSchema
            var fkRows = await LoadForeignKeysAsync(ctx, ct).ConfigureAwait(false);

            var fkSchemas = BuildForeignKeySchemas(fkRows);

            // attach FKs where FromTable matches
            var tableMap = tableSchemas.ToDictionary(
                ts => ts.Table, ts => ts, new TableIdentifierComparer()
            );

            foreach (var fk in fkSchemas)
            {
                if (!tableMap.TryGetValue(fk.FromTable, out var tsFrom)) continue;
                var fks = tsFrom.ForeignKeys.ToList();
                fks.Add(fk);
                var newTs = new TableSchema(
                    tsFrom.Table, tsFrom.Columns, tsFrom.PrimaryKeyColumns, fks
                );

                tableMap[fk.FromTable] = newTs;
            }

            var resultTables = tableMap.Values.ToList();
            // Attention: When tables are inspected we return Role = SchemaRole.Target by convention.
            // Callers should consider the Role when combining or comparing snapshots (Source vs Target).
            return new SchemaSnapshot(resultTables, SchemaRole.Target);
        }
        catch (SchemaMismatchException) { throw; }
        catch (Exception ex)
        {
            throw new SyncExecutionException("Failed to inspect schema: " + ex.Message, ex);
        }
    }

    private async Task<List<ColumnSchema>> LoadColumnsAsync(
        IDbContext ctx, TableIdentifier table, CancellationToken ct)
    {
        const string sql = """
                           SELECT s.name AS SchemaName,
                                  t.name AS TableName,
                                  c.name AS ColumnName,
                                  ty.name AS DataType,
                                  c.max_length AS MaxLength,
                                  c.is_nullable AS IsNullable,
                                  ic.is_identity AS IsIdentity,
                                  CASE WHEN pkcols.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKeyPart
                           FROM sys.tables t
                           JOIN sys.schemas s ON t.schema_id = s.schema_id
                           JOIN sys.columns c ON c.object_id = t.object_id
                           LEFT JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                           LEFT JOIN sys.identity_columns ic ON ic.object_id = t.object_id AND ic.column_id = c.column_id
                           LEFT JOIN (
                               SELECT ic2.object_id, ic2.column_id
                               FROM sys.indexes i2
                               JOIN sys.index_columns ic2 ON ic2.object_id = i2.object_id AND ic2.index_id = i2.index_id
                               WHERE i2.is_primary_key = 1
                           ) pkcols ON pkcols.object_id = c.object_id AND pkcols.column_id = c.column_id
                           WHERE s.name = @schema AND t.name = @table
                           ORDER BY c.column_id
                           """;

        var cmd = ctx.CreateCommand(sql, CommandType.Text);
        cmd.AddParameter("@schema", table.SchemaName, DbType.String);
        cmd.AddParameter("@table", table.TableName, DbType.String);

        // 执行查询，尝试将信息映射到 ColumnRow 对象，但是出现错误
        var rows = await ctx.ExecuteQueryAsync<ColumnRow>(cmd, ct).ConfigureAwait(false);

        var result = rows.Select(r => new ColumnSchema(
                    r.ColumnName,
                    MapSqlTypeToClr(r.DataType),
                    r.DataType,
                    r.IsNullable,
                    r.IsIdentity,
                    r.IsPrimaryKeyPart,
                    r.MaxLength is null or 0 ? null : (int?)r.MaxLength
                )
            )
           .ToList();

        return result;
    }


    private async Task<List<ForeignKeyRow>> LoadForeignKeysAsync(
        IDbContext ctx, CancellationToken ct)
    {
        const string sql = @"
SELECT fk.name AS ForeignKeyName,
       sch_from.name AS FromSchema, tab_from.name AS FromTable, col_from.name AS FromColumn,
       sch_to.name AS ToSchema, tab_to.name AS ToTable, col_to.name AS ToColumn,
       fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables tab_from ON fkc.parent_object_id = tab_from.object_id
JOIN sys.schemas sch_from ON tab_from.schema_id = sch_from.schema_id
JOIN sys.columns col_from ON fkc.parent_object_id = col_from.object_id AND fkc.parent_column_id = col_from.column_id
JOIN sys.tables tab_to ON fkc.referenced_object_id = tab_to.object_id
JOIN sys.schemas sch_to ON tab_to.schema_id = sch_to.schema_id
JOIN sys.columns col_to ON fkc.referenced_object_id = col_to.object_id AND fkc.referenced_column_id = col_to.column_id
ORDER BY fk.name, fkc.constraint_column_id";

        var cmd  = ctx.CreateCommand(sql, CommandType.Text);
        var rows = await ctx.ExecuteQueryAsync<ForeignKeyRow>(cmd, ct).ConfigureAwait(false);
        return rows;
    }

    private static IReadOnlyList<ForeignKeySchema> BuildForeignKeySchemas(
        IEnumerable<ForeignKeyRow> fkRows)
    {
        var grouped = fkRows.GroupBy(r => r.ForeignKeyName, StringComparer.OrdinalIgnoreCase);
        var result  = new List<ForeignKeySchema>();

        foreach (var g in grouped)
        {
            var first  = g.First();
            var fromId = new TableIdentifier(first.FromSchema, first.FromTable);
            var toId   = new TableIdentifier(first.ToSchema, first.ToTable);
            var pairs  = g.Select(x => new ColumnPair(x.FromColumn, x.ToColumn)).ToList();
            var isCascade = string.Equals(
                first.DeleteAction, "CASCADE", StringComparison.OrdinalIgnoreCase
            );

            var fk = new ForeignKeySchema(first.ForeignKeyName, fromId, toId, pairs, isCascade);
            result.Add(fk);
        }

        return result;
    }

    private static Type MapSqlTypeToClr(string sqlType)
    {
        if (string.IsNullOrEmpty(sqlType)) return typeof(object);
        sqlType = sqlType.ToLowerInvariant();
        return sqlType switch
        {
            "int"              => typeof(int),
            "bigint"           => typeof(long),
            "smallint"         => typeof(short),
            "tinyint"          => typeof(byte),
            "bit"              => typeof(bool),
            "nvarchar"         => typeof(string),
            "varchar"          => typeof(string),
            "char"             => typeof(string),
            "nchar"            => typeof(string),
            "text"             => typeof(string),
            "ntext"            => typeof(string),
            "datetime"         => typeof(DateTime),
            "datetime2"        => typeof(DateTime),
            "smalldatetime"    => typeof(DateTime),
            "date"             => typeof(DateTime),
            "time"             => typeof(TimeSpan),
            "uniqueidentifier" => typeof(Guid),
            "decimal"          => typeof(decimal),
            "numeric"          => typeof(decimal),
            "float"            => typeof(double),
            "real"             => typeof(float),
            _                  => typeof(object)
        };
    }

    private sealed class ColumnRow
    {
        public string SchemaName       { get; set; } = string.Empty;
        public string TableName        { get; set; } = string.Empty;
        public string ColumnName       { get; set; } = string.Empty;
        public string DataType         { get; set; } = string.Empty;
        public int?   MaxLength        { get; set; }
        public bool   IsNullable       { get; set; }
        public bool   IsIdentity       { get; set; }
        public bool   IsPrimaryKeyPart { get; set; }
    }

    private sealed class ForeignKeyRow
    {
        public string ForeignKeyName { get; set; } = string.Empty;
        public string FromSchema     { get; set; } = string.Empty;
        public string FromTable      { get; set; } = string.Empty;
        public string FromColumn     { get; set; } = string.Empty;
        public string ToSchema       { get; set; } = string.Empty;
        public string ToTable        { get; set; } = string.Empty;
        public string ToColumn       { get; set; } = string.Empty;
        public string DeleteAction   { get; set; } = string.Empty;
    }

    private sealed class TableIdentifierComparer : IEqualityComparer<TableIdentifier>
    {
        public bool Equals(TableIdentifier? x, TableIdentifier? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return StringComparer.OrdinalIgnoreCase.Equals(x.SchemaName, y.SchemaName)
             && StringComparer.OrdinalIgnoreCase.Equals(x.TableName, y.TableName);
        }

        public int GetHashCode(TableIdentifier obj)
        {
            unchecked
            {
                var hc = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SchemaName);
                hc = (hc * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TableName);
                return hc;
            }
        }
    }
}