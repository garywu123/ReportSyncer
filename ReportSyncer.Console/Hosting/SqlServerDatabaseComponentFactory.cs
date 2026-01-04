// ============================================================================
// File: DotNetToolkitDatabaseComponentFactory.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Implementation of IDatabaseComponentFactory using DotNetToolkit.Database components.
// ============================================================================

using DotNetToolkit.Database.Abstractions;
using DotNetToolkit.Database.Configuration;
using DotNetToolkit.Database.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Creates database components using DotNetToolkit.Database infrastructure.
/// </summary>
/// <remarks>
/// This factory encapsulates the creation logic for IDbContext and IDbConnectionFactory
/// using the DotNetToolkit.Database library. It configures components to use Microsoft.Data.SqlClient
/// as the database provider.
/// </remarks>
public sealed class SqlServerDatabaseComponentFactory : IDatabaseComponentFactory
{
    private const string SqlServerProviderName = "Microsoft.Data.SqlClient";
    
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerDatabaseComponentFactory"/> class.
    /// </summary>
    /// <param name="services">
    /// The service provider used to resolve optional dependencies like ILogger.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> is null.
    /// </exception>
    public SqlServerDatabaseComponentFactory(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc/>
    public IDbConnectionFactory CreateConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var settings = new DatabaseSettings
        {
            ProviderName = SqlServerProviderName,
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 30
        };

        var options = Options.Create(settings);
        return new DbConnectionFactory(options);
    }

    /// <inheritdoc/>
    public IDbContext CreateDbContext(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var settings = new DatabaseSettings
        {
            ProviderName = SqlServerProviderName,
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 30
        };

        var options = Options.Create(settings);
        var connectionFactory = new DbConnectionFactory(options);

        // Resolve ILogger<DbContext> from service provider if available
        var logger = _services.GetService(typeof(ILogger<DbContext>)) as ILogger<DbContext>
                     ?? CreateNullLogger();

        return new DbContext(connectionFactory, options, logger, _services);
    }

    /// <summary>
    /// Creates a null logger when ILogger&lt;DbContext&gt; is not registered in DI.
    /// </summary>
    private static ILogger<DbContext> CreateNullLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        return loggerFactory.CreateLogger<DbContext>();
    }
}
