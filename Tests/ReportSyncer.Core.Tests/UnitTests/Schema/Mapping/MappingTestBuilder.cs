using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Mapping;

internal static class MappingTestBuilder
{
    public static ColumnSchema Column(string name,
        Type? clrType = null,
        string dbType = "varchar",
        bool isNullable = false,
        bool isIdentity = false,
        bool isPrimaryKey = false,
        int? maxLength = null,
        bool isComputed = false,
        bool isRowVersion = false)
    {
        return new ColumnSchema(name, clrType ?? typeof(string), dbType, isNullable, isIdentity, isPrimaryKey, maxLength, isComputed, isRowVersion);
    }

    public static TableSchema Table(string tableName, params ColumnSchema[] columns)
    {
        var id = new TableIdentifier("dbo", tableName);
        var pkCols = columns.Where(c => c.IsPrimaryKeyPart).Select(c => c.Name).ToList();
        return new TableSchema(id, columns.ToList(), pkCols, null);
    }

    public static TableTaskConfig TableTask(string source = "dbo.src", string target = "dbo.tgt", bool enableIdentityInsert = false, ColumnMappingConfig? columnMapping = null)
    {
        return new TableTaskConfig(source, target, enabled: true, preSyncTargetAction: false, allowAllDelete: false, enableIdentityInsert: enableIdentityInsert, filter: null, columnMapping: columnMapping);
    }

    public static SchemaPolicyConfig SchemaPolicy(bool allowExtraTarget = false)
    {
        return new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: allowExtraTarget);
    }

    public static ColumnMappingContext Context(
        TableTaskConfig? tableTask,
        ColumnMappingRule? explicitRule,
        TableSchema sourceTable,
        TableSchema targetTable,
        IReadOnlyDictionary<string, string>? jobParameters = null,
        SchemaPolicyConfig? schemaPolicy = null,
        ColumnSchema? targetColumnOverride = null)
    {
        var tt = tableTask ?? TableTask(sourceTable.Table.ToString(), targetTable.Table.ToString());
        var sp = schemaPolicy ?? SchemaPolicy();
        var srcCols = sourceTable.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
        var targetCol = targetColumnOverride ?? targetTable.Columns.First();

        return new ColumnMappingContext(
            tt,
            explicitRule,
            sourceTable,
            targetTable,
            sourceTable.Table,
            targetTable.Table,
            sp,
            jobParameters ?? new Dictionary<string, string>(),
            srcCols,
            targetCol
        );
    }
}
