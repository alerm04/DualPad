using DualPad.Core.DualSense;
using DualPad.Core.Hid;
using DualPad.Core.Input;
using DualPad.Core.Mapping;
using DualPad.Core.Output;

namespace DualPad.Core;

/// <summary>One physical controller and the virtual Xbox controller that mirrors it.</summary>
public sealed class ControllerSession : IDisposable
{
    private readonly MappingEngine _engine = new();
    private readonly IInputSink _sink;
    private readonly object _outputLock = new();
    private volatile Profile _profile;
    private byte _rumbleLeft, _rumbleRight;
    private DualSenseOutput _output;
    private int _lastBatteryBand = -1;

    public DualSenseDevice Device { get; }
    public XboxOutput? Xbox { get; }
    public string? XboxError { get; }

    /// <summary>When false, input is read (for the live preview) but nothing is sent to Windows.</summary>
    public bool MappingEnabled { get; set; } = true;

    public DualSenseState State { get; private set; }
    public XboxReport LastReport { get; private set; }
    public DateTime ConnectedAt { get; } = DateTime.Now;

    public event Action<ControllerSession>? Disconnected;
    public event Action<ControllerSession, int>? BatteryChanged;

    internal ControllerSession(DualSenseDevice device, Profile profile, IInputSink sink)
    {
        Device = device;
        _profile = profile;
        _sink = sink;

        try
        {
            Xbox = XboxOutput.Create();
            Xbox.Rumble += OnRumble;
            Xbox.PlayerIndexChanged += _ => RefreshOutput();
        }
        catch (Exception ex)
        {
            XboxError = ex is ViGEmUnavailableException ? ex.Message : "Impossibile creare il controller Xbox virtuale: " + ex.Message;
        }

        device.StateReceived += OnState;
        device.Disconnected += _ => Disconnected?.Invoke(this);
        device.Start();
        RefreshOutput();
    }

    public Profile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            RefreshOutput();
        }
    }

    public int Battery => State.Battery;

    private void OnState(DualSenseDevice sender, in DualSenseState state)
    {
        var previousBattery = State.Battery;
        State = state;
        var profile = _profile;

        if (MappingEnabled)
        {
            var report = _engine.Process(state, profile, _sink);
            LastReport = report;
            Xbox?.Submit(report);
        }
        else
        {
            _engine.ReleaseAll(_sink);
            var neutral = new XboxReport();
            LastReport = neutral;
            Xbox?.Submit(neutral);
        }

        if (state.Battery != previousBattery)
        {
            BatteryChanged?.Invoke(this, state.Battery);
            var band = BatteryBand(state.Battery);
            if (band != _lastBatteryBand && profile.Lightbar.ShowBattery) RefreshOutput();
            _lastBatteryBand = band;
        }
    }

    private void OnRumble(byte large, byte small)
    {
        var rumble = _profile.Rumble;
        lock (_outputLock)
        {
            _rumbleLeft = rumble.Enabled ? Scale(large, rumble.Strength) : (byte)0;
            _rumbleRight = rumble.Enabled ? Scale(small, rumble.Strength) : (byte)0;
        }
        RefreshOutput();
    }

    /// <summary>Plays a short vibration so the user can feel the configured strength.</summary>
    public async Task TestRumbleAsync()
    {
        OnRumble(200, 200);
        await Task.Delay(350);
        OnRumble(0, 0);
    }

    public void RefreshOutput()
    {
        var light = _profile.Lightbar;
        lock (_outputLock)
        {
            var (r, g, b) = light.ShowBattery ? BatteryColor(State.Battery) : (light.R, light.G, light.B);
            var player = Xbox?.PlayerIndex ?? -1;
            _output = new DualSenseOutput
            {
                RumbleLeft = _rumbleLeft,
                RumbleRight = _rumbleRight,
                LightR = r,
                LightG = g,
                LightB = b,
                LedBrightness = light.Brightness,
                PlayerLeds = light.PlayerLeds && player >= 0 ? DualSenseOutputBuilder.PlayerPatterns[player] : (byte)0,
            };
            Device.SetOutput(_output);
        }
    }

    private static byte Scale(byte value, int percent) => (byte)Math.Clamp(value * percent / 100, 0, 255);

    private static int BatteryBand(int battery) => battery switch { <= 20 => 0, <= 50 => 1, _ => 2 };

    public static (byte R, byte G, byte B) BatteryColor(int battery) => BatteryBand(battery) switch
    {
        0 => (255, 30, 30),
        1 => (255, 170, 0),
        _ => (40, 220, 90),
    };

    public void Dispose()
    {
        Device.StateReceived -= OnState;
        _engine.ReleaseAll(_sink);
        lock (_outputLock)
        {
            // Stop vibration before letting go of the controller.
            Device.SetOutput(_output with { RumbleLeft = 0, RumbleRight = 0 });
        }
        Thread.Sleep(30);
        Xbox?.Dispose();
        Device.Dispose();
    }
}

