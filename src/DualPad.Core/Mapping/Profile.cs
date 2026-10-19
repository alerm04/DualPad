using System.Text.Json.Serialization;
using DualPad.Core.DualSense;

namespace DualPad.Core.Mapping;

/// <summary>Controls of the virtual Xbox 360 controller.</summary>
public enum XboxControl
{
    A, B, X, Y,
    LeftBumper, RightBumper,
    LeftTrigger, RightTrigger,
    Back, Start, Guide,
    LeftThumb, RightThumb,
    DpadUp, DpadDown, DpadLeft, DpadRight,
}

public enum MouseAction { LeftClick, RightClick, MiddleClick, Back, Forward, WheelUp, WheelDown }

public enum ActionKind { None, Xbox, Keyboard, Mouse }

/// <summary>What a physical button does when pressed.</summary>
public sealed record ButtonAction
{
    public ActionKind Kind { get; init; }
    public XboxControl Xbox { get; init; }
    /// <summary>Windows virtual-key code for <see cref="ActionKind.Keyboard"/>.</summary>
    public int Key { get; init; }
    public MouseAction Mouse { get; init; }

    public static readonly ButtonAction None = new() { Kind = ActionKind.None };
    public static ButtonAction ToXbox(XboxControl control) => new() { Kind = ActionKind.Xbox, Xbox = control };
    public static ButtonAction ToKey(int virtualKey) => new() { Kind = ActionKind.Keyboard, Key = virtualKey };
    public static ButtonAction ToMouse(MouseAction action) => new() { Kind = ActionKind.Mouse, Mouse = action };
}

public enum StickTarget { LeftStick, RightStick, Mouse, None }

public sealed record StickSettings
{
    public StickTarget Target { get; init; }
    /// <summary>Radial dead zone, 0..0.5 of the full range.</summary>
    public double DeadZone { get; init; } = 0.08;
    /// <summary>Sensitivity curve exponent: 1 = linear, &gt;1 finer control near the center.</summary>
    public double Curve { get; init; } = 1.0;
    public bool InvertY { get; init; }
    /// <summary>Cursor speed when the stick drives the mouse, in pixels per report at full deflection.</summary>
    public double MouseSpeed { get; init; } = 12;
}

public sealed record TriggerSettings
{
    /// <summary>Values below this (0..255) are treated as released.</summary>
    public int DeadZone { get; init; } = 6;
}

public enum TouchpadMode { Mouse, None }

public sealed record TouchpadSettings
{
    public TouchpadMode Mode { get; init; } = TouchpadMode.Mouse;
    /// <summary>Cursor speed multiplier.</summary>
    public double Sensitivity { get; init; } = 1.0;
    /// <summary>Accelerate fast swipes so the cursor can cross the screen in one gesture.</summary>
    public bool Acceleration { get; init; } = true;
    /// <summary>A quick tap without moving clicks the left mouse button.</summary>
    public bool TapToClick { get; init; } = true;
    /// <summary>Two fingers moving vertically scroll the mouse wheel.</summary>
    public bool TwoFingerScroll { get; init; } = true;
    /// <summary>Pressing the right half of the touchpad (or with two fingers) gives a right click.</summary>
    public bool RightClickZone { get; init; } = true;
}

public sealed record LightbarSettings
{
    public byte R { get; init; } = 0x00;
    public byte G { get; init; } = 0x78;
    public byte B { get; init; } = 0xFF;
    /// <summary>0 = high, 1 = medium, 2 = low.</summary>
    public byte Brightness { get; init; }
    /// <summary>Color the lightbar by battery level instead of the fixed color.</summary>
    public bool ShowBattery { get; init; }
    /// <summary>Show the Xbox player slot on the white LEDs under the touchpad.</summary>
    public bool PlayerLeds { get; init; } = true;
}

public sealed record RumbleSettings
{
    public bool Enabled { get; init; } = true;
    /// <summary>Strength multiplier in percent, 0..150.</summary>
    public int Strength { get; init; } = 100;
}

