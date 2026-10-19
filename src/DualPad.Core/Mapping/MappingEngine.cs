using DualPad.Core.DualSense;
using DualPad.Core.Input;

namespace DualPad.Core.Mapping;

/// <summary>State of the virtual Xbox 360 controller, in XInput units.</summary>
public struct XboxReport
{
    public ushort Buttons;
    public byte LeftTrigger, RightTrigger;
    public short LeftX, LeftY, RightX, RightY;

    public static ushort Flag(XboxControl control) => control switch
    {
        XboxControl.DpadUp => 0x0001,
        XboxControl.DpadDown => 0x0002,
        XboxControl.DpadLeft => 0x0004,
        XboxControl.DpadRight => 0x0008,
        XboxControl.Start => 0x0010,
        XboxControl.Back => 0x0020,
        XboxControl.LeftThumb => 0x0040,
        XboxControl.RightThumb => 0x0080,
        XboxControl.LeftBumper => 0x0100,
        XboxControl.RightBumper => 0x0200,
        XboxControl.Guide => 0x0400,
        XboxControl.A => 0x1000,
        XboxControl.B => 0x2000,
        XboxControl.X => 0x4000,
        XboxControl.Y => 0x8000,
        _ => 0,
    };

    public readonly bool IsPressed(XboxControl control) => (Buttons & Flag(control)) != 0;
}

/// <summary>Applies a <see cref="Profile"/> to controller input.</summary>
public sealed class MappingEngine
{
    // Analog triggers mapped to keyboard/mouse act as buttons past this point.
    private const int TriggerDigitalThreshold = 100;

    private readonly Dictionary<DsButtons, ButtonAction> _held = [];
    private readonly TouchpadMouse _touchpad = new();
    private double _stickRemX, _stickRemY;

    public XboxReport Process(in DualSenseState state, Profile profile, IInputSink sink)
    {
        var report = new XboxReport();

        foreach (var button in Profile.MappableButtons)
        {
            var action = profile.GetAction(button);
            var pressed = IsActive(state, button, action, profile.Triggers);
            ApplyButton(button, action, pressed, state, profile, sink, ref report);
        }

        ApplyStick(state.LeftX, state.LeftY, profile.LeftStick, sink, ref report);
        ApplyStick(state.RightX, state.RightY, profile.RightStick, sink, ref report);

        _touchpad.Process(state, profile.Touchpad, sink);
        return report;
    }

    /// <summary>Releases every key and mouse button that is still held, e.g. when mapping stops.</summary>
    public void ReleaseAll(IInputSink sink)
    {
        foreach (var action in _held.Values) Release(action, sink);
        _held.Clear();
        _touchpad.Reset();
    }

    private static bool IsActive(in DualSenseState state, DsButtons button, ButtonAction action, TriggerSettings triggers)
    {
        if (button is DsButtons.L2 or DsButtons.R2)
        {
            var value = button == DsButtons.L2 ? state.L2 : state.R2;
            var threshold = action.Kind == ActionKind.Xbox && action.Xbox is XboxControl.LeftTrigger or XboxControl.RightTrigger
                ? triggers.DeadZone
                : TriggerDigitalThreshold;
            return value > threshold;
        }
        return state.IsPressed(button);
    }

