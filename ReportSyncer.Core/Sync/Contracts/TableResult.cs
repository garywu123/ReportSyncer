// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Result summary for a single table within a sync job.
/// </summary>
public sealed record TableResult(
    string      TargetTable,
    TableStatus Status,
    long?       RowsDeleted  = null,
    long?       RowsInserted = null,
    TimeSpan?   Duration     = null,
    string?     ErrorCode    = null,
    string?     ErrorMessage = null
);