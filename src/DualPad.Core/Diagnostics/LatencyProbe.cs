using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DualPad.Core.Diagnostics;

public sealed record LatencyResult(
    TimingSummary ReportIntervals,
    TimingSummary Processing,
    TimingSummary Delivery,
    double ReportRateHz,
    bool DeliveryMeasured)
{
    /// <summary>Typical delay DualPad adds between a report arriving and a game being able to read it.</summary>
    public double AddedMedianMs => Processing.Median + (DeliveryMeasured ? Delivery.Median : 0);

    /// <summary>Worst-case wait for the next report, i.e. how old input can be when it is read.</summary>
    public double PollingDelayMs => ReportIntervals.Average / 2;
}

/// <summary>
/// Measures the latency DualPad can observe: how often the controller sends reports, how long
/// DualPad takes to process each one, and how long the virtual Xbox controller takes to expose
/// the new state through XInput (the same API games read). The wireless link itself cannot be
/// measured in software.
/// </summary>
public static class LatencyProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int userIndex, out XInputState state);

    public static async Task<LatencyResult> RunAsync(ControllerSession session, TimeSpan duration, IProgress<double>? progress = null, CancellationToken token = default)
    {
        session.Stats.Reset();
        var deliveries = new List<long>();
        var slot = session.Xbox?.PlayerIndex ?? -1;
        var deadline = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);

        var probe = new Thread(() =>
        {
            if (slot < 0 || XInputGetState(slot, out var state) != 0) return;
            var lastPacket = state.PacketNumber;
            long lastMatched = 0;
            var window = Stopwatch.Frequency / 20; // ignore matches older than 50 ms
            while (Stopwatch.GetTimestamp() < deadline && !token.IsCancellationRequested)
            {
                if (XInputGetState(slot, out state) != 0) break;
                if (state.PacketNumber != lastPacket)
                {
                    var seen = Stopwatch.GetTimestamp();
                    lastPacket = state.PacketNumber;
                    var submitted = session.LastSubmitTimestamp;
                    if (submitted != lastMatched && submitted > 0 && seen >= submitted && seen - submitted < window)
                    {
                        deliveries.Add(seen - submitted);
                        lastMatched = submitted;
                    }
                }
                Thread.SpinWait(10);
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Highest, Name = "Latency probe" };
        probe.Start();

        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetTimestamp() < deadline && !token.IsCancellationRequested)
        {
            progress?.Report((Stopwatch.GetTimestamp() - start) / (double)(deadline - start));
            await Task.Delay(100, CancellationToken.None);
        }
        probe.Join();
        progress?.Report(1);

        var (intervals, processing) = session.Stats.Snapshot();
        var rate = intervals.Average > 0 ? 1000 / intervals.Average : 0;
        var delivery = TimingSummary.From(deliveries);
        return new LatencyResult(intervals, processing, delivery, rate, delivery.Count >= 5);
    }
}
