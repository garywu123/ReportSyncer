// ============================================================================
// File: SqlIdentifier.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Helpers for safely escaping SQL identifiers.
// ============================================================================

using System;

namespace ReportSyncer.Core.Sync.Sql;

internal static class SqlIdentifier
{
    public static string EscapeIdentifier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Identifier cannot be empty or whitespace.", nameof(name));
        if (name.Contains(']'))
            throw new ArgumentException("Identifier cannot contain closing bracket. Escape is not supported.", nameof(name));

        return $"[{name}]";
    }

    public static string Qualify(string schema, string table)
    {
        return $"{EscapeIdentifier(schema)}.{EscapeIdentifier(table)}";
    }
}
