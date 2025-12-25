using FluentAssertions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Tests.Helpers.TestDoubles;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using DotNetToolkit.General;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Services;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema.Services")]
public class SchemaServiceUnitTests
{
    // Using shared test doubles in Helpers/TestDoubles/SchemaTestDoubles.cs

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

    // Use SchemaTestHelpers.CreateSnapshot when needed

    [Fact]
    public async Task AnalyzeJobAsync_WithNoEnabledTables_ReturnsFailure()
    {
        var job = CreateJob("job1", "src", "tgt", Table("dbo.S", "dbo.T", enabled: false));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No enabled table tasks found").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithInvalidSourceTableIdentifier_ReturnsFailure()
    {
        var job = CreateJob("job2", "src", "tgt", Table("a.b.c", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var svc = new SchemaService(new FakeInspector(), new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid table identifier").And.Contain(job.Name);
    }

    [Theory]
    [InlineData("a.b.c", "dbo.T", "a.b.c")]
    [InlineData("dbo.S", "x.y.z", "x.y.z")]
    public async Task AnalyzeJobAsync_WithInvalidTableIdentifier_ReturnsFailure(
        string source, string target, string expectedInvalid)
    {
        var job = CreateJob("job-invalid", "src", "tgt", Table(source, target, enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new RecordingFakeInspector();
        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid table identifier").And.Contain(job.Name).And.Contain(expectedInvalid);
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
        result.Error.Should().Contain("Source connection").And.Contain(job.Name);
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
        result.Error.Should().Contain("Target connection").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenSourceInspectorThrows_ReturnsFailure()
    {
        var job = CreateJob("job6", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector { ExceptionToThrow = new Exception("inspect fail") };
        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Source schema inspection failed").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenTargetInspectorThrows_ReturnsFailure()
    {
        var job = CreateJob("job7", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector();
        inspector.OnInspect = (req, ct) =>
        {
            if (req.Role == SchemaRole.Target) throw new Exception("target inspect fail");
            return Task.FromResult(new SchemaSnapshot(Array.Empty<TableSchema>(), req.Role, req.Level));
        };

        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Target schema inspection failed").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenMapperThrows_ReturnsFailure()
    {
        var job = CreateJob("job8", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector();
        var mapper = new FakeMapper { ExceptionToThrow = new Exception("mapper boom") };

        var svc = new SchemaService(inspector, mapper, new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Schema mapping threw").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenMapperReturnsFailure_PreservesError()
    {
        var job = CreateJob("job9", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector();
        var mapper = new FakeMapper();
        mapper.OnMap = (s, t, j, sc, tc, policy) => Result<SchemaMappingResult>.Fail("mapping error");

        var svc = new SchemaService(inspector, mapper, new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Schema mapping failed").And.Contain("mapping error").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenDependencyResolverThrows_ReturnsFailure()
    {
        var job = CreateJob("job10", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector();
        var resolver = new FakeResolver { ExceptionToThrow = new Exception("resolver boom") };

        var svc = new SchemaService(inspector, new FakeMapper(), resolver);

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Dependency resolution threw").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WhenDependencyResolverReturnsFailure_PreservesError()
    {
        var job = CreateJob("job11", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector();
        var resolver = new FakeResolver();
        resolver.OnBuild = (snap, tables) => Result<ExecutionPlan>.Fail("missing parent");

        var svc = new SchemaService(inspector, new FakeMapper(), resolver);

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Dependency resolution failed").And.Contain("missing parent").And.Contain(job.Name);
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithAllStepsSucceeding_ReturnsSuccess()
    {
        var job = CreateJob("job12", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new FakeInspector();
        var mapper = new FakeMapper();
        var resolver = new FakeResolver();
        // Return a non-empty execution plan to satisfy assertion
        resolver.OnBuild = (snap, tables) => Result<ExecutionPlan>.Ok(
            new ExecutionPlan(new[] { TableIdentifier.Parse("dbo.T") }, new[] { TableIdentifier.Parse("dbo.T") }));

        var svc = new SchemaService(inspector, mapper, resolver);

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SourceSnapshot.Should().NotBeNull();
        result.Value.TargetSnapshot.Should().NotBeNull();
        result.Value.Mapping.Success.Should().BeTrue();
        result.Value.ExecutionPlan.InsertOrder.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeJobAsync_CallsSourceInspectorWithCorrectLevel_ExistenceOnly()
    {
        var job = CreateJob("job13", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new RecordingFakeInspector();
        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        inspector.CallCount.Should().BeGreaterThanOrEqualTo(1);
        inspector.Requests.Should().ContainSingle(r => r.Role == SchemaRole.Source && r.Level == SchemaInspectionLevel.ExistenceOnly);
    }

    [Fact]
    public async Task AnalyzeJobAsync_CallsTargetInspectorWithCorrectLevel_Full()
    {
        var job = CreateJob("job14", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new RecordingFakeInspector();
        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        inspector.Requests.Should().ContainSingle(r => r.Role == SchemaRole.Target && r.Level == SchemaInspectionLevel.Full);
    }

    [Fact]
    public async Task AnalyzeJobAsync_FiltersDisabledTables_OnlyInspectsEnabled()
    {
        var job = CreateJob("job15", "src", "tgt",
            Table("dbo.S1", "dbo.T1", enabled: true),
            Table("dbo.S2", "dbo.T2", enabled: true),
            Table("dbo.S3", "dbo.T3", enabled: false));

        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new RecordingFakeInspector();
        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Both source and target requests should contain only two tables (enabled ones)
        var sourceReq = inspector.Requests.First(r => r.Role == SchemaRole.Source);
        var targetReq = inspector.Requests.First(r => r.Role == SchemaRole.Target);
        sourceReq.Tables.Count.Should().Be(2);
        targetReq.Tables.Count.Should().Be(2);
    }

    [Fact]
    public async Task AnalyzeJobAsync_DeduplicatesTableIdentifiers_WhenMultipleTasks()
    {
        var job = CreateJob("job16", "src", "tgt",
            Table("dbo.S1", "dbo.T", enabled: true),
            Table("dbo.S2", "dbo.T", enabled: true));

        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        var inspector = new RecordingFakeInspector();
        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        var result = await svc.AnalyzeJobAsync(cfg, job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var targetReq = inspector.Requests.First(r => r.Role == SchemaRole.Target);
        // should only contain the single unique target table
        targetReq.Tables.Count.Should().Be(1);
        targetReq.Tables.First().ToString().Should().Be("dbo.T");
    }

    [Fact]
    public async Task AnalyzeJobAsync_WithCancellationToken_PropagatesToken()
    {
        var job = CreateJob("job17", "src", "tgt", Table("dbo.S", "dbo.T", enabled: true));
        var cfg = CreateConfig(new[] { Conn("src"), Conn("tgt") }, new[] { job });

        CancellationToken? seenToken = null;
        var inspector = new FakeInspector();
        inspector.OnInspect = (req, ct) =>
        {
            seenToken = ct;
            return Task.FromResult(new SchemaSnapshot(Enumerable.Empty<TableSchema>(), req.Role, req.Level));
        };

        var svc = new SchemaService(inspector, new FakeMapper(), new FakeResolver());

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var result = await svc.AnalyzeJobAsync(cfg, job, token);

        result.IsSuccess.Should().BeTrue();
        seenToken.Should().NotBeNull();
        seenToken.Value.Should().Be(token);
    }

    // RecordingFakeInspector extracted to Tests/ReportSyncer.Core.Tests/Helpers/TestDoubles/RecordingFakeInspector.cs
}
