// ============================================================================
// File: RingBufferSink.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Serilog sink that writes formatted log lines to RingBufferLogStore.
// ============================================================================

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System.IO;

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Serilog sink that writes formatted log entries to a <see cref="RingBufferLogStore"/>.
/// </summary>
/// <remarks>
/// This sink is designed to provide recent log history for UI Area C.
/// It formats log events as human-readable text (not JSON).
/// </remarks>
public sealed class RingBufferSink : ILogEventSink
{
    private readonly RingBufferLogStore _store;
    private readonly ITextFormatter _formatter;
    private readonly object _lock = new object();

    /// <summary>
    /// Initializes a new instance of <see cref="RingBufferSink"/>.
    /// </summary>
    /// <param name="store">The ring buffer store to write to.</param>
    /// <param name="formatter">The text formatter for log events.</param>
    /// <exception cref="ArgumentNullException">Thrown when store or formatter is null.</exception>
    public RingBufferSink(RingBufferLogStore store, ITextFormatter formatter)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    /// <summary>
    /// Emits a log event to the ring buffer.
    /// </summary>
    /// <param name="logEvent">The log event to emit.</param>
    /// <remarks>
    /// This method is exception-safe: errors during formatting or writing will not propagate.
    /// </remarks>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null)
        {
            return;
        }

        try
        {
            lock (_lock)
            {
                using var writer = new StringWriter();
                _formatter.Format(logEvent, writer);
                var line = writer.ToString().TrimEnd('\r', '\n');

                // Optionally truncate very long lines (2KB max)
                if (line.Length > 2048)
                {
                    line = line.Substring(0, 2045) + "...";
                }

                _store.Add(line);
            }
        }
        catch
        {
            // Sink must be exception-safe - do not propagate errors
            // In production, this could be logged to a fallback logger
        }
    }
}
