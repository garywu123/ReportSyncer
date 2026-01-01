// ============================================================================
// File: ProgressTracker.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unified progress tracking for table synchronization operations.
//              Calculates metrics (throughput, percentage, ETA) with throttling
//              and optional exponential moving average (EMA) smoothing.
// ============================================================================

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Tracks progress for a long-running operation and calculates real-time metrics.
/// Supports throttled metric emission and EMA smoothing for throughput/ETA stability.
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Track cumulative rows processed vs total planned
/// - Calculate throughput (rows/sec) with optional EMA smoothing
/// - Calculate percentage complete and estimated time to completion (ETA)
/// - Throttle metric emission to avoid overwhelming consumers
/// 
/// Design:
/// - Pure computation, no I/O or side effects
/// - Testable via injected clock provider
/// - Thread-safe for single producer (not designed for concurrent AddProgress calls)
/// </remarks>
public sealed class ProgressTracker
{
    private readonly long _totalPlanned;
    private readonly TimeSpan _emitInterval;
    private readonly double? _etaSmoothing;
    private readonly Func<DateTimeOffset> _utcNow;

    private readonly DateTimeOffset _startedAtUtc;
    private DateTimeOffset _lastEmitUtc;
    private DateTimeOffset _lastSampleUtc;
    private long _lastSampleRows;
    private long _rowsProcessed;
    private double? _emaThroughput;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressTracker"/> class.
    /// </summary>
    /// <param name="totalPlanned">Total number of rows planned for the operation.</param>
    /// <param name="emitInterval">Minimum interval between metric emissions (throttling).</param>
    /// <param name="etaSmoothing">
    /// Optional EMA smoothing factor (0..1) for throughput calculation.
    /// - null: Use overall average throughput (totalProcessed / totalElapsed)
    /// - 0..1: Apply exponential moving average, where higher values give more weight to recent samples
    /// </param>
    /// <param name="utcNowProvider">
    /// Optional clock provider for testing. Defaults to DateTimeOffset.UtcNow.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when totalPlanned is negative, emitInterval is non-positive,
    /// or etaSmoothing is outside [0, 1] range.
    /// </exception>
    public ProgressTracker(
        long totalPlanned,
        TimeSpan emitInterval,
        double? etaSmoothing,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        if (totalPlanned < 0)
            throw new ArgumentOutOfRangeException(nameof(totalPlanned), "Total planned rows cannot be negative.");

        if (emitInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(emitInterval), "Emit interval must be positive.");

        if (etaSmoothing.HasValue && (etaSmoothing.Value < 0.0 || etaSmoothing.Value > 1.0))
            throw new ArgumentOutOfRangeException(nameof(etaSmoothing), "ETA smoothing must be between 0 and 1.");

        _totalPlanned = totalPlanned;
        _emitInterval = emitInterval;
        _etaSmoothing = etaSmoothing;
        _utcNow = utcNowProvider ?? (() => DateTimeOffset.UtcNow);

        var now = _utcNow();
        _startedAtUtc = now;
        _lastEmitUtc = now;
        _lastSampleUtc = now;
        _lastSampleRows = 0;
        _rowsProcessed = 0;
    }

    /// <summary>
    /// Records progress by adding the number of rows processed since the last call.
    /// </summary>
    /// <param name="deltaRows">Number of rows processed in this increment (must be non-negative).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when deltaRows is negative.</exception>
    /// <remarks>
    /// Cumulative rows processed is capped at totalPlanned to prevent overflow in percentage calculations.
    /// </remarks>
    public void AddProgress(long deltaRows)
    {
        if (deltaRows < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaRows), "Delta rows cannot be negative.");

        _rowsProcessed += deltaRows;

        // Cap at total planned to prevent percentage > 100%
        if (_rowsProcessed > _totalPlanned)
            _rowsProcessed = _totalPlanned;
    }

    /// <summary>
    /// Attempts to build progress metrics if the emit interval has elapsed.
    /// </summary>
    /// <param name="metrics">
    /// Output parameter containing calculated metrics if the method returns true.
    /// </param>
    /// <returns>
    /// True if metrics were calculated (emit interval elapsed); false if throttled.
    /// </returns>
    /// <remarks>
    /// Metrics calculation:
    /// - PercentComplete: (rowsProcessed / totalPlanned) * 100
    /// - ThroughputRowsPerSec:
    ///   - If etaSmoothing is null: overall average (rowsProcessed / elapsedSeconds)
    ///   - If etaSmoothing is set: exponential moving average of instantaneous throughput
    /// - EtaSeconds: (remaining rows) / throughput, rounded up
    /// 
    /// Null values:
    /// - Throughput/ETA are null if elapsed time is zero or no rows processed
    /// - PercentComplete is null if totalPlanned is zero
    /// </remarks>
    public bool TryBuildMetrics(out ProgressMetrics metrics)
    {
        var now = _utcNow();

        // Throttle: check if enough time has passed since last emit
        if (now - _lastEmitUtc < _emitInterval)
        {
            metrics = default!;
            return false;
        }

        var elapsed = now - _startedAtUtc;

        // Calculate percentage
        var percent = _totalPlanned == 0
            ? (double?)null
            : (double)_rowsProcessed / _totalPlanned * 100.0;

        // Calculate throughput
        double? throughput = null;
        if (elapsed.TotalSeconds > 0 && _rowsProcessed > 0)
        {
            if (_etaSmoothing is null)
            {
                // Use overall average throughput
                throughput = _rowsProcessed / elapsed.TotalSeconds;
            }
            else
            {
                // Use EMA of instantaneous throughput
                var sampleElapsed = now - _lastSampleUtc;
                if (sampleElapsed.TotalSeconds > 0)
                {
                    var delta = _rowsProcessed - _lastSampleRows;
                    var instantThroughput = delta / sampleElapsed.TotalSeconds;

                    // Initialize or update EMA
                    _emaThroughput = _emaThroughput is null
                        ? instantThroughput
                        : (_etaSmoothing.Value * instantThroughput) + ((1.0 - _etaSmoothing.Value) * _emaThroughput.Value);

                    throughput = _emaThroughput;
                }
            }
        }

        // Calculate ETA
        long? etaSeconds = null;
        if (throughput.HasValue && throughput.Value > 0 && _rowsProcessed < _totalPlanned)
        {
            var remaining = _totalPlanned - _rowsProcessed;
            etaSeconds = (long)Math.Ceiling(remaining / throughput.Value);
        }
        else if (_rowsProcessed >= _totalPlanned)
        {
            etaSeconds = 0;
        }

        metrics = new ProgressMetrics(
            RowsProcessed: _rowsProcessed,
            TotalRowsPlanned: _totalPlanned,
            PercentComplete: percent,
            ThroughputRowsPerSec: throughput,
            EtaSeconds: etaSeconds);

        // Update throttling and sampling state
        _lastEmitUtc = now;
        _lastSampleUtc = now;
        _lastSampleRows = _rowsProcessed;

        return true;
    }
}
