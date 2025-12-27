using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using Xunit;

namespace ReportSyncer.Core.Tests.IntegrationTests.Observability;

[CollectionDefinition("WorkEstimatorIntegrationTests", DisableParallelization = true)]
public sealed class WorkEstimatorIntegrationCollection : ICollectionFixture<WorkEstimatorIntegrationDatabaseFixture> { }

public sealed class WorkEstimatorIntegrationDatabaseFixture : IAsyncLifetime
{
    public string? SkipReason { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        var baseConn = Environment.GetEnvironmentVariable("REPORTSYNCER_TEST_SQL_CONN");
        if (string.IsNullOrWhiteSpace(baseConn))
        {
            SkipReason = "Set REPORTSYNCER_TEST_SQL_CONN to run WorkEstimator integration tests.";
            Console.WriteLine($"[WorkEstimatorIntegration] {SkipReason}");
            return;
        }

        try
        {
            await ExecuteSeedAsync(baseConn);
            ConnectionString = $"{baseConn};Initial Catalog=ReportSyncer_EstimatorDb";
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
        var seedPath = FindSeedScriptPath() ?? throw new InvalidOperationException("WorkEstimatorSeed.sql not found in output.");
        var sql = await File.ReadAllTextAsync(seedPath);
        var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        foreach (var batch in batches)
        {
            var text = batch?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            try
            {
                await using var cmd = new SqlCommand(text, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed executing seed batch: {text}", ex);
            }
        }
    }

    private static string? FindSeedScriptPath()
    {
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(baseDir, "IntegrationTests", "Observability", "WorkEstimatorSeed.sql"),
            Path.Combine(baseDir, "WorkEstimatorSeed.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Observability", "WorkEstimatorSeed.sql")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task DisposeAsync()
    {
        if (ConnectionString is null) return;

        await using var conn = new SqlConnection(ConnectionString.Replace("Initial Catalog=ReportSyncer_EstimatorDb", "Initial Catalog=master"));
        await conn.OpenAsync();
        var drop = """
                   IF DB_ID('ReportSyncer_EstimatorDb') IS NOT NULL
                   BEGIN
                       ALTER DATABASE [ReportSyncer_EstimatorDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                       DROP DATABASE [ReportSyncer_EstimatorDb];
                   END;
                   """;
        await using var cmd = new SqlCommand(drop, conn);
        await cmd.ExecuteNonQueryAsync();
        SqlConnection.ClearAllPools();
    }
}
