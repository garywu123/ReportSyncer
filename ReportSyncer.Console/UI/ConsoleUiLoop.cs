// ============================================================================
// File: ConsoleUiLoop.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Main UI rendering loop with keyboard input handling.
// ============================================================================

using DotNetToolkit.Logging;
using ReportSyncer.Console.Logging;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Console.UI;

/// <summary>
/// Main UI rendering loop. Drains events, updates state, handles keyboard input, and renders Areas A/B/C/D.
/// CRITICAL: This is the ONLY place where Console rendering happens.
/// </summary>
public sealed class ConsoleUiLoop
{
    private readonly UiEventQueue _queue;
    private readonly UiStateStore _store;
    private readonly AreaBPager _pager;
    private readonly RingBufferLogStore _logStore;
    private readonly ILogService _log;
    private readonly bool _interactive;

    /// <summary>
    /// Initializes a new instance of <see cref="ConsoleUiLoop"/>.
    /// </summary>
    /// <param name="queue">Event queue to drain.</param>
    /// <param name="store">State store to update.</param>
    /// <param name="pager">Pager for Area B.</param>
    /// <param name="logStore">Log store for Area C.</param>
    /// <param name="log">Log service for errors.</param>
    public ConsoleUiLoop(
        UiEventQueue queue,
        UiStateStore store,
        AreaBPager pager,
        RingBufferLogStore logStore,
        ILogService log)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _pager = pager ?? throw new ArgumentNullException(nameof(pager));
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _interactive = !System.Console.IsInputRedirected;
    }

    /// <summary>
    /// Runs the UI loop until cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var buffer = new List<UiEvent>(100);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 1. Drain events from queue
                buffer.Clear();
                _queue.DrainTo(buffer, maxItems: 100);

                // 2. Apply events to state store
                foreach (var evt in buffer)
                {
                    _store.Apply(evt);
                }

                // 3. Handle keyboard input (interactive mode only)
                if (_interactive)
                {
                    HandleKeyboardInput();
                }
                else
                {
                    // Non-interactive: force page index to 0
                    _pager.PageIndex = 0;
                }

                // 4. Render UI
                Render();

                // 5. Wait before next tick
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                break;
            }
            catch (Exception ex)
            {
                _log.LogError($"ConsoleUiLoop encountered unexpected error: {ex.Message}");
                // Continue loop - don't crash UI
            }
        }
    }

    private void HandleKeyboardInput()
    {
        while (System.Console.KeyAvailable)
        {
            var key = System.Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.N:
                case ConsoleKey.RightArrow:
                    _pager.PageIndex++;
                    break;

                case ConsoleKey.P:
                case ConsoleKey.LeftArrow:
                    _pager.PageIndex--;
                    break;
            }

            // Handle ] and [ via KeyChar
            if (key.KeyChar == ']')
            {
                _pager.PageIndex++;
            }
            else if (key.KeyChar == '[')
            {
                _pager.PageIndex--;
            }
        }
    }

    private void Render()
    {
        try
        {
            System.Console.Clear();

            // Area A: Job Summary
            RenderAreaA();

            System.Console.WriteLine();

            // Area B: Table Status
            RenderAreaB();

            System.Console.WriteLine();

            // Area C: Recent Logs
            RenderAreaC();

            System.Console.WriteLine();

            // Area D: Key Hints
            RenderAreaD();
        }
        catch (Exception ex)
        {
            _log.LogError($"Render failed: {ex.Message}");
        }
    }

    private void RenderAreaA()
    {
        var summary = _store.GetSummary();
        System.Console.WriteLine($"=== Job Progress ===");
        System.Console.WriteLine($"Total: {summary.Total} | Running: {summary.Running} | Completed: {summary.Completed} | Failed: {summary.Failed} | Skipped: {summary.Skipped}");
    }

    private void RenderAreaB()
    {
        System.Console.WriteLine($"=== Tables ===");

        var allRows = _store.AllRows;

        // Sort: Failed first, then by InsertOrder
        var sortedRows = allRows
            .OrderBy(r => r.Status == TableStatus.FailedExecution || r.Status == TableStatus.FailedPreFlight ? 0 : 1)
            .ThenBy(r => r.InsertOrder)
            .ToList();

        // Select page
        var pageRows = _pager.SelectPage(sortedRows);
        var pageInfo = _pager.GetPageInfo(sortedRows.Count);

        System.Console.WriteLine($"Page {pageInfo.PageIndex + 1}/{pageInfo.PageCount} | Showing {pageInfo.StartRowNumber}-{pageInfo.EndRowNumber} of {pageInfo.TotalCount}");

        if (pageRows.Count == 0)
        {
            System.Console.WriteLine("(no tables)");
            return;
        }

        foreach (var row in pageRows)
        {
            var statusSymbol = row.Status switch
            {
                TableStatus.Planned => "⏳",
                TableStatus.Succeeded => "✅",
                TableStatus.FailedPreFlight => "❌",
                TableStatus.FailedExecution => "❌",
                TableStatus.SkippedDryRun => "⏭️",
                TableStatus.Cancelled => "🚫",
                _ => "?"
            };

            var remarks = string.IsNullOrWhiteSpace(row.Remarks) ? "" : $" | {row.Remarks}";
            System.Console.WriteLine($"{statusSymbol} {row.TableName} - {row.Phase}{remarks}");
        }
    }

    private void RenderAreaC()
    {
        System.Console.WriteLine($"=== Recent Logs ===");

        var logs = _logStore.Snapshot();
        var recentLogs = logs.TakeLast(10).ToList();

        if (recentLogs.Count == 0)
        {
            System.Console.WriteLine("(no logs)");
            return;
        }

        foreach (var log in recentLogs)
        {
            System.Console.WriteLine(log);
        }
    }

    private void RenderAreaD()
    {
        if (_interactive)
        {
            System.Console.WriteLine($"=== Controls ===");
            System.Console.WriteLine("n/] = Next Page | p/[ = Previous Page");
        }
    }
}
