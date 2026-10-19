using DualPad.Core.Hid;

namespace DualPad.Core.DualSense;

public delegate void StateHandler(DualSenseDevice sender, in DualSenseState state);

/// <summary>A connected DualSense: reads input on a background task and writes output reports on demand.</summary>
public sealed class DualSenseDevice : IDisposable
{
    private readonly HidDevice _hid;
    private readonly CancellationTokenSource _cts = new();
    private readonly AutoResetEvent _outputSignal = new(false);
    private readonly object _outputLock = new();
    private DualSenseOutput _output;
    private bool _outputDirty;
    private byte _btSequence;
    private Task? _readTask;
    private Thread? _writeThread;
    private int _disposed;

    public ConnectionType Connection { get; }
    public string Path => _hid.Info.Path;
    public string InstanceId => _hid.Info.InstanceId;
    public bool IsEdge => _hid.Info.ProductId == DualSenseProtocol.DualSenseEdgeProductId;

    /// <summary>Stable id (MAC address when available) used to remember per-controller settings.</summary>
    public string Serial { get; }

    public DualSenseState LastState { get; private set; }

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

        _readTask = Task.Run(ReadLoop);
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

    private async Task ReadLoop()
    {
        var buffer = new byte[Math.Max((int)_hid.Info.InputReportLength, 78)];
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await _hid.ReadAsync(buffer.AsMemory(0, _hid.Info.InputReportLength), token).ConfigureAwait(false);
                if (read <= 0) break;
                if (!DualSenseProtocol.TryParseInput(buffer.AsSpan(0, read), Connection, out var state)) continue;
                LastState = state;
                StateReceived?.Invoke(this, in state);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            // Device removed or the handle was closed.
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
        try { _readTask?.Wait(500); } catch (AggregateException) { }
        _writeThread?.Join(500);
        _hid.Dispose();
        _cts.Dispose();
        _outputSignal.Dispose();
    }
}
