// ============================================================================
// File: IDatabaseComponentFactory.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Abstraction for creating database components (IDbContext and IDbConnectionFactory).
// ============================================================================

using DotNetToolkit.Database.Abstractions;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Defines a factory for creating database components from connection strings.
/// </summary>
/// <remarks>
/// This abstraction allows the Console Host to create database components while keeping
/// the implementation details of DotNetToolkit.Database encapsulated. It also enables
/// easier testing by allowing mock implementations.
/// </remarks>
public interface IDatabaseComponentFactory
{
    /// <summary>
    /// Creates a new <see cref="IDbContext"/> instance for the specified connection string.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>A new <see cref="IDbContext"/> instance configured for the connection string.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="connectionString"/> is null or whitespace.
    /// </exception>
    IDbContext CreateDbContext(string connectionString);

    /// <summary>
    /// Creates a new <see cref="IDbConnectionFactory"/> instance for the specified connection string.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>
    /// A new <see cref="IDbConnectionFactory"/> instance configured for the connection string.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="connectionString"/> is null or whitespace.
    /// </exception>
    IDbConnectionFactory CreateConnectionFactory(string connectionString);
}
