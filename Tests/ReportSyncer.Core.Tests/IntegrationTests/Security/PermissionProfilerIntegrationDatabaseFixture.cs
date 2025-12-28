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

namespace ReportSyncer.Core.Tests.IntegrationTests.Security;

[CollectionDefinition("PermissionProfilerIntegrationTests", DisableParallelization = true)]
public sealed class PermissionProfilerIntegrationCollection : ICollectionFixture<PermissionProfilerIntegrationDatabaseFixture> { }

public sealed class PermissionProfilerIntegrationDatabaseFixture : IAsyncLifetime
{
    private const string DatabaseName = "ReportSyncer_PermTest";
    private const string AdminUser = "rs_admin";
    private const string WriterUser = "rs_writer";
    private const string DefaultPassword = "P@ssw0rd_123!Secure";

    public string? SkipReason { get; private set; }

    public string? AdminConnectionString { get; private set; }

    public string? WriterConnectionString { get; private set; }

    private string? _baseConnectionString;

    public async Task InitializeAsync()
    {
        _baseConnectionString = SqlIntegrationTestHelper.RequireBaseConnection(
            "REPORTSYNCER_TEST_SQL_CONN",
            "PermissionProfilerIntegration",
            out var skipReason);

        if (_baseConnectionString is null)
        {
            SkipReason = skipReason;
            return;
        }

        try
        {
            await ExecuteSeedAsync(_baseConnectionString);
            AdminConnectionString = BuildUserConnection(_baseConnectionString, AdminUser, DefaultPassword);
            WriterConnectionString = BuildUserConnection(_baseConnectionString, WriterUser, DefaultPassword);
        }
        catch (Exception ex)
        {
            SkipReason = $"Permission profiler integration skipped: {ex.Message}";
            Console.WriteLine($"[PermissionProfilerIntegration] {SkipReason}");
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
        if (AdminConnectionString is null || WriterConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        return name => name switch
        {
            "Admin" => CreateDbContext(AdminConnectionString),
            "Writer" => CreateDbContext(WriterConnectionString),
            _ => throw new InvalidOperationException($"Unknown connection name '{name}'.")
        };
    }

    private static async Task ExecuteSeedAsync(string baseConn)
    {
        var masterConn = $"{baseConn};Initial Catalog=master";
        var seedPath = SqlIntegrationTestHelper.FindSeedScript(
            AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(),
            Path.Combine("IntegrationTests", "Security", "PermissionProfilerSeed.sql"),
            "PermissionProfilerSeed.sql",
            Path.Combine(Directory.GetCurrentDirectory(), "IntegrationTests", "Security", "PermissionProfilerSeed.sql"))
            ?? throw new InvalidOperationException("PermissionProfilerSeed.sql not found in output.");

        await SqlIntegrationTestHelper.ExecuteScriptAsync(masterConn, seedPath);
    }

    private static string BuildUserConnection(string baseConn, string user, string password)
    {
        var builder = new SqlConnectionStringBuilder(baseConn)
        {
            InitialCatalog = DatabaseName,
            UserID = user,
            Password = password,
            IntegratedSecurity = false
        };

        return builder.ConnectionString;
    }

    private static IDbContext CreateDbContext(string connectionString)
    {
        var settings = Options.Create(new DatabaseSettings
        {
            ProviderName = "Microsoft.Data.SqlClient",
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 30
        });

        var factory = new DbConnectionFactory(settings);

        var services = new ServiceCollection()
            .AddSingleton<IDbConnectionFactory>(factory)
            .AddTransient(typeof(IDataMapper<>), typeof(ReflectionDataMapper<>))
            .AddSingleton<ILogger<DbContext>>(NullLogger<DbContext>.Instance)
            .BuildServiceProvider();

        return new DbContext(factory, settings, NullLogger<DbContext>.Instance, services);
    }
}
