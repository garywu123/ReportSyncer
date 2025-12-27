// ============================================================================
// File: WhereClauseBuilder.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Builds WHERE clause SQL fragments and parameters from filter predicates.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync.Sql;

internal sealed class WhereClauseBuilder
{
    public (string Sql, IReadOnlyList<CommandParameterSpec> Parameters) Build(
        IReadOnlyList<FilterPredicate> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
        {
            return (string.Empty, Array.Empty<CommandParameterSpec>());
        }

        var parts = new List<string>(filters.Count);
        var parameters = new List<CommandParameterSpec>();
        var index = 0;

        foreach (var filter in filters)
        {
            if (filter is null)
                throw new ArgumentException("Filters cannot contain null entries.", nameof(filters));

            var column = SqlIdentifier.EscapeIdentifier(filter.ColumnName);

            switch (filter.Operator)
            {
                case FilterOperator.Equals:
                    parts.Add($"{column} = @p{index}");
                    parameters.Add(new CommandParameterSpec($"@p{index}", filter.Value));
                    index++;
                    break;
                case FilterOperator.GreaterOrEqual:
                    parts.Add($"{column} >= @p{index}");
                    parameters.Add(new CommandParameterSpec($"@p{index}", filter.Value));
                    index++;
                    break;
                case FilterOperator.LessOrEqual:
                    parts.Add($"{column} <= @p{index}");
                    parameters.Add(new CommandParameterSpec($"@p{index}", filter.Value));
                    index++;
                    break;
                case FilterOperator.BetweenInclusive:
                    parts.Add($"{column} >= @p{index} AND {column} <= @p{index + 1}");
                    parameters.Add(new CommandParameterSpec($"@p{index}", filter.Value));
                    parameters.Add(new CommandParameterSpec($"@p{index + 1}", filter.Value2));
                    index += 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filter.Operator), filter.Operator, "Unsupported filter operator.");
            }
        }

        var sql = $"WHERE {string.Join(" AND ", parts)}";
        return (sql, parameters.AsReadOnly());
    }
}
