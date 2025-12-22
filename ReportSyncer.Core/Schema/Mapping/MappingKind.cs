// ============================================================================
// File: MappingKind.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Enumeration of supported column mapping kinds used by the schema mapper.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Describes how a target column is populated when mapping from a source table.
/// </summary>
public enum MappingKind
{
    /// <summary>Map directly from a same-named source column.</summary>
    OneToOne,

    /// <summary>Populate the target column with a constant value.</summary>
    Constant,

    /// <summary>Populate the target column from a job-level context parameter.</summary>
    ContextColumn,

    /// <summary>Ignore this target column (do not populate).</summary>
    Ignored
}
