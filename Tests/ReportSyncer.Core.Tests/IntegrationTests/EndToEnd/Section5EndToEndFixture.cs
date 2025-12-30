using DotNetToolkit.Database.Abstractions;
using DotNetToolkit.Database.Configuration;
using DotNetToolkit.Database.Internal;
using DotNetToolkit.Database.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.IntegrationTests.Common;

namespace ReportSyncer.Core.Tests.IntegrationTests.EndToEnd;

[CollectionDefinition("Section5EndToEnd", DisableParallelization = true)]
public sealed class Section5EndToEndCollection : ICollectionFixture<Section5EndToEndFixture> { }

/// <summary>
/// Creates a dedicated SQL Server database for Section 5 end-to-end integration tests and
/// provides helpers for reseeding and basic queries.
/// </summary>
public sealed class Section5EndToEndFixture : IAsyncLifetime
{
    private const string DatabaseName = "ReportSyncerTest";

    public string? ConnectionString { get; private set; }
    public string? SkipReason { get; private set; }

    private string? _baseConnectionString;

    public async Task InitializeAsync()
    {
        _baseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection(
            "REPORTSYNCER_TEST_SQL_CONN",
            "Section5EndToEnd",
            out var skipReason);

        if (_baseConnectionString is null)
        {
            SkipReason = skipReason;
            return;
        }

        try
        {
            await SqlIntegrationTestHelper.DropDatabaseAsync(_baseConnectionString, DatabaseName);
            await SqlIntegrationTestHelper.CreateDatabaseAsync(_baseConnectionString, DatabaseName);
            ConnectionString = $"{_baseConnectionString};Initial Catalog={DatabaseName}";
            await ResetAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Section5EndToEnd skipped: {ex.Message}";
            Console.WriteLine($"[Section5EndToEnd] {SkipReason}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_baseConnectionString is null) return;
        await SqlIntegrationTestHelper.DropDatabaseAsync(_baseConnectionString, DatabaseName);
        SqlConnection.ClearAllPools();
    }

    public async Task ResetAsync()
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("TestFiles", "Section5EndToEnd", "section5_endtoend_schema_seed.sql"),
            Path.Combine("TestFiles", "Section5EndToEnd", "section5_endtoend_schema_seed.sql"))
            ?? throw new InvalidOperationException("Section5 seed script not found in output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync(ConnectionString, seedPath);
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

    public Func<ConnectionConfig, IDbContext> CreateInspectorContextFactory()
    {
        return connection =>
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            var settings = Options.Create(new DatabaseSettings
            {
                ProviderName = "Microsoft.Data.SqlClient",
                ConnectionString = connection.ConnectionString,
                CommandTimeoutSeconds = 30
            });

            var connectionFactory = new DbConnectionFactory(settings);
            var services = new ServiceCollection()
                .AddSingleton<IDbConnectionFactory>(connectionFactory)
                .AddTransient(typeof(IDataMapper<>), typeof(ReflectionDataMapper<>))
                .AddSingleton<ILogger<DbContext>>(NullLogger<DbContext>.Instance)
                .BuildServiceProvider();

            return new DbContext(connectionFactory, settings, NullLogger<DbContext>.Instance, services);
        };
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

    public async Task<List<string>> QueryStringsAsync(string sql)
    {
        if (ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var results = new List<string>();
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
