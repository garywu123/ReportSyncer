using FluentAssertions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Mapping;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema.Mapping")]
public class SchemaMapperUnitTests
{
    private static ColumnSchema Col(string name, string dbType, bool isNullable = false, bool isIdentity = false, bool isPk = false)
        => new(name, typeof(object), dbType, isNullable, isIdentity, isPk, null);

    private static TableSchema Tbl(string schema, string name, IEnumerable<ColumnSchema> cols, IEnumerable<string>? pk = null)
        => new(new TableIdentifier(schema, name), cols.ToArray(), pk?.ToArray(), null);

    private static SchemaSnapshot Snapshot(IEnumerable<TableSchema> tables, SchemaRole role)
        => new(tables, role, SchemaInspectionLevel.Full);

    private static SyncJobConfig Job(string sourceConnName, string targetConnName, IEnumerable<TableTaskConfig> tables, IDictionary<string,string>? parameters = null)
        => new("job", "desc", sourceConnName, targetConnName, parameters ?? new Dictionary<string,string>(), tables);

    [Fact]
    public void MapJob_PerfectMatch_Ok()
    {
        var sourceTable = Tbl("dbo", "T", [Col("Id","int", false, false, true), Col("Name","nvarchar")], ["Id"]);
        var targetTable = Tbl("dbo", "T", [Col("Id","int", false, false, true), Col("Name","nvarchar")], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: true, allowExtraTargetColumns: false);
        

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.Errors.Should().BeEmpty();
        result.Value.TableMappings.Should().ContainKey(new TableIdentifier("dbo","T"));
    }

    [Fact]
    public void MapJob_SourceTableMissing_Fail()
    {
        var targetTable = Tbl("dbo", "T", [Col("Id","int")], ["Id"]);
        var sourceSnapshot = Snapshot([], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.Missing","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.SourceTableNotFound);
        result.Value.TableMappings.Should().NotContainKey(new TableIdentifier("dbo","T"));
    }

    [Fact]
    public void MapJob_TargetTableMissing_Fail()
    {
        var sourceTable = Tbl("dbo","S", [Col("Id","int")], ["Id"]);
        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.S","dbo.Missing")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.TargetTableNotFound);
    }

