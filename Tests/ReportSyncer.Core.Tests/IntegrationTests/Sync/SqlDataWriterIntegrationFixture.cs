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

[CollectionDefinition("SqlDataWriterIntegrationTests", DisableParallelization = true)]
public sealed class SqlDataWriterIntegrationCollection : ICollectionFixture<SqlDataWriterIntegrationFixture> { }

public sealed class SqlDataWriterIntegrationFixture : IAsyncLifetime
{
    private const string DatabaseName = "ReportSyncer_DataWriterDb";
    public string? ConnectionString { get; private set; }
    public string? SkipReason { get; private set; }
    private string? _baseConnectionString;

    public async Task InitializeAsync()
    {
        _baseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection(
            "REPORTSYNCER_TEST_SQL_CONN",
            "SqlDataWriterIntegration",
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
            SkipReason = $"SqlDataWriter integration skipped: {ex.Message}";
            Console.WriteLine($"[SqlDataWriterIntegration] {SkipReason}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_baseConnectionString is null) return;
        await SqlIntegrationTestHelper.DropDatabaseAsync(_baseConnectionString, DatabaseName);
        SqlConnection.ClearAllPools();
    }

    public Func<string, IDbContext> CreateDbContextFactory()
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var settings = Options.Create(new DatabaseSettings
        {
            ProviderName = "Microsoft.Data.SqlClient",
            ConnectionString = ConnectionString,
            CommandTimeoutSeconds = 30
        });

        var factory = new DbConnectionFactory(settings);

        var services = new ServiceCollection()
            .AddSingleton<IDbConnectionFactory>(factory)
            .AddTransient(typeof(IDataMapper<>), typeof(ReflectionDataMapper<>))
            .AddSingleton<ILogger<DbContext>>(NullLogger<DbContext>.Instance)
            .BuildServiceProvider();

        return _ => new DbContext(factory, settings, NullLogger<DbContext>.Instance, services);
    }

    public async Task<int> ExecuteScalarAsync(string sql)
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task ResetTableAsync()
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
        var sql = """
                  DELETE FROM rpt.TargetEvents;
                  DBCC CHECKIDENT ('rpt.TargetEvents', RESEED, 0);
                  INSERT INTO rpt.TargetEvents(CustomerId, EventTime, Payload)
                  VALUES
                  (1, '2025-12-20', 'a'),
                  (1, '2025-12-21', 'b'),
                  (1, '2025-12-22', 'c'),
                  (2, '2025-12-20', 'd'),
                  (2, '2025-12-21', 'e');
                  """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteSeedAsync(string baseConn)
    {
        var masterConn = $"{baseConn};Initial Catalog=master";
        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("IntegrationTests", "Sync", "SqlDataWriterSeed.sql"),
            "SqlDataWriterSeed.sql",
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Sync", "SqlDataWriterSeed.sql"))
            ?? throw new InvalidOperationException("SqlDataWriterSeed.sql not found in output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync(masterConn, seedPath);
    }
}
