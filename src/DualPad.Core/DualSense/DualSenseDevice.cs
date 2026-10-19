using DualPad.Core.Hid;

namespace DualPad.Core.DualSense;

public delegate void StateHandler(DualSenseDevice sender, in DualSenseState state);

/// <summary>
/// A connected DualSense. Input is read on a dedicated high-priority thread and handled on that same
/// thread, so a report reaches the virtual Xbox controller without any queueing or thread hop.
/// </summary>
public sealed class DualSenseDevice : IDisposable
{
    private readonly HidDevice _hid;
    private readonly CancellationTokenSource _cts = new();
    private readonly AutoResetEvent _outputSignal = new(false);
    private readonly object _outputLock = new();
    private DualSenseOutput _output;
    private bool _outputDirty;
    private byte _btSequence;
    private Thread? _readThread;
    private Thread? _writeThread;
    private int _disposed;

    public ConnectionType Connection { get; }
    public string Path => _hid.Info.Path;
    public string InstanceId => _hid.Info.InstanceId;
    public bool IsEdge => _hid.Info.ProductId == DualSenseProtocol.DualSenseEdgeProductId;

    /// <summary>Stable id (MAC address when available) used to remember per-controller settings.</summary>
    public string Serial { get; }

    public DualSenseState LastState { get; private set; }

    /// <summary><see cref="System.Diagnostics.Stopwatch"/> timestamp taken as soon as the last report was read.</summary>
    public long LastReportTimestamp { get; private set; }

    public event StateHandler? StateReceived;
    public event Action<DualSenseDevice>? Disconnected;

    private DualSenseDevice(HidDevice hid)
    {
        _hid = hid;
        Connection = hid.Info.IsBluetooth || hid.Info.InputReportLength >= 78 ? ConnectionType.Bluetooth : ConnectionType.Usb;
        Serial = hid.GetSerialNumber() ?? hid.Info.InstanceId;
    }

    public static IReadOnlyList<HidDeviceInfo> FindAll() =>
        HidDevice.Enumerate(DualSenseProtocol.IsDualSense)
            // DualSense exposes a single top-level collection; skip anything that cannot carry input.
            .Where(d => d.InputReportLength >= 64)
            .ToList();

    public static DualSenseDevice? TryOpen(HidDeviceInfo info)
    {
        var hid = HidDevice.TryOpen(info);
        return hid is null ? null : new DualSenseDevice(hid);
    }

    public void Start()
    {
        if (Connection == ConnectionType.Bluetooth)
        {
            // Reading the calibration feature report switches the controller from the reduced
            // report 0x01 to the full report 0x31 over Bluetooth.
            var feature = new byte[Math.Max((int)_hid.Info.FeatureReportLength, 41)];
            feature[0] = DualSenseProtocol.CalibrationFeatureId;
            _hid.GetFeature(feature);
        }

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "DualSense input", Priority = ThreadPriority.Highest };
        _readThread.Start();
        _writeThread = new Thread(WriteLoop) { IsBackground = true, Name = "DualSense output", Priority = ThreadPriority.AboveNormal };
        _writeThread.Start();
    }

    /// <summary>Queues new output values; only changed values are sent.</summary>
    public void SetOutput(in DualSenseOutput output)
    {
        lock (_outputLock)
        {
            if (_output.Equals(output) && !_outputDirty) return;
            _output = output;
            _outputDirty = true;
        }
        _outputSignal.Set();
    }

    private void ReadLoop()
    {
        var buffer = new byte[Math.Max((int)_hid.Info.InputReportLength, 78)];
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = _hid.Read(buffer, _hid.Info.InputReportLength);
                var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                if (read <= 0) break;
                if (!DualSenseProtocol.TryParseInput(buffer.AsSpan(0, read), Connection, out var state)) continue;
                LastReportTimestamp = timestamp;
                LastState = state;
                StateReceived?.Invoke(this, in state);
            }
        }
        catch (Exception)
        {
            // Device removed, or Dispose cancelled the pending read.
        }

        if (!token.IsCancellationRequested) Disconnected?.Invoke(this);
    }

    private void WriteLoop()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            _outputSignal.WaitOne(250);
            if (token.IsCancellationRequested) break;

            DualSenseOutput output;
            lock (_outputLock)
            {
                if (!_outputDirty) continue;
                output = _output;
                _outputDirty = false;
            }

            // WriteFile expects the driver's maximum output length; the extra bytes are zero padding.
            var report = DualSenseOutputBuilder.Build(output, Connection, _btSequence, _hid.Info.OutputReportLength);
            _btSequence = (byte)((_btSequence + 1) & 0x0F);
            try
            {
                _hid.Write(report);
            }
            catch (Exception)
            {
                // Ignore transient write failures; the read loop reports real disconnects.
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cts.Cancel();
        _outputSignal.Set();
        _hid.CancelPendingIo();
        _readThread?.Join(500);
        _writeThread?.Join(500);
        _hid.Dispose();
        _cts.Dispose();
        _outputSignal.Dispose();
    }
}
