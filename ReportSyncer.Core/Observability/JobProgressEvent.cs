#region License

// author:         GWu
// created:        18:12
// description:

#endregion

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Progress notification for a job lifecycle.
/// </summary>
public sealed record JobProgressEvent(
    string            JobId,
    ProgressEventKind Kind,
    ProgressPhase     Phase,
    DateTimeOffset    UtcTimestamp,
    TimeSpan?         Elapsed      = null,
    string?           Message      = null,
    string?           ErrorCode    = null,
    string?           ErrorMessage = null);
