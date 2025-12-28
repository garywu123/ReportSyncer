// ============================================================================
// File: SafetyErrorCodes.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Stable error codes for safety violations.
// ============================================================================

namespace ReportSyncer.Core.Security;

/// <summary>
/// Well-known safety error codes emitted by the safety validator.
/// </summary>
public static class SafetyErrorCodes
{
    public const string DeleteWithoutScope = "DELETE_WITHOUT_SCOPE";
    public const string LargeDeleteConfirmationRequired = "LARGE_DELETE_CONFIRMATION_REQUIRED";
    public const string LargeDeleteConfirmationMismatch = "LARGE_DELETE_CONFIRMATION_MISMATCH";
    public const string NoSourceRead = "NO_SOURCE_READ";
    public const string NoTargetRead = "NO_TARGET_READ";
    public const string NoTargetInsert = "NO_TARGET_INSERT";
    public const string NoTargetDelete = "NO_TARGET_DELETE";
    public const string NoIdentityInsert = "NO_IDENTITY_INSERT";
    public const string MissingDeleteEstimate = "MISSING_DELETE_ESTIMATE";
    public const string NegativeEstimate = "NEGATIVE_ESTIMATE";
}
