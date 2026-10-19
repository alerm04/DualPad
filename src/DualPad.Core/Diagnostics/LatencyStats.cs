using System.Diagnostics;

namespace DualPad.Core.Diagnostics;

/// <summary>Summary of a set of timings, in milliseconds.</summary>
public readonly record struct TimingSummary(int Count, double Average, double Median, double P99, double Max)
{
    public static TimingSummary From(IReadOnlyList<long> ticks)
    {
        if (ticks.Count == 0) return default;
        var ms = ticks.Select(t => t * 1000.0 / Stopwatch.Frequency).Order().ToArray();
        return new TimingSummary(ms.Length, ms.Average(), ms[ms.Length / 2], ms[Math.Min(ms.Length - 1, (int)(ms.Length * 0.99))], ms[^1]);
    }
}

/// <summary>Rolling record of report intervals and DualPad's own processing time.</summary>
public sealed class LatencyStats
{
    private const int Capacity = 8192;
    private readonly object _lock = new();
    private readonly long[] _intervals = new long[Capacity];
    private readonly long[] _processing = new long[Capacity];
    private int _count;
    private int _next;

    public void Add(long intervalTicks, long processingTicks)
    {
        lock (_lock)
        {
            _intervals[_next] = intervalTicks;
            _processing[_next] = processingTicks;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _count = 0;
            _next = 0;
        }
    }

    public (TimingSummary Intervals, TimingSummary Processing) Snapshot()
    {
        lock (_lock)
        {
            var intervals = new long[_count];
            var processing = new long[_count];
            for (var i = 0; i < _count; i++)
            {
                var index = (_next - _count + i + Capacity) % Capacity;
                intervals[i] = _intervals[index];
                processing[i] = _processing[index];
            }
            return (TimingSummary.From(intervals), TimingSummary.From(processing));
        }
    }
}