    private void ApplyButton(DsButtons button, ButtonAction action, bool pressed, in DualSenseState state, Profile profile, IInputSink sink, ref XboxReport report)
    {
        switch (action.Kind)
        {
            case ActionKind.Xbox:
                if (!pressed) break;
                if (action.Xbox is XboxControl.LeftTrigger or XboxControl.RightTrigger)
                {
                    var value = TriggerValue(state, button, profile.Triggers);
                    if (action.Xbox == XboxControl.LeftTrigger) report.LeftTrigger = Math.Max(report.LeftTrigger, value);
                    else report.RightTrigger = Math.Max(report.RightTrigger, value);
                }
                else
                {
                    report.Buttons |= XboxReport.Flag(action.Xbox);
                }
                break;

            case ActionKind.Keyboard:
            case ActionKind.Mouse:
                var isHeld = _held.TryGetValue(button, out var heldAction);
                if (pressed && !isHeld)
                {
                    var effective = action;
                    if (button == DsButtons.TouchpadClick && action is { Kind: ActionKind.Mouse, Mouse: MouseAction.LeftClick }
                        && profile.Touchpad.RightClickZone && TouchpadMouse.IsRightClickZone(state))
                    {
                        effective = ButtonAction.ToMouse(MouseAction.RightClick);
                    }
                    Press(effective, sink);
                    _held[button] = effective;
                }
                else if (!pressed && isHeld)
                {
                    Release(heldAction!, sink);
                    _held.Remove(button);
                }
                break;
        }

        // The mapping changed while the button was held: release what was pressed before.
        if (action.Kind is not (ActionKind.Keyboard or ActionKind.Mouse) && _held.Remove(button, out var stale))
            Release(stale, sink);
    }

    private static byte TriggerValue(in DualSenseState state, DsButtons source, TriggerSettings triggers)
    {
        if (source is not (DsButtons.L2 or DsButtons.R2)) return 255;
        var raw = source == DsButtons.L2 ? state.L2 : state.R2;
        if (raw <= triggers.DeadZone) return 0;
        // Rescale so the trigger still reaches 0..255 after the dead zone.
        return (byte)Math.Clamp((raw - triggers.DeadZone) * 255 / (255 - triggers.DeadZone), 0, 255);
    }

    private void ApplyStick(byte rawX, byte rawY, StickSettings settings, IInputSink sink, ref XboxReport report)
    {
        if (settings.Target == StickTarget.None) return;

        var (x, y) = NormalizeStick(rawX, rawY, settings);
        switch (settings.Target)
        {
            case StickTarget.LeftStick:
                report.LeftX = AddAxis(report.LeftX, x);
                report.LeftY = AddAxis(report.LeftY, y);
                break;
            case StickTarget.RightStick:
                report.RightX = AddAxis(report.RightX, x);
                report.RightY = AddAxis(report.RightY, y);
                break;
            case StickTarget.Mouse:
                _stickRemX += x * settings.MouseSpeed;
                _stickRemY += -y * settings.MouseSpeed;
                var mx = (int)_stickRemX;
                var my = (int)_stickRemY;
                _stickRemX -= mx;
                _stickRemY -= my;
                sink.MoveMouse(mx, my);
                break;
        }
    }

    /// <summary>Converts raw 0..255 axes to -1..1 with a radial dead zone and response curve. Y is up-positive like XInput.</summary>
    public static (double X, double Y) NormalizeStick(byte rawX, byte rawY, StickSettings settings)
    {
        var x = (rawX - 127.5) / 127.5;
        var y = -(rawY - 127.5) / 127.5;
        if (settings.InvertY) y = -y;

        var magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude <= settings.DeadZone || magnitude == 0) return (0, 0);

        var scaled = Math.Min(1, (magnitude - settings.DeadZone) / (1 - settings.DeadZone));
        if (settings.Curve != 1) scaled = Math.Pow(scaled, settings.Curve);
        var factor = scaled / magnitude;
        return (Math.Clamp(x * factor, -1, 1), Math.Clamp(y * factor, -1, 1));
    }

    private static short AddAxis(short current, double value) =>
        (short)Math.Clamp(current + Math.Round(value * 32767), short.MinValue, short.MaxValue);

    private static void Press(ButtonAction action, IInputSink sink)
    {
        if (action.Kind == ActionKind.Keyboard) sink.Key(action.Key, true);
        else if (action.Kind == ActionKind.Mouse) sink.MouseButton(action.Mouse, true);
    }

    private static void Release(ButtonAction action, IInputSink sink)
    {
        if (action.Kind == ActionKind.Keyboard) sink.Key(action.Key, false);
        else if (action.Kind == ActionKind.Mouse) sink.MouseButton(action.Mouse, false);
    }
}