/// <summary>A full controller configuration.</summary>
public sealed record Profile
{
    public string Name { get; init; } = "Xbox";
    public Dictionary<DsButtons, ButtonAction> Buttons { get; init; } = [];
    public StickSettings LeftStick { get; init; } = new() { Target = StickTarget.LeftStick };
    public StickSettings RightStick { get; init; } = new() { Target = StickTarget.RightStick };
    public TriggerSettings Triggers { get; init; } = new();
    public TouchpadSettings Touchpad { get; init; } = new();
    public LightbarSettings Lightbar { get; init; } = new();
    public RumbleSettings Rumble { get; init; } = new();

    [JsonIgnore]
    public static IReadOnlyList<DsButtons> MappableButtons { get; } =
    [
        DsButtons.Cross, DsButtons.Circle, DsButtons.Square, DsButtons.Triangle,
        DsButtons.L1, DsButtons.R1, DsButtons.L2, DsButtons.R2,
        DsButtons.Create, DsButtons.Options, DsButtons.PS,
        DsButtons.L3, DsButtons.R3,
        DsButtons.DpadUp, DsButtons.DpadDown, DsButtons.DpadLeft, DsButtons.DpadRight,
        DsButtons.TouchpadClick, DsButtons.Mute,
        DsButtons.LeftFn, DsButtons.RightFn, DsButtons.LeftPaddle, DsButtons.RightPaddle,
    ];

    /// <summary>The standard Xbox layout: every DualSense control sits on the Xbox control in the same position.</summary>
    public static Dictionary<DsButtons, ButtonAction> XboxLayout() => new()
    {
        [DsButtons.Cross] = ButtonAction.ToXbox(XboxControl.A),
        [DsButtons.Circle] = ButtonAction.ToXbox(XboxControl.B),
        [DsButtons.Square] = ButtonAction.ToXbox(XboxControl.X),
        [DsButtons.Triangle] = ButtonAction.ToXbox(XboxControl.Y),
        [DsButtons.L1] = ButtonAction.ToXbox(XboxControl.LeftBumper),
        [DsButtons.R1] = ButtonAction.ToXbox(XboxControl.RightBumper),
        [DsButtons.L2] = ButtonAction.ToXbox(XboxControl.LeftTrigger),
        [DsButtons.R2] = ButtonAction.ToXbox(XboxControl.RightTrigger),
        [DsButtons.Create] = ButtonAction.ToXbox(XboxControl.Back),
        [DsButtons.Options] = ButtonAction.ToXbox(XboxControl.Start),
        [DsButtons.PS] = ButtonAction.ToXbox(XboxControl.Guide),
        [DsButtons.L3] = ButtonAction.ToXbox(XboxControl.LeftThumb),
        [DsButtons.R3] = ButtonAction.ToXbox(XboxControl.RightThumb),
        [DsButtons.DpadUp] = ButtonAction.ToXbox(XboxControl.DpadUp),
        [DsButtons.DpadDown] = ButtonAction.ToXbox(XboxControl.DpadDown),
        [DsButtons.DpadLeft] = ButtonAction.ToXbox(XboxControl.DpadLeft),
        [DsButtons.DpadRight] = ButtonAction.ToXbox(XboxControl.DpadRight),
        [DsButtons.TouchpadClick] = ButtonAction.ToMouse(MouseAction.LeftClick),
        [DsButtons.Mute] = ButtonAction.None,
        [DsButtons.LeftFn] = ButtonAction.None,
        [DsButtons.RightFn] = ButtonAction.None,
        [DsButtons.LeftPaddle] = ButtonAction.None,
        [DsButtons.RightPaddle] = ButtonAction.None,
    };

    public static Profile CreateDefault(string name = "Xbox") => new() { Name = name, Buttons = XboxLayout() };

    public ButtonAction GetAction(DsButtons button) =>
        Buttons.TryGetValue(button, out var action) ? action : XboxLayout().GetValueOrDefault(button, ButtonAction.None);
}
