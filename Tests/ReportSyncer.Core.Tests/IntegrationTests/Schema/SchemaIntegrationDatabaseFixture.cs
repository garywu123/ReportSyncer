using DotNetToolkit.Database.Abstractions;
using DotNetToolkit.Database.Configuration;
using DotNetToolkit.Database.Internal;
using DotNetToolkit.Database.Services;
using Microsoft.Data.SqlClient;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReportSyncer.Core.Tests.IntegrationTests.Common;

namespace ReportSyncer.Core.Tests.IntegrationTests.Schema;

[CollectionDefinition("SchemaIntegrationTests", DisableParallelization = true)]
public sealed class SchemaIntegrationCollection : ICollectionFixture<SchemaIntegrationDatabaseFixture> { }

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SchemaIntegrationDatabaseFixture : IAsyncLifetime
{
    public string? SkipReason { get; private set; }
    public string? ConnectionString { get; private set; }
    public string? DatabaseName { get; private set; }
    private string? _baseConnectionString;

    public async Task InitializeAsync()
    {
        _baseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection("REPORTSYNCER_TEST_SQL_CONN", "SchemaIntegration", out var skipReason);
        if (_baseConnectionString is null)
        {
            SkipReason = skipReason;
            return;
        }

        DatabaseName = $"ReportSyncer_Schema_IT_{Guid.NewGuid():N}";
        await SqlIntegrationTestHelper.CreateDatabaseAsync(_baseConnectionString, DatabaseName);
        ConnectionString = $"{_baseConnectionString};Initial Catalog={DatabaseName}";

        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("IntegrationTests", "Schema", "SchemaSeed.sql"),
            "SchemaSeed.sql",
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Schema", "SchemaSeed.sql"))
            ?? throw new InvalidOperationException("Schema seed script not found in output. Ensure SchemaSeed.sql is copied to test output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync(ConnectionString, seedPath);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName)) return;
        if (_baseConnectionString is null) return;
        await SqlIntegrationTestHelper.DropDatabaseAsync(_baseConnectionString, DatabaseName);
    }

    public async Task ExecuteNonQueryAsync(string sql)
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public Func<object, IDbContext> CreateDbContextFactory()
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
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
           .AddSingleton<ILogger<DbContext>>(NullLogger<DbContext>.Instance);
            
        var sp = services.BuildServiceProvider();

        return _ => new DbContext(factory, settings, NullLogger<DbContext>.Instance, sp);
    }
}