    [Fact]
    public void MapJob_TargetExtraRequiredColumn_Fail()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("Extra","nvarchar", isNullable: false)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.TargetColumnMissingInSource);
    }

    [Fact]
    public void MapJob_TargetExtraNullableColumn_Ignored_WhenPolicyOn()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("Extra","nvarchar", isNullable: true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Warn, requirePrimaryKey: false, allowExtraTargetColumns: true);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        // Extra nullable column should be ignored (mapping created) and not produce missing-column error
        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        mapping.ColumnMappings.Should().ContainSingle(cm => cm.TargetColumn.Name == "Extra" && cm.Kind == MappingKind.Ignored);
    }

    [Fact]
    public void MapJob_TypeIncompatible_Fail()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int"), Col("Value","nvarchar")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("Value","int")], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.TargetColumnTypeIncompatible);
    }

    [Fact]
    public void MapJob_ContextInjection_AddsContextColumn()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        // target has CustomerId column, source does not
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("CustomerId","int", isNullable:false)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var parameters = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { { "CustomerId","101" } };
        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerId"] = new ColumnMappingRule { FromParameter = "CustomerId" }
        });
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)], parameters);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        var cm = mapping.ColumnMappings.SingleOrDefault(c => c.TargetColumn.Name == "CustomerId");
        cm.Should().NotBeNull();
        cm.Kind.Should().Be(MappingKind.Constant);
        cm.SourceColumn.Should().BeNull();
        cm.ConstantValue.Should().Be("101");
    }

    [Fact]
    public void MapJob_ContextParamMissing_Fail()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("CustomerId","int")], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")], new Dictionary<string,string>());
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.TargetColumnMissingInSource);
    }

    [Fact]
    public void MapJob_ContextAmbiguous_Fail()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int"), Col("CustomerId","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("CustomerId","int")], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var parameters = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { { "CustomerId","101" } };
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")], parameters);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        // With environment-based automatic context injection removed, automap will bind the source
        // CustomerId to target CustomerId and the job parameter is ignored unless explicitly configured.
        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        var cm = mapping.ColumnMappings.Single(c => c.TargetColumn.Name == "CustomerId");
        cm.Kind.Should().Be(MappingKind.OneToOne);
    }

    [Fact]
    public void MapJob_ExplicitFromParameterMissing_Fails()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("CustomerId","int", isNullable:false)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerId"] = new ColumnMappingRule { FromParameter = "MissingParam" }
        });

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)], new Dictionary<string,string>());
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.JobParameterMissingForMapping);
    }

    [Fact]
    public void MapJob_ExplicitFromSourceMissing_Fails()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("XCol","nvarchar", isNullable:false)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["XCol"] = new ColumnMappingRule { FromSource = "NonExistent" }
        });

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)], new Dictionary<string,string>());
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.TargetColumnMissingInSource);
    }

    [Fact]
    public void MapJob_ExplicitConstMapping_Works()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), Col("SourceSystem","nvarchar", isNullable:true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceSystem"] = new ColumnMappingRule { Const = "ERP" }
        });

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)], new Dictionary<string,string>());
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        var cm = mapping.ColumnMappings.Single(c => c.TargetColumn.Name == "SourceSystem");
        cm.Kind.Should().Be(MappingKind.Constant);
        cm.ConstantValue.Should().Be("ERP");
    }

    [Fact]
    public void MapJob_RequirePrimaryKey_TargetHasNone_Fail()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")]);
        var targetTable = Tbl("dbo","T", [Col("Id","int")]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: true, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.PrimaryKeyMissing);
    }

    [Fact]
    public void MapJob_ExplicitConstToIdentityWithoutEnable_Fails()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int", isNullable:false, isIdentity:true, isPk:true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = new ColumnMappingRule { Const = "1" }
        });

        // enableIdentityInsert = false (default)
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.IdentityInsertNotEnabled);
    }

    [Fact]
    public void MapJob_ExplicitConstToIdentityWithEnable_Succeeds()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int", isNullable:false, isIdentity:true, isPk:true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = new ColumnMappingRule { Const = "1" }
        });

        // enableIdentityInsert = true
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, true, null, mappingConfig)]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        var cm = mapping.ColumnMappings.SingleOrDefault(c => c.TargetColumn.Name == "Id");
        cm.Should().NotBeNull();
        cm.Kind.Should().NotBe(MappingKind.Ignored);
    }

    [Fact]
    public void MapJob_ExplicitParameterToIdentityWithEnable_Succeeds()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int", isNullable:false, isIdentity:true, isPk:true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = new ColumnMappingRule { FromParameter = "IdParam" }
        });

        var parameters = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { { "IdParam", "42" } };

        // enableIdentityInsert = true
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, true, null, mappingConfig)], parameters);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        var cm = mapping.ColumnMappings.SingleOrDefault(c => c.TargetColumn.Name == "Id");
        cm.Should().NotBeNull();
        cm.Kind.Should().NotBe(MappingKind.Ignored);
        cm.Kind.Should().Be(MappingKind.Constant);
        cm.ConstantValue.Should().Be("42");
    }

    [Fact]
    public void MapJob_AutomapToIdentityWithoutEnable_Fails()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int", isNullable:false, isIdentity:true, isPk:true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        // No explicit mapping config -> automap should attempt to map Id and fail
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.IdentityInsertNotEnabled);
    }

    [Fact]
    public void MapJob_ExplicitIgnoreOnIdentity_AllowsEvenWhenInsertDisabled()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var targetTable = Tbl("dbo","T", [Col("Id","int", isNullable:false, isIdentity:true, isPk:true)], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = new ColumnMappingRule { Ignore = true }
        });

        // enableIdentityInsert = false
        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        var cm = mapping.ColumnMappings.SingleOrDefault(c => c.TargetColumn.Name == "Id");
        cm.Should().NotBeNull();
        cm.Kind.Should().Be(MappingKind.Ignored);
    }

    [Fact]
    public void MapJob_ExplicitConstToComputed_Fails()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        // target has a computed column TotalPrice
        var totalPriceComputed = new ColumnSchema("TotalPrice", typeof(object), "decimal", isNullable: false, isIdentity: false, isPrimaryKeyPart: false, maxLength: null, isComputed: true);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), totalPriceComputed], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["TotalPrice"] = new ColumnMappingRule { Const = "100" }
        });

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.ComputedColumnCannotBeWritten);
    }

    [Fact]
    public void MapJob_AutomapToComputed_Fails()
    {
        // source has TotalPrice, target has computed TotalPrice -> automap should fail
        var sourceTable = Tbl("dbo","T", [Col("Id","int"), Col("TotalPrice","decimal")], ["Id"]);
        var totalPriceComputed = new ColumnSchema("TotalPrice", typeof(object), "decimal", isNullable: false, isIdentity: false, isPrimaryKeyPart: false, maxLength: null, isComputed: true);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), totalPriceComputed], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeFalse();
        result.Value.Errors.Select(e => e.Code).Should().Contain(SchemaMappingErrorCode.ComputedColumnCannotBeWritten);
    }

    [Fact]
    public void MapJob_ExplicitIgnoreOnComputed_Allows()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var totalPriceComputed = new ColumnSchema("TotalPrice", typeof(object), "decimal", isNullable: false, isIdentity: false, isPrimaryKeyPart: false, maxLength: null, isComputed: true);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), totalPriceComputed], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var mappingConfig = new ColumnMappingConfig(mappings: new Dictionary<string, ColumnMappingRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["TotalPrice"] = new ColumnMappingRule { Ignore = true }
        });

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T", true, false, false, false, null, mappingConfig)]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: false, allowExtraTargetColumns: false);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        mapping.ColumnMappings.Should().ContainSingle(cm => cm.TargetColumn.Name == "TotalPrice" && cm.Kind == MappingKind.Ignored);
    }

    [Fact]
    public void MapJob_AllowExtraTargetColumns_AutoIgnoreComputedAndRowVersion()
    {
        var sourceTable = Tbl("dbo","T", [Col("Id","int")], ["Id"]);
        var computedCol = new ColumnSchema("ComputedCol", typeof(object), "decimal", isNullable: false, isIdentity: false, isPrimaryKeyPart: false, maxLength: null, isComputed: true);
        var rowVerCol = new ColumnSchema("RowVer", typeof(object), "timestamp", isNullable: false, isIdentity: false, isPrimaryKeyPart: false, maxLength: null, isComputed: false, isRowVersion: true);
        var targetTable = Tbl("dbo","T", [Col("Id","int"), computedCol, rowVerCol], ["Id"]);

        var sourceSnapshot = Snapshot([sourceTable], SchemaRole.Source);
        var targetSnapshot = Snapshot([targetTable], SchemaRole.Target);

        var job = Job("src","tgt", [new TableTaskConfig("dbo.T","dbo.T")]);
        var sourceConn = new ConnectionConfig("src","conn", EnvironmentType.Dev, ConnectionType.Application);
        var targetConn = new ConnectionConfig("tgt","conn", EnvironmentType.Reporting, ConnectionType.Reporting);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Warn, requirePrimaryKey: false, allowExtraTargetColumns: true);

        var mapper = new SchemaMapper();
        var result = mapper.MapJob(sourceSnapshot, targetSnapshot, job, sourceConn, targetConn, policy);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value.TableMappings[new TableIdentifier("dbo","T")];
        mapping.ColumnMappings.Should().Contain(cm => cm.TargetColumn.Name == "ComputedCol" && cm.Kind == MappingKind.Ignored);
        mapping.ColumnMappings.Should().Contain(cm => cm.TargetColumn.Name == "RowVer" && cm.Kind == MappingKind.Ignored);
    }
}
