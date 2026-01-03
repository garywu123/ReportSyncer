// ============================================================================
// File: ConnectionConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for a database connection with environment and type metadata.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// <summary>
/// Database connection configuration for sync operations.
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    /// Gets the unique name of this connection, used to reference it from sync jobs.
    /// </summary>
    /// <remarks>
    /// Connection names must be unique within a configuration file and are case-sensitive.
    /// Use descriptive names that indicate both the database and environment (e.g., "AppDB_Prod", "ReportDB_Dev").
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the SQL Server connection string for this connection.
    /// </summary>
    /// <remarks>
    /// Should include all necessary parameters for SQL Server connectivity including
    /// server, database, authentication, and any connection pooling or timeout settings.
    /// Supports both Windows Authentication (Integrated Security) and SQL Authentication.
    /// </remarks>
    public string ConnectionString { get; }

    /// <summary>
    /// Gets the environment classification for this connection.
    /// </summary>
    /// <remarks>
    /// Used to enforce safety rules such as preventing Prod→Prod syncs when
    /// <see cref="SafetyConfig.ForbidProdToProd"/> is enabled.
    /// </remarks>
    public EnvironmentType Environment { get; }

    /// <summary>
    /// Gets the type classification for this connection.
    /// </summary>
    public ConnectionType Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class.
    /// </summary>
    /// <param name="name">
    /// The unique name for this connection. Must not be null or empty.
    /// Used to reference this connection from sync jobs.
    /// </param>
    /// <param name="connectionString">
    /// The SQL Server connection string. Must not be null or empty.
    /// </param>
    /// <param name="environment">
    /// The environment classification for safety rule enforcement.
    /// </param>
    /// <param name="type">
    /// The connection type that determines context injection behavior.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="connectionString"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="connectionString"/> is empty or whitespace.
    /// </exception>
    public ConnectionConfig(
        string          name,
        string          connectionString,
        EnvironmentType environment,
        ConnectionType  type)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connection name cannot be empty or whitespace.", nameof(name));
        ArgumentNullException.ThrowIfNull(connectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty or whitespace.", nameof(connectionString));

        Name = name;
        ConnectionString = connectionString;
        Environment = environment;
        Type = type;
    }
}