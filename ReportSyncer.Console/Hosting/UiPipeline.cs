// ============================================================================
// File: UiPipeline.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: UI pipeline helper to wire up UI components with job execution.
// ============================================================================

using Microsoft.Extensions.Logging;
using ReportSyncer.Console.Logging;
using ReportSyncer.Console.UI;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Helper to create and wire up UI components for job execution.
/// </summary>
public sealed class UiPipeline : IDisposable
{
    private readonly UiEventQueue _queue;
    private readonly UiStateStore _store;
    private readonly AreaBPager _pager;
    private readonly ConsoleUiLoop _uiLoop;
    private readonly CancellationTokenSource _uiCts;
    private Task? _uiTask;

    /// <summary>
    /// Gets the progress reporter that feeds the UI pipeline.
    /// </summary>
    public ConsoleJobProgressReporter Reporter { get; }

    private UiPipeline(
        UiEventQueue queue,
        UiStateStore store,
        AreaBPager pager,
        RingBufferLogStore logStore,
        ConsoleJobProgressReporter reporter,
        ILogger<UiStateStore> storeLogger,
        ILogger<ConsoleUiLoop> loopLogger)
    {
        _queue = queue;
        _store = store;
        _pager = pager;
        Reporter = reporter;
        _uiLoop = new ConsoleUiLoop(queue, store, pager, logStore, loopLogger);
        _uiCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Creates a UI pipeline from a PreFlightResult.
    /// </summary>
    /// <param name="preFlight">PreFlight result containing execution plan.</param>
    /// <param name="logStore">Ring buffer log store for Area C.</param>
    /// <param name="storeLogger">Logger for UiStateStore.</param>
    /// <param name="reporterLogger">Logger for ConsoleJobProgressReporter.</param>
    /// <param name="loopLogger">Logger for ConsoleUiLoop.</param>
    /// <returns>A configured UI pipeline.</returns>
    public static UiPipeline Create(
        PreFlightResult preFlight,
        RingBufferLogStore logStore,
        ILogger<UiStateStore> storeLogger,
        ILogger<ConsoleJobProgressReporter> reporterLogger,
        ILogger<ConsoleUiLoop> loopLogger)
    {
        // Extract table names from execution plan (InsertOrder is the canonical list)
        var tables = preFlight.Schema.ExecutionPlan.InsertOrder
            .Select(t => t.ToString())
            .ToList();

        var queue = new UiEventQueue();
        var store = new UiStateStore(tables, storeLogger);
        var pager = new AreaBPager(pageSize: 20);
        var reporter = new ConsoleJobProgressReporter(queue, reporterLogger);

        return new UiPipeline(queue, store, pager, logStore, reporter, storeLogger, loopLogger);
    }

    /// <summary>
    /// Starts the UI rendering loop.
    /// </summary>
    public void Start()
    {
        _uiTask = _uiLoop.RunAsync(_uiCts.Token);
    }

    /// <summary>
    /// Stops the UI rendering loop and waits for completion.
    /// </summary>
    public async Task StopAsync()
    {
        if (_uiTask is null)
            return;

        _uiCts.Cancel();

        try
        {
            await _uiTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    public void Dispose()
    {
        _uiCts.Dispose();
    }
}
