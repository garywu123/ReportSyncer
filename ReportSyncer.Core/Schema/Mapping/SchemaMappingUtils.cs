// ============================================================================
// File: SchemaMappingUtils.cs
// Author: GitHub Copilot (GPT-5 mini)
// Date: 2025-12-22
// Project: ReportSyncer
// Description: Utility helpers for schema mapping (type checks, parameter lookup).
// ============================================================================

using System;
using System.Collections.Generic;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Schema.Mapping;

internal static class SchemaMappingUtils
{
    public static bool IsTypeCompatible(ColumnSchema sourceCol, ColumnSchema targetCol)
    {
        var s = (sourceCol.DbType ?? string.Empty).ToLowerInvariant();
        var t = (targetCol.DbType ?? string.Empty).ToLowerInvariant();
        if (s == t) return true;

        var stringFamily = new[] { "varchar", "nvarchar", "char", "nchar", "text", "ntext" };
        var intFamily = new[] { "tinyint", "smallint", "int", "bigint" };
        var decimalFamily = new[] { "decimal", "numeric", "money", "smallmoney" };
        var dateFamily = new[] { "date", "datetime", "datetime2", "smalldatetime", "time" };
        var guidFamily = new[] { "uniqueidentifier" };
        var boolFamily = new[] { "bit" };

        if ((ArrayContains(stringFamily, s) && ArrayContains(stringFamily, t)) ||
            (ArrayContains(intFamily, s) && ArrayContains(intFamily, t)) ||
            (ArrayContains(decimalFamily, s) && ArrayContains(decimalFamily, t)) ||
            (ArrayContains(dateFamily, s) && ArrayContains(dateFamily, t)) ||
            (ArrayContains(guidFamily, s) && ArrayContains(guidFamily, t)) ||
            (ArrayContains(boolFamily, s) && ArrayContains(boolFamily, t)))
        {
            return true;
        }

        return false;
    }

    private static bool ArrayContains(string[] arr, string v)
    {
        foreach (var x in arr)
            if (string.Equals(x, v, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public static bool TryGetJobParameter(IReadOnlyDictionary<string, string> parameters, string key, out string? value)
    {
        if (parameters == null)
        {
            value = null;
            return false;
        }

        foreach (var kvp in parameters)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(kvp.Key, key))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
