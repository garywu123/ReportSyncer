using Microsoft.Data.SqlClient;
using ReportSyncer.Core.Tests.IntegrationTests.Common;
using System.IO;
using Xunit;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sql;

[CollectionDefinition("SqlQueryBuilderIntegrationTests", DisableParallelization = true)]
public sealed class SqlQueryBuilderIntegrationCollection : ICollectionFixture<SqlQueryBuilderIntegrationDatabaseFixture> { }

public sealed class SqlQueryBuilderIntegrationDatabaseFixture : IAsyncLifetime
{
    public string? SkipReason { get; private set; }
    public string? BaseConnectionString { get; private set; }
    public string? SourceConnectionString { get; private set; }
    public string? TargetConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        BaseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection("REPORTSYNCER_TEST_SQL_CONN", "SqlQueryBuilderIntegration", out var skipReason);
        if (BaseConnectionString is null)
        {
            SkipReason = skipReason;
            return;
        }

        try
        {
            await CreateDatabasesAsync(BaseConnectionString);
            SourceConnectionString = $"{BaseConnectionString};Initial Catalog=ReportSyncer_SourceApp";
            TargetConnectionString = $"{BaseConnectionString};Initial Catalog=ReportSyncer_TargetRpt";
        }
        catch (Exception ex)
        {
            SkipReason = $"SQL integration skipped: {ex.Message}";
        }
    }

    private async Task CreateDatabasesAsync(string baseConn)
    {
        await using var conn = new SqlConnection($"{baseConn};Initial Catalog=master");
        await conn.OpenAsync();

        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("IntegrationTests", "Sql", "QueryBuilderSeed.sql"),
            "QueryBuilderSeed.sql",
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Sql", "QueryBuilderSeed.sql"))
            ?? throw new InvalidOperationException("QueryBuilderSeed.sql not found in output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync($"{baseConn};Initial Catalog=master", seedPath);
    }

    public async Task DisposeAsync()
    {
        if (BaseConnectionString is null) return;

        await DropDatabaseAsync("ReportSyncer_TargetRpt");
        await DropDatabaseAsync("ReportSyncer_SourceApp");
        SqlConnection.ClearAllPools();
    }

    private async Task DropDatabaseAsync(string name)
    {
        if (BaseConnectionString is null) return;
        await SqlIntegrationTestHelper.DropDatabaseAsync(BaseConnectionString, name);
    }
}
