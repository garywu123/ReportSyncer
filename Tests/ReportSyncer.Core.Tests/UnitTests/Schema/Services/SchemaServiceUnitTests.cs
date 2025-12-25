using FluentAssertions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Services;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema.Services")]
public class SchemaServiceUnitTests
{
    // Minimal fakes for dependencies
    private class FakeInspector : ISchemaInspector
    {
        public Func<SchemaInspectionRequest, CancellationToken, Task<SchemaSnapshot>>? OnInspect { get; set; }

        public Task<SchemaSnapshot> InspectAsync(SchemaInspectionRequest request, CancellationToken ct = default)
        {
            if (OnInspect != null) return OnInspect(request, ct);
            // Default: return empty snapshot with requested level/role
            return Task.FromResult(new SchemaSnapshot(Array.Empty<TableSchema>(), request.Role, request.Level));
        }
    }

    private class FakeMapper : ISchemaMapper
    {
        public Func<SchemaSnapshot, SchemaSnapshot, SyncJobConfig, ConnectionConfig, ConnectionConfig, SchemaPolicyConfig, DotNetToolkit.General.Result<SchemaMappingResult>>? OnMap { get; set; }

        public DotNetToolkit.General.Result<SchemaMappingResult> MapJob(SchemaSnapshot sourceSnapshot, SchemaSnapshot targetSnapshot, SyncJobConfig job, ConnectionConfig sourceConnection, ConnectionConfig targetConnection, SchemaPolicyConfig schemaPolicy)
        {
            if (OnMap != null) return OnMap(sourceSnapshot, targetSnapshot, job, sourceConnection, targetConnection, schemaPolicy);
            var empty = new SchemaMappingResult(true, Array.Empty<SchemaMappingError>(), new Dictionary<TableIdentifier, TableMapping>());
            return DotNetToolkit.General.Result<SchemaMappingResult>.Ok(empty);
        }
    }

    private class FakeResolver : IDependencyResolver
    {
        public Func<SchemaSnapshot, IReadOnlyList<TableIdentifier>, DotNetToolkit.General.Result<ExecutionPlan>>? OnBuild { get; set; }

        public DotNetToolkit.General.Result<ExecutionPlan> BuildExecutionPlan(SchemaSnapshot targetSnapshot, IReadOnlyList<TableIdentifier> selectedTargetTables)
        {
            if (OnBuild != null) return OnBuild(targetSnapshot, selectedTargetTables);
            return DotNetToolkit.General.Result<ExecutionPlan>.Ok(ExecutionPlan.Empty);
        }
    }

    // Helper factories
    private static ConnectionConfig Conn(string name) => new(name, "conn", EnvironmentType.Dev, ConnectionType.Application);

    private static RunConfig DefaultRun() => new(false, 2000, 5000, true);

    private static SafetyConfig DefaultSafety() => new(false, true, 0.8);

    private static SchemaPolicyConfig DefaultPolicy() => new(SchemaMismatchBehavior.Fail, requirePrimaryKey: true, allowExtraTargetColumns: false);

    private static SyncConfiguration CreateConfig(IEnumerable<ConnectionConfig> connections, IEnumerable<SyncJobConfig> jobs)
        => new("1.0", DefaultRun(), DefaultSafety(), DefaultPolicy(), connections, jobs);

    private static SyncJobConfig CreateJob(string name, string sourceConn, string targetConn, params TableTaskConfig[] tables)
        => new(name, null, sourceConn, targetConn, new Dictionary<string, string>(), tables);

    private static TableTaskConfig Table(string source, string target, bool enabled = true)
        => new(source, target, enabled);

    [Fact]
    public async Task AnalyzeJobAsync_WithNoEnabledTables_ReturnsFailure()
    {
        var job = CreateJob("job1", "src", "tgt", Table("dbo.S", "dbo.T", enabled: false));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No enabled table tasks found");
        result.Error.Should().Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithInvalidSourceTableIdentifier_ReturnsFailure()
    {
        var job = CreateJob("job2", "src", "tgt", Table("a.b.c", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid table identifier");
        result.Error.Should().Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithInvalidTargetTableIdentifier_ReturnsFailure()
    {
        var job = CreateJob("job3", "src", "tgt", Table("dbo.S", "x.y.z", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid table identifier");
        result.Error.Should().Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithMissingSourceConnection_ReturnsFailure()
    {
        var job = CreateJob("job4", "missing_src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        // Config contains only target connection
        var cfg = CreateConfig(new[] { Conn("tgt") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Source connection");
        result.Error.Should().Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithMissingTargetConnection_ReturnsFailure()
    {
        var job = CreateJob("job5", "src", "missing_tgt", Table("dbo.S", "dbo.T", enabled: true));
        // Config contains only source connection
        var cfg = CreateConfig(new[] { Conn("src") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Target connection");
        result.Error.Should().Contain(job.Name);
    }
}
