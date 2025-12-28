using Microsoft.Data.SqlClient;
using ReportSyncer.Core.Tests.IntegrationTests.Common;
using System.IO;
using Xunit;

namespace ReportSyncer.Core.Tests.IntegrationTests.Observability;

[CollectionDefinition("WorkEstimatorIntegrationTests", DisableParallelization = true)]
public sealed class WorkEstimatorIntegrationCollection : ICollectionFixture<WorkEstimatorIntegrationDatabaseFixture> { }

public sealed class WorkEstimatorIntegrationDatabaseFixture : IAsyncLifetime
{
    public string? SkipReason { get; private set; }
    public string? ConnectionString { get; private set; }
    private string? _baseConnectionString;

    public async Task InitializeAsync()
    {
        _baseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection("REPORTSYNCER_TEST_SQL_CONN", "WorkEstimatorIntegration", out var skipReason);
        if (_baseConnectionString is null)
        {
            SkipReason = skipReason;
            return;
        }

        try
        {
            await ExecuteSeedAsync(_baseConnectionString);
            ConnectionString = $"{_baseConnectionString};Initial Catalog=ReportSyncer_EstimatorDb";
        }
        catch (Exception ex)
        {
            SkipReason = $"Estimator integration skipped: {ex.Message}";
            Console.WriteLine($"[WorkEstimatorIntegration] {SkipReason}");
        }
    }

    private static async Task ExecuteSeedAsync(string baseConn)
    {
        await using var conn = new SqlConnection($"{baseConn};Initial Catalog=master");
        await conn.OpenAsync();
        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("IntegrationTests", "Observability", "WorkEstimatorSeed.sql"),
            "WorkEstimatorSeed.sql",
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Observability", "WorkEstimatorSeed.sql"))
            ?? throw new InvalidOperationException("WorkEstimatorSeed.sql not found in output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync($"{baseConn};Initial Catalog=master", seedPath);
    }

    public async Task DisposeAsync()
    {
        if (ConnectionString is null) return;
        if (_baseConnectionString is null) return;
        await SqlIntegrationTestHelper.DropDatabaseAsync(_baseConnectionString, "ReportSyncer_EstimatorDb");
        SqlConnection.ClearAllPools();
    }
}
