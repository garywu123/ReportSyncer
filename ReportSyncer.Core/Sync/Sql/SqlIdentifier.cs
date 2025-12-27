// ============================================================================
// File: SqlIdentifier.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Helpers for safely escaping SQL identifiers.
// ============================================================================

using System;

namespace ReportSyncer.Core.Sync.Sql;

/// <summary>
/// Helpers for safely escaping and qualifying SQL identifiers for the target provider.
/// </summary>
internal static class SqlIdentifier
{
    /// <summary>
    /// Escapes a single SQL identifier using square-bracket quoting.
    /// </summary>
    /// <param name="name">The identifier name (schema, table or column).</param>
    /// <returns>The escaped identifier, e.g. <c>[MyName]</c>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is empty/whitespace or contains a closing bracket.</exception>
    public static string EscapeIdentifier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Identifier cannot be empty or whitespace.", nameof(name));
        if (name.Contains(']'))
            throw new ArgumentException("Identifier cannot contain closing bracket. Escape is not supported.", nameof(name));

        return $"[{name}]";
    }

    /// <summary>
    /// Qualifies a table name with its schema, escaping both parts.
    /// </summary>
    /// <param name="schema">Schema name.</param>
    /// <param name="table">Table name.</param>
    /// <returns>A fully qualified identifier like <c>[schema].[table]</c>.</returns>
    public static string Qualify(string schema, string table)
    {
        return $"{EscapeIdentifier(schema)}.{EscapeIdentifier(table)}";
    }
}
