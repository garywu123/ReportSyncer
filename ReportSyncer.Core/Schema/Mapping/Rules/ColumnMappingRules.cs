// ============================================================================
// File: ColumnMappingRules.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Column mapping rule implementations extracted from SchemaMapper.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Rule: ExplicitIgnore
/// 
/// If a column mapping entry explicitly requests ignore, this rule produces
/// an Ignored mapping. It is evaluated before other mapping rules so explicit
/// ignores take precedence.
/// </summary>
internal sealed class ExplicitIgnoreRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        if (context.ExplicitRule?.Ignore != true)
        {
            return ColumnResolution.NotHandled;
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(null, context.TargetColumn, MappingKind.Ignored, null)
        );
    }
}

/// <summary>
/// Rule: FromParameter
/// 
/// Maps a target column from a named job parameter. If the parameter is
/// missing the rule returns an error (JobParameterMissingForMapping).
/// </summary>
internal sealed class FromParameterRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        var fromParameter = context.ExplicitRule?.FromParameter;
        if (string.IsNullOrWhiteSpace(fromParameter))
        {
            return ColumnResolution.NotHandled;
        }

        if (!ColumnMappingHelpers.TryGetJobParameter(context.JobParameters, fromParameter!, out var paramValue))
        {
            return ColumnResolution.FromError(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.JobParameterMissingForMapping,
                    context.SourceTableId,
                    context.TargetTableId,
                    fromParameter,
                    null,
                    null
                )
            );
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(null, context.TargetColumn, MappingKind.Constant, paramValue)
        );
    }
}

/// <summary>
/// Rule: Const
/// 
/// Produces a constant mapping value when the task specifies a constant.
/// </summary>
internal sealed class ConstRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        if (context.ExplicitRule?.Const is null)
        {
            return ColumnResolution.NotHandled;
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(
                null,
                context.TargetColumn,
                MappingKind.Constant,
                context.ExplicitRule.Const
            )
        );
    }
}

/// <summary>
/// Rule: FromSource
/// 
/// Maps a target column directly from a named source column. If the source
/// column is missing the rule returns a TargetColumnMissingInSource error.
/// </summary>
internal sealed class FromSourceRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        var fromSource = context.ExplicitRule?.FromSource;
        if (string.IsNullOrWhiteSpace(fromSource))
        {
            return ColumnResolution.NotHandled;
        }

        if (!context.TryGetSourceColumn(fromSource!, out var sourceColumn))
        {
            return ColumnResolution.FromError(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.TargetColumnMissingInSource,
                    context.SourceTableId,
                    context.TargetTableId,
                    context.TargetColumn.Name,
                    null,
                    context.TargetColumn.DbType
                )
            );
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(sourceColumn, context.TargetColumn, MappingKind.OneToOne, null)
        );
    }
}

/// <summary>
/// Rule: ExplicitFallbackIgnore
/// When an explicit mapping entry exists but no directive (FromSource/Const/etc.)
/// was provided, this rule treats the column as ignored.
/// </summary>
internal sealed class ExplicitFallbackIgnoreRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        if (context.ExplicitRule is null)
        {
            return ColumnResolution.NotHandled;
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(null, context.TargetColumn, MappingKind.Ignored, null)
        );
    }
}

/// <summary>
/// Rule: Automap
/// 
/// Attempts a case-insensitive name-based mapping from source to target when
/// automap is enabled and no explicit rule exists.
/// </summary>
internal sealed class AutomapRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        if (!context.AutomapEnabled || context.ExplicitRule is not null)
        {
            return ColumnResolution.NotHandled;
        }

        if (!context.TryGetSourceColumn(context.TargetColumn.Name, out var sourceColumn))
        {
            return ColumnResolution.NotHandled;
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(sourceColumn, context.TargetColumn, MappingKind.OneToOne, null)
        );
    }
}

/// <summary>
/// Rule: ExtraTargetAllowed
/// 
/// When policy permits extra target columns, non-critical columns (nullable,
/// identity, computed or rowversion) are allowed to be ignored instead of
/// producing an error.
/// </summary>
internal sealed class ExtraTargetAllowedRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        var targetCol = context.TargetColumn;
        if (context.ExplicitRule is not null)
        {
            return ColumnResolution.NotHandled;
        }

        if (!context.SchemaPolicy.AllowExtraTargetColumns)
        {
            return ColumnResolution.NotHandled;
        }

        if (!(targetCol.IsNullable || targetCol.IsIdentity || targetCol.IsComputed || targetCol.IsRowVersion))
        {
            return ColumnResolution.NotHandled;
        }

        return ColumnResolution.FromMapping(
            new ColumnMapping(null, context.TargetColumn, MappingKind.Ignored, null)
        );
    }
}

/// <summary>
/// Rule: MissingSource (fallback)
/// 
/// Final catch-all rule that produces a TargetColumnMissingInSource error when
/// no prior rule produced a mapping. This ensures the chain always returns a
/// handled resolution.
/// </summary>
internal sealed class MissingSourceRule : IColumnMappingRule
{
    public ColumnResolution TryMap(ColumnMappingContext context)
    {
        return ColumnResolution.FromError(
            ColumnMappingHelpers.CreateError(
                SchemaMappingErrorCode.TargetColumnMissingInSource,
                context.SourceTableId,
                context.TargetTableId,
                context.TargetColumn.Name,
                null,
                context.TargetColumn.DbType
            )
        );
    }
}
