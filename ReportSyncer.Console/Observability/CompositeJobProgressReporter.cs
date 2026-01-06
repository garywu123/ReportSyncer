// ============================================================================
// File: CompositeJobProgressReporter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-06
// Description: Fans out progress events to multiple reporters with error handling.
// ============================================================================

using Microsoft.Extensions.Logging;
using ReportSyncer.Core.Observability;

namespace ReportSyncer.Console.Observability;

/// <summary>
/// Composite reporter that fans out progress events to multiple child reporters.
/// </summary>
/// <remarks>
/// <para>
/// This reporter implements the Composite pattern to broadcast progress events to multiple
/// reporters simultaneously (e.g., UI reporter + file log reporter).
/// </para>
/// <para>
/// Error Handling:
/// - If a child reporter throws, the exception is logged and the criticalErrorHandler is invoked.
/// - The error does NOT stop other reporters from receiving the event.
/// - This allows UI to continue updating even if file logging fails, and vice versa.
/// </para>
/// <para>
/// Thread-safety: This class itself is thread-safe, but child reporters must also be thread-safe.
/// </para>
/// <example>
/// <code><![CDATA[
/// var reporters = new IJobProgressReporter[]
/// {
///     new TextLogJobProgressReporter(logger, throttler),
///     new ConsoleJobProgressReporter(uiQueue, logger)
/// };
/// 
/// Action<Exception> errorHandler = (ex) =>
/// {
///     jobCts.Cancel();  // Cancel current job
///     uiPipeline?.ReportCriticalError(ex);  // Show error in UI
/// };
/// 
/// var composite = new CompositeJobProgressReporter(reporters, logger, errorHandler);
/// ]]></code>
/// </example>
/// </remarks>
public sealed class CompositeJobProgressReporter : IJobProgressReporter
{
    private readonly IJobProgressReporter[] _reporters;
    private readonly ILogger<CompositeJobProgressReporter> _logger;
    private readonly Action<Exception>? _criticalErrorHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="CompositeJobProgressReporter"/>.
    /// </summary>
    /// <param name="reporters">Child reporters to fan out to. Empty array is allowed (no-op).</param>
    /// <param name="logger">Logger for recording reporter failures.</param>
    /// <param name="criticalErrorHandler">Optional callback for critical errors (e.g., cancel job, notify UI).</param>
    public CompositeJobProgressReporter(
        IJobProgressReporter[] reporters,
        ILogger<CompositeJobProgressReporter> logger,
        Action<Exception>? criticalErrorHandler = null)
    {
        _reporters = reporters ?? throw new ArgumentNullException(nameof(reporters));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _criticalErrorHandler = criticalErrorHandler;
    }

    /// <summary>
    /// Reports a job progress event to all child reporters.
    /// </summary>
    /// <param name="evt">The job progress event.</param>
    /// <remarks>
    /// If a child reporter throws, the exception is logged and error handler is invoked,
    /// but other reporters will still receive the event.
    /// </remarks>
    public void Report(JobProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        foreach (var reporter in _reporters)
        {
            try
            {
                reporter.Report(evt);
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(
                    ex,
                    "Reporter {ReporterType} failed to report job progress event. JobId={JobId}, Kind={Kind}, Phase={Phase}",
                    reporter.GetType().Name,
                    evt.JobId,
                    evt.Kind,
                    evt.Phase);

                // Invoke critical error handler (e.g., cancel job, notify UI)
                try
                {
                    _criticalErrorHandler?.Invoke(ex);
                }
                catch (Exception handlerEx)
                {
                    // Handler itself failed - log but don't propagate
                    _logger.LogError(handlerEx, "Critical error handler threw an exception");
                }

                // Continue to next reporter (don't throw)
            }
        }
    }

    /// <summary>
    /// Reports a table progress event to all child reporters.
    /// </summary>
    /// <param name="evt">The table progress event.</param>
    /// <remarks>
    /// If a child reporter throws, the exception is logged and error handler is invoked,
    /// but other reporters will still receive the event.
    /// </remarks>
    public void Report(TableProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        foreach (var reporter in _reporters)
        {
            try
            {
                reporter.Report(evt);
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(
                    ex,
                    "Reporter {ReporterType} failed to report table progress event. JobId={JobId}, Table={Table}, Kind={Kind}, Phase={Phase}",
                    reporter.GetType().Name,
                    evt.JobId,
                    evt.Table,
                    evt.Kind,
                    evt.Phase);

                // Invoke critical error handler (e.g., cancel job, notify UI)
                try
                {
                    _criticalErrorHandler?.Invoke(ex);
                }
                catch (Exception handlerEx)
                {
                    // Handler itself failed - log but don't propagate
                    _logger.LogError(handlerEx, "Critical error handler threw an exception");
                }

                // Continue to next reporter (don't throw)
            }
        }
    }
}
