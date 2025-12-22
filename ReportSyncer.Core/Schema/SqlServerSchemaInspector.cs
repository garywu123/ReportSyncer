using System.Data;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;

// ReSharper disable UnusedMember.Local

namespace ReportSyncer.Core.Schema;

/// <summary>
/// SQL Server implementation of <see cref="ISchemaInspector"/>.
/// <para>
/// Uses a factory delegate to obtain an <see cref="IDbContext"/> for the provided
/// <see cref="ConnectionConfig"/>. Supports two inspection levels via
/// <see cref="SchemaInspectionLevel"/>:
/// </para>
/// <list type="bullet">
/// <item><c>ExistenceOnly</c>: Loads columns and PK metadata (no FK loading). Suitable for source snapshots.</item>
/// <item><c>Full</c>: Loads columns, PK, and FK metadata. Required for target snapshots needing dependency ordering.</item>
/// </list>
/// </summary>
/// <remarks>
/// The inspector returns a <see cref="SchemaSnapshot"/> where <see cref="SchemaSnapshot.Role"/>
/// matches the caller-supplied request.
/// </remarks>
public sealed class SqlServerSchemaInspector(
    Func<ConnectionConfig, IDbContext> dbContextFactory)
    : ISchemaInspector
{
    private readonly Func<ConnectionConfig, IDbContext> _dbContextFactory =
        dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));

    #region SqlTemplates

    private static readonly string ColumnsSql = """
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

    private static readonly string ForeignKeysSql = """
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
    ORDER BY fk.name, fkc.constraint_column_id
    """;

    #endregion

    /// <summary>
    /// Inspect the schema for the supplied <paramref name="request"/> and return a
    /// <see cref="SchemaSnapshot"/> describing the discovered tables.
    /// </summary>
    /// <param name="request">Inspection request containing connection, tables, role and level.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SchemaSnapshot"/> matching the requested <see cref="SchemaRole"/> and <see cref="SchemaInspectionLevel"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="request"/> or required properties are null.</exception>
    /// <exception cref="SchemaMismatchException">When a requested table cannot be found (existence or column check failed).</exception>
    /// <exception cref="SyncExecutionException">When an unexpected error occurs while querying the database.</exception>
    public async Task<SchemaSnapshot> InspectAsync(SchemaInspectionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Connection);
        ArgumentNullException.ThrowIfNull(request.Tables);

        var requested = request.Tables.ToList();

        // When no tables are requested return an empty snapshot preserving the requested Role/Level
        if (requested.Count == 0)
            return new SchemaSnapshot(Array.Empty<TableSchema>(), request.Role, request.Level);

        try
        {
            using var ctx = _dbContextFactory(request.Connection);

            if (request.Level == SchemaInspectionLevel.ExistenceOnly)
            {
                // ExistenceOnly: load columns for type compatibility checks, skip FK loading
                var tables = await InspectWithoutForeignKeysAsync(ctx, requested, ct).ConfigureAwait(false);
                return new SchemaSnapshot(tables, request.Role, request.Level);
            }

            // Full inspection: load columns + FK metadata
            var tablesFull = await InspectFullAsync(ctx, requested, ct).ConfigureAwait(false);
            return new SchemaSnapshot(tablesFull, request.Role, SchemaInspectionLevel.Full);
        }
        catch (SchemaMismatchException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new SyncExecutionException("Failed to inspect schema: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Load column metadata for the specified <paramref name="table"/> from the
    /// provided <paramref name="ctx"/>. Returns an empty list only when the
    /// table has no columns (caller treats that as missing table).
    /// </summary>
    /// <param name="ctx">Database context to use for queries.</param>
    /// <param name="table">Table identifier (schema + name).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="ColumnSchema"/> for the table.</returns>
    private async Task<List<ColumnSchema>> LoadColumnsAsync(
        IDbContext ctx, TableIdentifier table, CancellationToken ct)
    {
        var cmd = ctx.CreateCommand(ColumnsSql, CommandType.Text);
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


    /// <summary>
    /// Load all foreign key rows for the database visible to the connection.
    /// The returned rows are later grouped into <see cref="ForeignKeySchema"/> objects.
    /// </summary>
    /// <param name="ctx">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw foreign-key rows used to construct <see cref="ForeignKeySchema"/> instances.</returns>
    private async Task<List<ForeignKeyRow>> LoadForeignKeysAsync(
        IDbContext ctx, CancellationToken ct)
    {
        var cmd = ctx.CreateCommand(ForeignKeysSql, CommandType.Text);
        var rows = await ctx.ExecuteQueryAsync<ForeignKeyRow>(cmd, ct).ConfigureAwait(false);
        return rows;
    }

    /// <summary>
    /// Inspect the requested tables without loading foreign key metadata.
    /// This mode loads columns and PK information for type compatibility checks
    /// but skips FK loading (used for Source snapshots where FKs aren't needed).
    /// </summary>
    /// <param name="ctx">Database context.</param>
    /// <param name="requested">List of table identifiers to inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="TableSchema"/> instances with columns but no FKs.</returns>
    private async Task<List<TableSchema>> InspectWithoutForeignKeysAsync(IDbContext ctx, List<TableIdentifier> requested, CancellationToken ct)
    {
        var tableSchemas = new List<TableSchema>(requested.Count);

        foreach (var t in requested)
        {
            var cols = await LoadColumnsAsync(ctx, t, ct).ConfigureAwait(false);
            if (cols == null || cols.Count == 0)
                throw new SchemaMismatchException($"Table not found or has no columns: {t}");

            var pkCols = cols.Where(c => c.IsPrimaryKeyPart).Select(c => c.Name).ToList();
            tableSchemas.Add(new TableSchema(t, cols, pkCols, Array.Empty<ForeignKeySchema>()));
        }

        return tableSchemas;
    }

    /// <summary>
    /// Inspect the requested tables in <c>Full</c> mode: load columns (and detect PK/identity)
    /// and load/attach foreign keys for involved tables.
    /// </summary>
    /// <param name="ctx">Database context.</param>
    /// <param name="requested">List of table identifiers to fully inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Fully populated <see cref="TableSchema"/> instances including FK metadata.</returns>
    private async Task<List<TableSchema>> InspectFullAsync(IDbContext ctx, List<TableIdentifier> requested, CancellationToken ct)
    {
        var fullTableSchemas = new List<TableSchema>(requested.Count);

        foreach (var t in requested)
        {
            var cols = await LoadColumnsAsync(ctx, t, ct).ConfigureAwait(false);
            if (cols == null || cols.Count == 0)
                throw new SchemaMismatchException($"Table not found or has no columns: {t}");

            var pkCols = cols.Where(c => c.IsPrimaryKeyPart).Select(c => c.Name).ToList();

            var tableSchema = new TableSchema(t, cols, pkCols, Array.Empty<ForeignKeySchema>());
            fullTableSchemas.Add(tableSchema);
        }

        var fkRows = await LoadForeignKeysAsync(ctx, ct).ConfigureAwait(false);
        var fkSchemas = BuildForeignKeySchemas(fkRows);

        var tableMap = fullTableSchemas.ToDictionary(ts => ts.Table, ts => ts, new TableIdentifierComparer());

        foreach (var fk in fkSchemas)
        {
            if (!tableMap.TryGetValue(fk.FromTable, out var tsFrom)) continue;
            var fks = tsFrom.ForeignKeys.ToList();
            fks.Add(fk);
            var newTs = new TableSchema(tsFrom.Table, tsFrom.Columns, tsFrom.PrimaryKeyColumns, fks);

            tableMap[fk.FromTable] = newTs;
        }

        return tableMap.Values.ToList();
    }

    /// <summary>
    /// Build grouped <see cref="ForeignKeySchema"/> objects from raw <see cref="ForeignKeyRow"/> results.
    /// </summary>
    /// <param name="fkRows">Raw FK rows returned from the DB.</param>
    /// <returns>Collection of <see cref="ForeignKeySchema"/> grouped by constraint name.</returns>
    private static IReadOnlyList<ForeignKeySchema> BuildForeignKeySchemas(
        IEnumerable<ForeignKeyRow> fkRows)
    {
        var grouped = fkRows.GroupBy(r => r.ForeignKeyName, StringComparer.OrdinalIgnoreCase);
        var result = new List<ForeignKeySchema>();

        foreach (var g in grouped)
        {
            var first = g.First();
            var fromId = new TableIdentifier(first.FromSchema, first.FromTable);
            var toId = new TableIdentifier(first.ToSchema, first.ToTable);
            var pairs = g.Select(x => new ColumnPair(x.FromColumn, x.ToColumn)).ToList();
            var isCascade = string.Equals(
                first.DeleteAction, "CASCADE", StringComparison.OrdinalIgnoreCase
            );

            var fk = new ForeignKeySchema(first.ForeignKeyName, fromId, toId, pairs, isCascade);
            result.Add(fk);
        }

        return result;
    }

    /// <summary>
    /// Map a SQL Server type name to a CLR <see cref="Type"/>. Falls back to <see cref="object"/> for unknown types.
    /// </summary>
    /// <param name="sqlType">SQL type name (e.g. 'int', 'nvarchar').</param>
    /// <returns>Corresponding CLR <see cref="Type"/>.</returns>
    private static Type MapSqlTypeToClr(string sqlType)
    {
        if (string.IsNullOrEmpty(sqlType)) return typeof(object);
        sqlType = sqlType.ToLowerInvariant();
        return sqlType switch
        {
            "int" => typeof(int),
            "bigint" => typeof(long),
            "smallint" => typeof(short),
            "tinyint" => typeof(byte),
            "bit" => typeof(bool),
            "nvarchar" => typeof(string),
            "varchar" => typeof(string),
            "char" => typeof(string),
            "nchar" => typeof(string),
            "text" => typeof(string),
            "ntext" => typeof(string),
            "datetime" => typeof(DateTime),
            "datetime2" => typeof(DateTime),
            "smalldatetime" => typeof(DateTime),
            "date" => typeof(DateTime),
            "time" => typeof(TimeSpan),
            "uniqueidentifier" => typeof(Guid),
            "decimal" => typeof(decimal),
            "numeric" => typeof(decimal),
            "float" => typeof(double),
            "real" => typeof(float),
            _ => typeof(object)
        };
    }

    /// <summary>
    /// Helper POCO used to map column query results.
    /// </summary>
    private sealed class ColumnRow
    {
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKeyPart { get; set; }
    }

    /// <summary>
    /// Helper POCO used to map foreign-key query results.
    /// </summary>
    private sealed class ForeignKeyRow
    {
        public string ForeignKeyName { get; set; } = string.Empty;
        public string FromSchema { get; set; } = string.Empty;
        public string FromTable { get; set; } = string.Empty;
        public string FromColumn { get; set; } = string.Empty;
        public string ToSchema { get; set; } = string.Empty;
        public string ToTable { get; set; } = string.Empty;
        public string ToColumn { get; set; } = string.Empty;
        public string DeleteAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Case-insensitive comparer for <see cref="TableIdentifier"/> instances.
    /// </summary>
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