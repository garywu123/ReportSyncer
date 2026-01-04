// ============================================================================
// File: ConnectionStringResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Implementation of IConnectionStringResolver that maps connection names to connection strings.
// ============================================================================

using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Resolves connection names to connection strings from a pre-loaded configuration.
/// </summary>
/// <remarks>
/// This class builds an immutable connection name-to-connection string mapping during construction
/// and validates for duplicate connection names. It provides fast lookup with actionable error
/// messages when a connection name is not found.
/// </remarks>
public sealed class ConnectionStringResolver : IConnectionStringResolver
{
    private readonly IReadOnlyDictionary<string, string> _connectionMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStringResolver"/> class.
    /// </summary>
    /// <param name="connections">
    /// The collection of connection configurations to build the resolver from.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="connections"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when duplicate connection names are detected.
    /// </exception>
    public ConnectionStringResolver(IEnumerable<ConnectionConfig> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var conn in connections)
        {
            if (map.ContainsKey(conn.Name))
            {
                throw new InvalidOperationException(
                    $"Duplicate connection name '{conn.Name}' found in configuration. " +
                    "Connection names must be unique.");
            }

            map[conn.Name] = conn.ConnectionString;
        }

        _connectionMap = map;
    }

    /// <inheritdoc/>
    public string Get(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        if (_connectionMap.TryGetValue(connectionName, out var connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            $"Connection '{connectionName}' was not found. " +
            "Check YAML: connections[].name");
    }
}