/// <summary>A DualSense that is present but cannot be opened (usually because another app such as DS4Windows owns it).</summary>
public sealed record BlockedController(string Path, bool IsBluetooth);

/// <summary>Detects controllers and keeps one <see cref="ControllerSession"/> per connected DualSense.</summary>
public sealed class ControllerService : IDisposable
{
    private readonly object _lock = new();
    private readonly List<ControllerSession> _sessions = [];
    private readonly IInputSink _sink;
    private Timer? _scanTimer;
    private Profile _profile;
    private bool _mappingEnabled = true;
    private int _scanning;

    public ControllerService(Profile profile, IInputSink? sink = null)
    {
        _profile = profile;
        _sink = sink ?? InputSimulator.Instance;
    }

    public bool IsViGEmInstalled { get; } = XboxOutput.IsDriverInstalled();

    public IReadOnlyList<ControllerSession> Sessions
    {
        get { lock (_lock) return _sessions.ToList(); }
    }

    public IReadOnlyList<BlockedController> Blocked { get; private set; } = [];

    public event Action? SessionsChanged;
    public event Action<ControllerSession>? ControllerConnected;
    public event Action<ControllerSession>? ControllerDisconnected;
    public event Action<ControllerSession, int>? LowBattery;

    public Profile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            foreach (var s in Sessions) s.Profile = value;
        }
    }

    public bool MappingEnabled
    {
        get => _mappingEnabled;
        set
        {
            _mappingEnabled = value;
            foreach (var s in Sessions) s.MappingEnabled = value;
            SessionsChanged?.Invoke();
        }
    }

    public void Start() => _scanTimer = new Timer(_ => Scan(), null, 0, 1500);

    public void ScanNow() => ThreadPool.QueueUserWorkItem(_ => Scan());

    private void Scan()
    {
        if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
        try
        {
            ScanCore();
        }
        finally
        {
            Volatile.Write(ref _scanning, 0);
        }
    }

    private void ScanCore()
    {
        IReadOnlyList<HidDeviceInfo> found;
        try
        {
            found = DualSenseDevice.FindAll();
        }
        catch (Exception)
        {
            return;
        }

        var changed = false;
        var blocked = new List<BlockedController>();
        lock (_lock)
        {
            foreach (var info in found)
            {
                if (_sessions.Any(s => string.Equals(s.Device.Path, info.Path, StringComparison.OrdinalIgnoreCase))) continue;

                var device = DualSenseDevice.TryOpen(info);
                if (device is null)
                {
                    blocked.Add(new BlockedController(info.Path, info.IsBluetooth));
                    continue;
                }

                // The same controller plugged in by cable while paired over Bluetooth: keep the first one.
                if (_sessions.Any(s => s.Device.Serial == device.Serial))
                {
                    device.Dispose();
                    continue;
                }

                var session = new ControllerSession(device, _profile, _sink) { MappingEnabled = _mappingEnabled };
                session.Disconnected += OnDisconnected;
                session.BatteryChanged += OnBatteryChanged;
                _sessions.Add(session);
                changed = true;
                ControllerConnected?.Invoke(session);
            }
        }

        if (!blocked.SequenceEqual(Blocked))
        {
            Blocked = blocked;
            changed = true;
        }
        if (changed) SessionsChanged?.Invoke();
    }

    private void OnBatteryChanged(ControllerSession session, int battery)
    {
        if (battery <= 15 && session.State.Charge == ChargeState.Discharging) LowBattery?.Invoke(session, battery);
    }

    private void OnDisconnected(ControllerSession session)
    {
        lock (_lock)
        {
            if (!_sessions.Remove(session)) return;
        }
        ControllerDisconnected?.Invoke(session);
        ThreadPool.QueueUserWorkItem(_ => session.Dispose());
        SessionsChanged?.Invoke();
    }

    public void Dispose()
    {
        _scanTimer?.Dispose();
        lock (_lock)
        {
            foreach (var s in _sessions) s.Dispose();
            _sessions.Clear();
        }
    }
}
