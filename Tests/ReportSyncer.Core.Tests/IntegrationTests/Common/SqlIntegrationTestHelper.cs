using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

/*
 Author: Gary Wu
 Project: ReportSyncer
 Date: 2025-12-28
 Description: Integration test helper utilities for SQL Server-backed integration tests.
 */

namespace ReportSyncer.Core.Tests.IntegrationTests.Common;

/// <summary>
/// Provides helper methods used by SQL Server integration tests.
/// Includes helpers for reading environment-based connection strings, locating seed scripts,
/// creating and dropping test databases, and executing SQL scripts with GO batch support.
/// </summary>
internal static class SqlIntegrationTestHelper
{
    /// <summary>
    /// Reads a base connection string from an environment variable required by an integration test fixture.
    /// If the environment variable is missing or empty, sets <paramref name="skipReason"/> and returns <c>null</c>.
    /// </summary>
    /// <param name="envVarName">The environment variable name that should contain the base connection string.</param>
    /// <param name="fixtureName">The name of the integration test fixture (used for logging).</param>
    /// <param name="skipReason">Output parameter set to a human-readable skip reason when the env var is missing.</param>
    /// <returns>The base connection string if present; otherwise <c>null</c>.</returns>
    public static string? RequireBaseConnection(string envVarName, string fixtureName, out string? skipReason)
    {
        var baseConn = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrWhiteSpace(baseConn))
        {
            skipReason = $"Set {envVarName} to run {fixtureName} integration tests.";
            Console.WriteLine($"[{fixtureName}] {skipReason}");
            return null;
        }

        skipReason = null;
        return baseConn;
    }

    /// <summary>
    /// Searches for a seed SQL script file by combining <paramref name="baseDir"/> with each candidate relative path.
    /// Returns the first matching file path or <c>null</c> if none are found.
    /// </summary>
    /// <param name="baseDir">The directory to combine with the relative candidate paths.</param>
    /// <param name="relativeCandidates">One or more relative file paths to try.</param>
    /// <returns>The full path to the first existing file, or <c>null</c> if not found.</returns>
    public static string? FindSeedScript(string baseDir, params string[] relativeCandidates)
    {
        foreach (var relative in relativeCandidates)
        {
            var path = Path.Combine(baseDir, relative);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a new database with the specified name using the provided base connection string.
    /// The <paramref name="baseConnectionString"/> should be a server-level connection string (no Initial Catalog).
    /// </summary>
    /// <param name="baseConnectionString">The base connection string for the server (without Initial Catalog).</param>
    /// <param name="databaseName">The database name to create.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A task that completes when the database has been created.</returns>
    public static async Task CreateDatabaseAsync(string baseConnectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        var masterConn = $"{baseConnectionString};Initial Catalog=master";
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new SqlCommand($"CREATE DATABASE [{databaseName}];", conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the specified database if it exists. The database is set to SINGLE_USER with immediate rollback
    /// before being dropped to ensure the operation succeeds in test environments.
    /// </summary>
    /// <param name="baseConnectionString">The base connection string for the server (without Initial Catalog).</param>
    /// <param name="databaseName">The database name to drop.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A task that completes when the database has been dropped (or did not exist).</returns>
    public static async Task DropDatabaseAsync(string baseConnectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        var masterConn = $"{baseConnectionString};Initial Catalog=master";
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
                   IF DB_ID('{databaseName}') IS NOT NULL
                   BEGIN
                       ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                       DROP DATABASE [{databaseName}];
                   END;
                   """;
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the SQL script at <paramref name="scriptPath"/> against the provided connection string.
    /// The script is split by GO batch separators (case-insensitive) so multi-batch scripts can be executed.
    /// </summary>
    /// <param name="connectionString">The full connection string to use when executing the script.</param>
    /// <param name="scriptPath">The path to the SQL script file to execute.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A task that completes when the script has been executed.</returns>
    public static async Task ExecuteScriptAsync(string connectionString, string scriptPath, CancellationToken cancellationToken = default)
    {
        var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var batch in batches)
        {
            var text = batch?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            await using var cmd = new SqlCommand(text, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
