// ============================================================================
// File: TableRowState.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: UI state for a single table row in Area B.
// ============================================================================

using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Console.UI;

/// <summary>
/// Represents the display state of a single table in the UI.
/// </summary>
/// <param name="TableName">Fully qualified table name (schema.table).</param>
/// <param name="InsertOrder">Execution order index (0-based).</param>
/// <param name="Status">Current table status.</param>
/// <param name="Phase">Current sync phase name.</param>
/// <param name="Remarks">Additional status remarks or error messages.</param>
public sealed record TableRowState(
    string TableName,
    int InsertOrder,
    TableStatus Status,
    string Phase,
    string? Remarks);
