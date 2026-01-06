// ============================================================================
// File: ConsoleUiLoop.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Main UI rendering loop with keyboard input handling.
// ============================================================================

using Microsoft.Extensions.Logging;
using ReportSyncer.Console.Logging;
using ReportSyncer.Core.Sync.Contracts;
using Spectre.Console;

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
    private readonly ConsoleUiViewBuilder _viewBuilder;
    private readonly int _maxLogLines;
    private readonly ILogger<ConsoleUiLoop> _logger;
    private readonly bool _interactive;

    /// <summary>
    /// Initializes a new instance of <see cref="ConsoleUiLoop"/>.
    /// </summary>
    /// <param name="queue">Event queue to drain.</param>
    /// <param name="store">State store to update.</param>
    /// <param name="pager">Pager for Area B.</param>
    /// <param name="logStore">Log store for Area C.</param>
    /// <param name="viewBuilder">View builder for rendering.</param>
    /// <param name="maxLogLines">Maximum log lines to display in Area C.</param>
    /// <param name="logger">Logger for errors.</param>
    public ConsoleUiLoop(
        UiEventQueue queue,
        UiStateStore store,
        AreaBPager pager,
        RingBufferLogStore logStore,
        ConsoleUiViewBuilder viewBuilder,
        int maxLogLines,
        ILogger<ConsoleUiLoop> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _pager = pager ?? throw new ArgumentNullException(nameof(pager));
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        _viewBuilder = viewBuilder ?? throw new ArgumentNullException(nameof(viewBuilder));
        _maxLogLines = maxLogLines;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                _logger.LogError(ex, "ConsoleUiLoop encountered unexpected error");
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
            // Check for critical error first
            var criticalError = _store.CriticalError;
            if (criticalError != null)
            {
                AnsiConsole.Clear();
                var errorPanel = _viewBuilder.BuildCriticalError(criticalError);
                AnsiConsole.Write(errorPanel);
                return;
            }

            // Normal rendering using view builder
            AnsiConsole.Clear();
            var view = _viewBuilder.Build(_store, _pager, _logStore, _maxLogLines, _interactive);
            AnsiConsole.Write(view);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Render failed");
            // Fallback to console.error if Spectre fails
            System.Console.Error.WriteLine($"Render error: {ex.Message}");
        }
    }
}
