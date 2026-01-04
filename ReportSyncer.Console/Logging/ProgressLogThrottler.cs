// ============================================================================
// File: ProgressLogThrottler.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Throttles high-frequency progress log writes to file.
// ============================================================================

using System.Collections.Concurrent;

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Throttles progress log writes to prevent excessive file I/O from high-frequency events.
/// </summary>
/// <remarks>
/// <para>
/// This throttler uses time-based coalescing: each unique key (jobId + table + phase)
/// can only write to the file log at most once per interval.
/// </para>
/// <para>
/// Terminal events (Completed, Failed, Skipped) always write and bypass throttling.
/// </para>
/// </remarks>
public sealed class ProgressLogThrottler
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minInterval;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWriteTimes;

    /// <summary>
    /// Initializes a new instance of <see cref="ProgressLogThrottler"/>.
    /// </summary>
    /// <param name="timeProvider">Time provider for testability.</param>
    /// <param name="minInterval">Minimum time interval between writes for the same key.</param>
    /// <exception cref="ArgumentNullException">Thrown when timeProvider is null.</exception>
    /// <exception cref="ArgumentException">Thrown when minInterval is not positive.</exception>
    public ProgressLogThrottler(TimeProvider timeProvider, TimeSpan minInterval)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        if (minInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Minimum interval must be positive.", nameof(minInterval));
        }

        _minInterval = minInterval;
        _lastWriteTimes = new ConcurrentDictionary<string, DateTimeOffset>();
    }

    /// <summary>
    /// Determines whether a progress log should be written to file.
    /// </summary>
    /// <param name="jobId">Job identifier.</param>
    /// <param name="table">Table name.</param>
    /// <param name="phase">Sync phase (e.g., "InProgress", "Completed").</param>
    /// <param name="isTerminal">True if this is a terminal event (Completed, Failed, Skipped).</param>
    /// <returns>True if the log should be written, false if it should be throttled.</returns>
    /// <remarks>
    /// Terminal events always return true and update the last write time.
    /// Non-terminal events are subject to time-based throttling.
    /// </remarks>
    public bool ShouldWrite(string jobId, string table, string phase, bool isTerminal)
    {
        if (string.IsNullOrEmpty(jobId) || string.IsNullOrEmpty(table) || string.IsNullOrEmpty(phase))
        {
            // Invalid key - allow write but don't track
            return true;
        }

        var key = $"{jobId}|{table}|{phase}";
        var now = _timeProvider.GetUtcNow();

        if (isTerminal)
        {
            // Terminal events always write and update timestamp
            _lastWriteTimes[key] = now;
            return true;
        }

        // Check if enough time has passed since last write
        if (_lastWriteTimes.TryGetValue(key, out var lastWrite))
        {
            var elapsed = now - lastWrite;
            if (elapsed < _minInterval)
            {
                // Throttled - too soon
                return false;
            }
        }

        // Update last write time and allow
        _lastWriteTimes[key] = now;
        return true;
    }

    /// <summary>
    /// Gets the number of tracked keys (for diagnostics/testing).
    /// </summary>
    public int TrackedKeyCount => _lastWriteTimes.Count;
}
