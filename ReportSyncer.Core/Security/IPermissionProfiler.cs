// ============================================================================
// File: IPermissionProfiler.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Abstraction for probing database permissions for a table context.
// ============================================================================

using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Security;

/// <summary>
/// Probes database permissions for a given table execution context.
/// Implementations must use only no-op operations to avoid mutating data.
/// </summary>
public interface IPermissionProfiler
{
    /// <summary>
    /// Probes permissions for the supplied table execution context.
    /// </summary>
    /// <param name="tableCtx">Execution context describing the table pair and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A permissions profile describing which operations are allowed.</returns>
    Task<PermissionsProfile> ProbeTablePermissionsAsync(TableExecutionContext tableCtx, CancellationToken ct);
}
