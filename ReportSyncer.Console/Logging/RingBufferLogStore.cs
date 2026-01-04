// ============================================================================
// File: RingBufferLogStore.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Thread-safe circular buffer for storing recent log lines.
// ============================================================================

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Thread-safe circular buffer for storing recent log lines.
/// </summary>
/// <remarks>
/// <para>
/// This store maintains a fixed-capacity buffer of the most recent log lines.
/// When the buffer reaches capacity, older entries are discarded (FIFO).
/// </para>
/// <para>
/// Designed for use with UI Area C to display recent log activity.
/// </para>
/// </remarks>
public sealed class RingBufferLogStore
{
    private readonly int _capacity;
    private readonly Queue<string> _buffer;
    private readonly object _lock = new object();

    /// <summary>
    /// Initializes a new instance of <see cref="RingBufferLogStore"/>.
    /// </summary>
    /// <param name="capacity">Maximum number of log lines to retain. Must be positive.</param>
    /// <exception cref="ArgumentException">Thrown when capacity is not positive.</exception>
    public RingBufferLogStore(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be positive.", nameof(capacity));
        }

        _capacity = capacity;
        _buffer = new Queue<string>(capacity);
    }

    /// <summary>
    /// Adds a log line to the buffer.
    /// </summary>
    /// <param name="line">The log line to add. Null or empty lines are ignored.</param>
    /// <remarks>
    /// If the buffer is at capacity, the oldest line is removed before adding the new one.
    /// </remarks>
    public void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_lock)
        {
            // Remove oldest if at capacity
            if (_buffer.Count >= _capacity)
            {
                _buffer.Dequeue();
            }

            _buffer.Enqueue(line);
        }
    }

    /// <summary>
    /// Creates a snapshot of the current log lines.
    /// </summary>
    /// <returns>
    /// A read-only list of log lines, ordered from oldest to newest.
    /// </returns>
    /// <remarks>
    /// The returned list is a copy and can be safely iterated without locking.
    /// </remarks>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock)
        {
            return _buffer.ToArray();
        }
    }

    /// <summary>
    /// Gets the current number of log lines in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Count;
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int Capacity => _capacity;
}
