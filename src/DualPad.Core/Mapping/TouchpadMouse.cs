using System.Diagnostics;
using DualPad.Core.DualSense;
using DualPad.Core.Input;

namespace DualPad.Core.Mapping;

/// <summary>Turns touchpad contacts into cursor movement, tap clicks and two-finger scrolling.</summary>
public sealed class TouchpadMouse
{
    private const double BaseScale = 0.45;      // pixels per touchpad unit at sensitivity 1
    private const double TapMaxMs = 180;
    private const int TapMaxTravel = 40;         // touchpad units
    private const double ScrollScale = 1.6;      // wheel units (120 = notch) per touchpad unit

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TouchPoint _prev0, _prev1;
    private double _remX, _remY, _remWheel;
    private double _lastDx, _lastDy;
    private double _touchStartMs;
    private int _travel;
    private int _maxFingers;
    private bool _physicalClickDuringTouch;

    public void Reset()
    {
        _prev0 = _prev1 = default;
        _remX = _remY = _remWheel = 0;
        _lastDx = _lastDy = 0;
        _maxFingers = 0;
    }

    public void Process(in DualSenseState state, TouchpadSettings settings, IInputSink sink)
    {
        if (settings.Mode != TouchpadMode.Mouse)
        {
            Reset();
            return;
        }

        var t0 = state.Touch0;
        var t1 = state.Touch1;
        var fingers = (t0.Active ? 1 : 0) + (t1.Active ? 1 : 0);
        var wasFingers = (_prev0.Active ? 1 : 0) + (_prev1.Active ? 1 : 0);
        var now = _clock.Elapsed.TotalMilliseconds;

        if (wasFingers == 0 && fingers > 0)
        {
            _touchStartMs = now;
            _travel = 0;
            _maxFingers = 0;
            _physicalClickDuringTouch = false;
            _lastDx = _lastDy = 0;
        }

        _maxFingers = Math.Max(_maxFingers, fingers);
        if (state.IsPressed(DsButtons.TouchpadClick)) _physicalClickDuringTouch = true;

        if (fingers == 1 && wasFingers == 1)
        {
            var (cur, prev) = t0.Active ? (t0, _prev0) : (t1, _prev1);
            if (prev.Active && prev.Id == cur.Id) MoveCursor(cur.X - prev.X, cur.Y - prev.Y, settings, sink);
        }
        else if (fingers == 2 && wasFingers == 2 && settings.TwoFingerScroll && _prev0.Active && _prev1.Active)
        {
            var dy = ((t0.Y - _prev0.Y) + (t1.Y - _prev1.Y)) / 2.0;
            _travel += (int)Math.Abs(dy);
            _remWheel += -dy * ScrollScale * settings.Sensitivity;
            var amount = (int)_remWheel;
            if (amount != 0)
            {
                _remWheel -= amount;
                sink.WheelRaw(amount);
            }
        }

        if (wasFingers > 0 && fingers == 0)
        {
            var isTap = settings.TapToClick && !_physicalClickDuringTouch
                && now - _touchStartMs <= TapMaxMs && _travel <= TapMaxTravel;
            if (isTap)
            {
                var button = _maxFingers >= 2 ? MouseAction.RightClick : MouseAction.LeftClick;
                sink.MouseButton(button, true);
                sink.MouseButton(button, false);
            }
            _remX = _remY = 0;
        }

        _prev0 = t0;
        _prev1 = t1;
    }

    private void MoveCursor(int rawDx, int rawDy, TouchpadSettings settings, IInputSink sink)
    {
        _travel += Math.Abs(rawDx) + Math.Abs(rawDy);

        // Light smoothing over two reports removes jitter without adding noticeable lag.
        var dx = (rawDx + _lastDx) / 2.0;
        var dy = (rawDy + _lastDy) / 2.0;
        _lastDx = rawDx;
        _lastDy = rawDy;

        var gain = BaseScale * settings.Sensitivity;
        if (settings.Acceleration)
        {
            var speed = Math.Sqrt(dx * dx + dy * dy);
            gain *= 1 + Math.Min(1.6, speed / 35.0);
        }

        _remX += dx * gain;
        _remY += dy * gain;
        var mx = (int)_remX;
        var my = (int)_remY;
        _remX -= mx;
        _remY -= my;
        sink.MoveMouse(mx, my);
    }

    /// <summary>True when a press on the touchpad should count as a right click.</summary>
    public static bool IsRightClickZone(in DualSenseState state) =>
        (state.Touch0.Active && state.Touch1.Active)
        || (state.Touch0.Active && state.Touch0.X > DualSenseState.TouchpadWidth / 2);
}
