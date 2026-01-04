// ============================================================================
// File: UiEventQueue.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Thread-safe queue for UI events using Channel.
// ============================================================================

using System.Threading.Channels;

namespace ReportSyncer.Console.UI;

/// <summary>
/// Thread-safe queue for UI events. Supports multiple writers and single reader.
/// </summary>
/// <remarks>
/// Uses <see cref="Channel{T}"/> internally to enable batched draining on the UI thread.
/// </remarks>
public sealed class UiEventQueue
{
    private readonly Channel<UiEvent> _channel;

    /// <summary>
    /// Initializes a new instance of <see cref="UiEventQueue"/>.
    /// </summary>
    /// <param name="capacity">Maximum queue capacity (default 1000).</param>
    public UiEventQueue(int capacity = 1000)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive");

        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<UiEvent>(options);
    }

    /// <summary>
    /// Attempts to enqueue an event without blocking.
    /// </summary>
    /// <param name="evt">Event to enqueue.</param>
    /// <returns>True if enqueued successfully; false if channel is closed or full.</returns>
    public bool TryEnqueue(UiEvent evt)
    {
        if (evt is null)
            return false;

        return _channel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Drains available events into the provided buffer without blocking.
    /// </summary>
    /// <param name="buffer">Buffer to receive events.</param>
    /// <param name="maxItems">Maximum number of items to drain (default 100).</param>
    /// <returns>Number of items drained.</returns>
    public int DrainTo(List<UiEvent> buffer, int maxItems = 100)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));

        if (maxItems <= 0)
            return 0;

        int count = 0;
        while (count < maxItems && _channel.Reader.TryRead(out var evt))
        {
            buffer.Add(evt);
            count++;
        }

        return count;
    }
}
