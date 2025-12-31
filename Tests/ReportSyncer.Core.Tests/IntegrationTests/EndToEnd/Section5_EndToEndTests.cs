using FluentAssertions;
using System.Linq;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Tests.Helpers.Observability;
using Xunit;

namespace ReportSyncer.Core.Tests.IntegrationTests.EndToEnd;

[Collection("Section5EndToEnd")]
public sealed class Section5_EndToEndTests
{
    private readonly Section5EndToEndFixture _fixture;

    public Section5_EndToEndTests(Section5EndToEndFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task DryRun_DoesNotChangeTarget()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it1_dryrun_parent_child.yaml");

        var beforeParent = await _fixture.CountAsync("SELECT COUNT(1) FROM rs_tgt.Parent");
        var beforeChild = await _fixture.CountAsync("SELECT COUNT(1) FROM rs_tgt.Child");
        var beforeIdentity = await _fixture.CountAsync("SELECT COUNT(1) FROM rs_tgt.IdentityDemo");

        var result = await services.Orchestrator.RunJobAsync(path, "it1_dryrun_parent_child", null, CancellationToken.None);

        result.Status.Should().Be(JobStatus.SkippedDryRun);
        result.DryRun.Should().BeTrue();
        var afterParent = await _fixture.CountAsync("SELECT COUNT(1) FROM rs_tgt.Parent");
        var afterChild = await _fixture.CountAsync("SELECT COUNT(1) FROM rs_tgt.Child");
        var afterIdentity = await _fixture.CountAsync("SELECT COUNT(1) FROM rs_tgt.IdentityDemo");
        afterParent.Should().Be(beforeParent);
        afterChild.Should().Be(beforeChild);
        afterIdentity.Should().Be(beforeIdentity);
        AssertProgressCompleted(services.Progress, dryRun: true);
    }

    [SkippableFact]
    public async Task Overwrite_RunTwice_IsIdempotentForFilteredParentAndChild()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it2_overwrite_parent_child.yaml");

        async Task ExecuteAsync() =>
            await services.Orchestrator.RunJobAsync(path, "it2_overwrite_parent_child", null, CancellationToken.None);

        await ExecuteAsync();
        await ExecuteAsync(); // idempotent second run

        var parentName = (await _fixture.QueryStringsAsync("SELECT Name FROM rs_tgt.Parent WHERE BizKey = 'P-1'")).Single();
        parentName.Should().Be("New Parent 1");

        var childLines = await _fixture.QueryStringsAsync("SELECT LineCode FROM rs_tgt.Child WHERE ParentId = 1 ORDER BY LineCode");
        childLines.Should().BeEquivalentTo(new[] { "NEW-L1", "NEW-L2" }, opts => opts.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task IdentityInsert_On_AllowsExplicitIds_Off_Fails()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it4_identity_insert_on_off.yaml");

        var result = await services.Orchestrator.RunJobAsync(path, "it4_identity_insert_on", null, CancellationToken.None);
        result.Status.Should().Be(JobStatus.Succeeded);

        var ids = await _fixture.QueryStringsAsync("SELECT CAST(Id AS NVARCHAR(10)) FROM rs_tgt.IdentityDemo ORDER BY Id");
        ids.Should().Contain("1000").And.Contain("1001");

        // Reset to validate OFF path
        await _fixture.ResetAsync();
        services = Section5TestServiceFactory.Create(_fixture);

        var action = async () => await services.Orchestrator.RunJobAsync(path, "it4_identity_insert_off", null, CancellationToken.None);
        await action.Should().ThrowAsync<Exception>(); // identity insert off should raise
    }

    private static void AssertProgressCompleted(InMemoryProgressReporter progress, bool dryRun)
    {
        progress.JobEvents.Should().NotBeEmpty();
        progress.JobEvents.Last().Kind.Should().Be(ProgressEventKind.Completed);
        progress.JobEvents.Last().Phase.Should().Be(ProgressPhase.Execution);
    }

    [SkippableFact]
    public async Task Safety_FilterRequired_BlocksJob()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it5_safety_filter_required.yaml");

        var act = async () => await services.Orchestrator.RunJobAsync(path, "it5_safety_filter_required", null, CancellationToken.None);
        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*allowAllDelete is false*");
    }

    [SkippableFact]
    public async Task LargeDelete_NoFilter_BlocksJob()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it6_large_delete_block.yaml");

        var act = async () => await services.Orchestrator.RunJobAsync(path, "it6_large_delete_block", null, CancellationToken.None);
        await act.Should().ThrowAsync<SchemaMismatchException>();
    }

    [SkippableFact]
    public async Task IgnoreDependencies_True_FailsPreflightForMissingParent()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it7_ignore_deps_true.yaml");

        var act = async () => await services.Orchestrator.RunJobAsync(path, "it7_ignore_dependencies_true", null, CancellationToken.None);
        await act.Should().ThrowAsync<SchemaMismatchException>();
    }

    [SkippableFact]
    public async Task IgnoreDependencies_False_FailsPreflight()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("it8_ignore_deps_false.yaml");

        var act = async () => await services.Orchestrator.RunJobAsync(path, "it8_ignore_dependencies_false", null, CancellationToken.None);
        await act.Should().ThrowAsync<SchemaMismatchException>();
    }

    [SkippableFact]
    public async Task Extra_DateRangeOnly_SucceedsWithNoChanges()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("extra_date_range_only.yaml");

        var result = await services.Orchestrator.RunJobAsync(path, "extra_date_range_only", null, CancellationToken.None);
        result.Status.Should().Be(JobStatus.Succeeded);
    }

    [SkippableFact]
    public async Task Extra_KeyFilterOnly_Succeeds()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("extra_key_filter_only.yaml");

        var result = await services.Orchestrator.RunJobAsync(path, "extra_key_filter_only", null, CancellationToken.None);
        result.Status.Should().Be(JobStatus.Succeeded);
    }

    [SkippableFact]
    public async Task Extra_DateAndKeyFilter_Succeeds()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("extra_date_and_key_filter.yaml");

        var result = await services.Orchestrator.RunJobAsync(path, "extra_date_and_key_filter", null, CancellationToken.None);
        result.Status.Should().Be(JobStatus.Succeeded);
    }

    [SkippableFact]
    public async Task Extra_AdvancedMapping_Succeeds()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("extra_advanced_mapping.yaml");

        var result = await services.Orchestrator.RunJobAsync(path, "extra_advanced_mapping", null, CancellationToken.None);
        result.Status.Should().Be(JobStatus.Succeeded);
    }

    [SkippableFact]
    public async Task Extra_PreSyncToggle_RunsWithoutConflict()
    {
        Skip.If(!string.IsNullOrEmpty(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetAsync();

        var services = Section5TestServiceFactory.Create(_fixture);
        var path = CreateConfigWithFixtureConnection("extra_presync_toggle.yaml");

        var result = await services.Orchestrator.RunJobAsync(path, "extra_presync_toggle", null, CancellationToken.None);
        result.Status.Should().Be(JobStatus.Succeeded);
    }

    private string CreateConfigWithFixtureConnection(string fileName)
    {
        if (string.IsNullOrWhiteSpace(_fixture.ConnectionString))
            throw new InvalidOperationException("Fixture not initialized with a connection string.");

        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Section5EndToEnd", fileName);
        var text = File.ReadAllText(sourcePath);
        var updated = text.Replace("Server=(localdb)\\MSSQLLocalDB;Initial Catalog=ReportSyncerTest;Integrated Security=True;", _fixture.ConnectionString);

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}.yaml");
        File.WriteAllText(tempPath, updated);
        return tempPath;
    }
}
