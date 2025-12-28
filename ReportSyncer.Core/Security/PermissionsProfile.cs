// ============================================================================
// File: PermissionsProfile.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: DTO describing probed permissions for a table execution context.
// ============================================================================

namespace ReportSyncer.Core.Security;

/// <summary>
/// Result of probing runtime permissions for a table.
/// </summary>
public sealed record PermissionsProfile
{
    public PermissionsProfile(
        bool canReadSource,
        bool canReadTarget,
        bool? canDelete,
        bool? canInsert,
        bool? canSetIdentityInsert,
        IReadOnlyList<string>? notes = null)
    {
        CanReadSource = canReadSource;
        CanReadTarget = canReadTarget;
        CanDelete = canDelete;
        CanInsert = canInsert;
        CanSetIdentityInsert = canSetIdentityInsert;
        Notes = notes ?? Array.Empty<string>();
    }

    public bool CanReadSource { get; }

    public bool CanReadTarget { get; }

    public bool? CanDelete { get; }

    public bool? CanInsert { get; }

    public bool? CanSetIdentityInsert { get; }

    public IReadOnlyList<string> Notes { get; }
}
