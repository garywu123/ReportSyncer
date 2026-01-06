// ============================================================================
// File: ConsoleUiViewBuilder.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-06
// Description: Builds Spectre.Console renderables for the four UI areas.
// ============================================================================

using Spectre.Console;
using Spectre.Console.Rendering;
using ReportSyncer.Console.Logging;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Console.UI;

/// <summary>
/// Builds Spectre.Console UI components for the four display areas.
/// </summary>
/// <remarks>
/// <para>
/// This class is responsible for constructing the visual layout of the UI using Spectre.Console.
/// It separates rendering logic from the UI loop for better testability and maintainability.
/// </para>
/// <para>
/// The UI is divided into four areas:
/// - Area A: Job Progress Summary (counts of jobs in each state)
/// - Area B: Table Status (paginated list of tables with status symbols)
/// - Area C: Recent Logs (last N log lines from ring buffer)
/// - Area D: Control Hints (keyboard shortcuts for interactive mode)
/// </para>
/// <example>
/// <code><![CDATA[
/// var builder = new ConsoleUiViewBuilder();
/// var renderable = builder.Build(store, pager, logStore, maxLogLines: 10);
/// AnsiConsole.Write(renderable);
/// ]]></code>
/// </example>
/// </remarks>
public sealed class ConsoleUiViewBuilder
{
    /// <summary>
    /// Builds the complete UI layout.
    /// </summary>
    /// <param name="store">State store containing table rows and job summary.</param>
    /// <param name="pager">Pager for Area B pagination.</param>
    /// <param name="logStore">Log store for Area C.</param>
    /// <param name="maxLogLines">Maximum number of log lines to display.</param>
    /// <param name="interactive">Whether the UI is in interactive mode (shows controls).</param>
    /// <returns>A renderable layout containing all four areas.</returns>
    public IRenderable Build(
        UiStateStore store,
        AreaBPager pager,
        RingBufferLogStore logStore,
        int maxLogLines,
        bool interactive = true)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(pager);
        ArgumentNullException.ThrowIfNull(logStore);

        // Build individual areas
        var areaA = BuildAreaA(store);
        var areaB = BuildAreaB(store, pager);
        var areaC = BuildAreaC(logStore, maxLogLines);
        var areaD = interactive ? BuildAreaD() : new Text("");

        // Combine into vertical layout
        var layout = new Rows(
            areaA,
            new Text(""),  // Blank line
            areaB,
            new Text(""),  // Blank line
            areaC,
            new Text(""),  // Blank line
            areaD
        );

        return layout;
    }

    /// <summary>
    /// Builds the critical error panel (full-screen, red border).
    /// </summary>
    /// <param name="error">The exception to display.</param>
    /// <returns>A red error panel with exception details.</returns>
    public IRenderable BuildCriticalError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var errorText = new Markup($"[red bold]CRITICAL ERROR[/]\n\n" +
                                    $"[yellow]{Markup.Escape(error.GetType().Name)}[/]: {Markup.Escape(error.Message)}\n\n" +
                                    $"[dim]{Markup.Escape(error.StackTrace ?? "(no stack trace)")}[/]");

        var panel = new Panel(errorText)
            .Header("[red bold]⚠ REPORTER FAILURE ⚠[/]")
            .BorderColor(Color.Red)
            .Border(BoxBorder.Double);

        return panel;
    }

    // ========================================================================
    // Area Builders
    // ========================================================================

    private IRenderable BuildAreaA(UiStateStore store)
    {
        var summary = store.GetSummary();

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Label")
            .AddColumn("Value");

        table.AddRow("[bold]Total[/]", summary.Total.ToString());
        table.AddRow("[yellow]Running[/]", summary.Running.ToString());
        table.AddRow("[green]Completed[/]", summary.Completed.ToString());
        table.AddRow("[red]Failed[/]", summary.Failed.ToString());
        table.AddRow("[blue]Skipped[/]", summary.Skipped.ToString());

        var panel = new Panel(table)
            .Header("[bold]Job Progress[/]")
            .BorderColor(Color.Blue);

        return panel;
    }

    private IRenderable BuildAreaB(UiStateStore store, AreaBPager pager)
    {
        var allRows = store.AllRows;

        // Sort: Failed first, then by InsertOrder
        var sortedRows = allRows
            .OrderBy(r => r.Status == TableStatus.FailedExecution || r.Status == TableStatus.FailedPreFlight ? 0 : 1)
            .ThenBy(r => r.InsertOrder)
            .ToList();

        // Select page
        var pageRows = pager.SelectPage(sortedRows);
        var pageInfo = pager.GetPageInfo(sortedRows.Count);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Status")
            .AddColumn("Table")
            .AddColumn("Phase")
            .AddColumn("Remarks");

        if (pageRows.Count == 0)
        {
            table.AddRow("[dim](no tables)[/]", "", "", "");
        }
        else
        {
            foreach (var row in pageRows)
            {
                var statusSymbol = row.Status switch
                {
                    TableStatus.Planned => "⏳",
                    TableStatus.Succeeded => "[green]✅[/]",
                    TableStatus.FailedPreFlight => "[red]❌[/]",
                    TableStatus.FailedExecution => "[red]❌[/]",
                    TableStatus.SkippedDryRun => "[blue]⏭️[/]",
                    TableStatus.Cancelled => "[yellow]🚫[/]",
                    _ => "?"
                };

                var remarks = string.IsNullOrWhiteSpace(row.Remarks) ? "" : row.Remarks;
                table.AddRow(statusSymbol, Markup.Escape(row.TableName), Markup.Escape(row.Phase), Markup.Escape(remarks));
            }
        }

        var header = $"[bold]Tables[/] (Page {pageInfo.PageIndex + 1}/{pageInfo.PageCount} | Showing {pageInfo.StartRowNumber}-{pageInfo.EndRowNumber} of {pageInfo.TotalCount})";
        var panel = new Panel(table)
            .Header(header)
            .BorderColor(Color.Green);

        return panel;
    }

    private IRenderable BuildAreaC(RingBufferLogStore logStore, int maxLogLines)
    {
        var logs = logStore.Snapshot();
        var recentLogs = logs.TakeLast(maxLogLines).ToList();

        IRenderable content;
        if (recentLogs.Count == 0)
        {
            content = new Text("[dim](no logs)[/]");
        }
        else
        {
            var logText = string.Join("\n", recentLogs.Select(Markup.Escape));
            content = new Markup(logText);
        }

        var panel = new Panel(content)
            .Header("[bold]Recent Logs[/]")
            .BorderColor(Color.Yellow);

        return panel;
    }

    private IRenderable BuildAreaD()
    {
        var text = new Markup("[dim]n/] = Next Page | p/[ = Previous Page[/]");
        
        var panel = new Panel(text)
            .Header("[bold]Controls[/]")
            .BorderColor(Color.Grey);

        return panel;
    }
}
