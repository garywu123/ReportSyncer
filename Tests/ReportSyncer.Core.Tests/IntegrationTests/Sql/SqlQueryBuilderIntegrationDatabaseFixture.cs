using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
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
        var baseConn = Environment.GetEnvironmentVariable("REPORTSYNCER_TEST_SQL_CONN")
                       ?? "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True";

        try
        {
            await CreateDatabasesAsync(baseConn);
            BaseConnectionString = baseConn;
            SourceConnectionString = $"{baseConn};Initial Catalog=ReportSyncer_SourceApp";
            TargetConnectionString = $"{baseConn};Initial Catalog=ReportSyncer_TargetRpt";
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

        var seedPath = FindSeedScriptPath() ?? throw new InvalidOperationException("QueryBuilderSeed.sql not found in output.");
        var sql = await File.ReadAllTextAsync(seedPath);
        var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        foreach (var batch in batches)
        {
            var text = batch?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            await using var cmd = new SqlCommand(text, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string? FindSeedScriptPath()
    {
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(baseDir, "IntegrationTests", "Sql", "QueryBuilderSeed.sql"),
            Path.Combine(baseDir, "QueryBuilderSeed.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Sql", "QueryBuilderSeed.sql")
        };

        return candidates.FirstOrDefault(File.Exists);
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
        await using var conn = new SqlConnection($"{BaseConnectionString};Initial Catalog=master");
        await conn.OpenAsync();
        var sql = $"""
                   IF DB_ID('{name}') IS NOT NULL
                   BEGIN
                       ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                       DROP DATABASE [{name}];
                   END;
                   """;
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
