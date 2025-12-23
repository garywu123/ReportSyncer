using DotNetToolkit.Database.Abstractions;
using DotNetToolkit.Database.Configuration;
using DotNetToolkit.Database.Internal;
using DotNetToolkit.Database.Services;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ReportSyncer.Core.Tests.IntegrationTests.Schema;

[CollectionDefinition("SchemaIntegrationTests", DisableParallelization = true)]
public sealed class SchemaIntegrationCollection : ICollectionFixture<SchemaIntegrationDatabaseFixture> { }

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SchemaIntegrationDatabaseFixture : IAsyncLifetime
{
    public string? SkipReason       { get; private set; }
    public string? ConnectionString { get; private set; }
    // ReSharper disable once MemberCanBePrivate.Global
    public string? DatabaseName     { get; private set; }

    public async Task InitializeAsync()
    {
        var baseConn = Environment.GetEnvironmentVariable("REPORTSYNCER_TEST_SQL_CONN");

        Console.WriteLine($"[DEBUG] Connection String Source: {baseConn ?? "NULL"}");
        Console.WriteLine($"[DEBUG] Process ID: {Environment.ProcessId}");

        if (string.IsNullOrWhiteSpace(baseConn))
        {
            SkipReason = "Set REPORTSYNCER_TEST_SQL_CONN to run schema integration tests.";
            return;
        }

        DatabaseName = $"ReportSyncer_Schema_IT_{Guid.NewGuid():N}";
        var masterConn = $"{baseConn};Initial Catalog=master";
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();
        await using var create = new SqlCommand($"CREATE DATABASE [{DatabaseName}];", conn);
        await create.ExecuteNonQueryAsync();
        ConnectionString = $"{baseConn};Initial Catalog={DatabaseName}";

        // Execute the schema seed script (runs once per fixture instance)
        var seedPath = FindSeedScriptPath() ?? throw new InvalidOperationException("Schema seed script not found in output. Ensure SchemaSeed.sql is copied to test output.");
        await ExecuteSqlScriptAsync(seedPath);
    }

    private string? FindSeedScriptPath()
    {
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(baseDir, "IntegrationTests", "Schema", "SchemaSeed.sql"),
            Path.Combine(baseDir, "SchemaSeed.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Schema", "SchemaSeed.sql")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task ExecuteSqlScriptAsync(string path)
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
        var sql = await File.ReadAllTextAsync(path);

        // Split batches on GO lines
        var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        foreach (var batch in batches)
        {
            var text = batch?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            await using var cmd = new SqlCommand(text, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName)) return;
        var masterConn = ConnectionString?.Replace($"Initial Catalog={DatabaseName}", "Initial Catalog=master");
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();
        await using var drop = new SqlCommand(
            $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];", conn);
        await drop.ExecuteNonQueryAsync();
        SqlConnection.ClearAllPools();
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