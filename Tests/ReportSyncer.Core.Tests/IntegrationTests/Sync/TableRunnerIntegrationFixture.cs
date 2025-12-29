using DotNetToolkit.Database.Abstractions;
using DotNetToolkit.Database.Configuration;
using DotNetToolkit.Database.Internal;
using DotNetToolkit.Database.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReportSyncer.Core.Tests.IntegrationTests.Common;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sync;

[CollectionDefinition("TableRunnerIntegrationTests", DisableParallelization = true)]
public sealed class TableRunnerIntegrationCollection : ICollectionFixture<TableRunnerIntegrationFixture> { }

/// <summary>
/// Creates a dedicated database with both source and target tables for TableRunner integration tests.
/// </summary>
public sealed class TableRunnerIntegrationFixture : IAsyncLifetime
{
    private const string DatabaseName = "ReportSyncer_TableRunnerDb";
    public string? ConnectionString { get; private set; }
    public string? SkipReason { get; private set; }

    private string? _baseConnectionString;

    public async Task InitializeAsync()
    {
        _baseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection(
            "REPORTSYNCER_TEST_SQL_CONN",
            "TableRunnerIntegration",
            out var skipReason);

        if (_baseConnectionString is null)
        {
            SkipReason = skipReason;
            return;
        }

        try
        {
            await ExecuteSeedAsync(_baseConnectionString);
            ConnectionString = $"{_baseConnectionString};Initial Catalog={DatabaseName}";
        }
        catch (Exception ex)
        {
            SkipReason = $"TableRunner integration skipped: {ex.Message}";
            Console.WriteLine($"[TableRunnerIntegration] {SkipReason}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_baseConnectionString is null) return;
        await SqlIntegrationTestHelper.DropDatabaseAsync(_baseConnectionString, DatabaseName);
        SqlConnection.ClearAllPools();
    }

    public IDbConnectionFactory CreateConnectionFactory()
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var settings = Options.Create(new DatabaseSettings
        {
            ProviderName = "Microsoft.Data.SqlClient",
            ConnectionString = ConnectionString,
            CommandTimeoutSeconds = 30
        });

        return new DbConnectionFactory(settings);
    }

    public Func<string, IDbContext> CreateDbContextFactory()
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var connectionFactory = CreateConnectionFactory();
        var settings = Options.Create(new DatabaseSettings
        {
            ProviderName = "Microsoft.Data.SqlClient",
            ConnectionString = ConnectionString,
            CommandTimeoutSeconds = 30
        });

        var services = new ServiceCollection()
            .AddSingleton<IDbConnectionFactory>(connectionFactory)
            .AddTransient(typeof(IDataMapper<>), typeof(ReflectionDataMapper<>))
            .AddSingleton<ILogger<DbContext>>(NullLogger<DbContext>.Instance)
            .BuildServiceProvider();

        return _ => new DbContext(connectionFactory, settings, NullLogger<DbContext>.Instance, services);
    }

    public async Task ResetDataAsync()
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        const string sql = """
                           DELETE FROM rpt.TargetEvents;
                           DBCC CHECKIDENT ('rpt.TargetEvents', RESEED, 0);
                           INSERT INTO rpt.TargetEvents(CustomerId, EventTime, Payload)
                           VALUES
                           (1, '2025-11-01', 'old-1'),
                           (1, '2025-11-02', 'old-2'),
                           (2, '2025-12-20', 'keep-2');
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountAsync(string sql)
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<(int CustomerId, int EventId, string Payload)>> LoadTargetAsync(int customerId)
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var sql = "SELECT CustomerId, EventId, Payload FROM rpt.TargetEvents WHERE CustomerId = @cid ORDER BY EventId;";
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", customerId);

        var results = new List<(int, int, string)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2)));
        }

        return results;
    }

    private static async Task ExecuteSeedAsync(string baseConn)
    {
        var masterConn = $"{baseConn};Initial Catalog=master";
        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("IntegrationTests", "Sync", "TableRunnerSeed.sql"),
            "TableRunnerSeed.sql",
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Sync", "TableRunnerSeed.sql"))
            ?? throw new InvalidOperationException("TableRunnerSeed.sql not found in output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync(masterConn, seedPath);
    }
}
