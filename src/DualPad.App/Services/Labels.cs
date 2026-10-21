using System.Windows.Input;
using System.Windows.Media;
using DualPad.Core.DualSense;
using DualPad.Core.Mapping;

namespace DualPad.App.Services;

/// <summary>Display names (translated through <see cref="Loc"/>) and colors for controls and actions.</summary>
public static class Labels
{
    public static string Name(DsButtons button) => button switch
    {
        DsButtons.Cross => Loc.T("ds.cross"),
        DsButtons.Circle => Loc.T("ds.circle"),
        DsButtons.Square => Loc.T("ds.square"),
        DsButtons.Triangle => Loc.T("ds.triangle"),
        DsButtons.L1 => "L1",
        DsButtons.R1 => "R1",
        DsButtons.L2 => Loc.T("ds.l2"),
        DsButtons.R2 => Loc.T("ds.r2"),
        DsButtons.Create => Loc.T("ds.create"),
        DsButtons.Options => "Options",
        DsButtons.L3 => Loc.T("ds.l3"),
        DsButtons.R3 => Loc.T("ds.r3"),
        DsButtons.PS => Loc.T("ds.ps"),
        DsButtons.TouchpadClick => Loc.T("ds.touchpad"),
        DsButtons.Mute => Loc.T("ds.mute"),
        DsButtons.DpadUp => Loc.T("ds.dpadUp"),
        DsButtons.DpadDown => Loc.T("ds.dpadDown"),
        DsButtons.DpadLeft => Loc.T("ds.dpadLeft"),
        DsButtons.DpadRight => Loc.T("ds.dpadRight"),
        DsButtons.LeftFn => Loc.T("ds.leftFn"),
        DsButtons.RightFn => Loc.T("ds.rightFn"),
        DsButtons.LeftPaddle => Loc.T("ds.leftPaddle"),
        DsButtons.RightPaddle => Loc.T("ds.rightPaddle"),
        _ => button.ToString(),
    };

    public static string Glyph(DsButtons button) => button switch
    {
        DsButtons.Cross => "✕",
        DsButtons.Circle => "○",
        DsButtons.Square => "□",
        DsButtons.Triangle => "△",
        DsButtons.DpadUp => "↑",
        DsButtons.DpadDown => "↓",
        DsButtons.DpadLeft => "←",
        DsButtons.DpadRight => "→",
        DsButtons.L3 => "L3",
        DsButtons.R3 => "R3",
        DsButtons.L2 => "L2",
        DsButtons.R2 => "R2",
        DsButtons.PS => "PS",
        DsButtons.TouchpadClick => "TP",
        DsButtons.Mute => "🎙",
        DsButtons.Create => Loc.T("ds.create.short"),
        DsButtons.Options => "Opt",
        DsButtons.LeftFn => "Fn",
        DsButtons.RightFn => "Fn",
        DsButtons.LeftPaddle => "LB",
        DsButtons.RightPaddle => "RB",
        _ => button.ToString(),
    };

    public static Color GlyphColor(DsButtons button) => button switch
    {
        DsButtons.Cross => Color.FromRgb(0x7C, 0xB2, 0xE8),
        DsButtons.Circle => Color.FromRgb(0xFF, 0x66, 0x6D),
        DsButtons.Square => Color.FromRgb(0xF2, 0x8C, 0xCB),
        DsButtons.Triangle => Color.FromRgb(0x3F, 0xD1, 0xA6),
        _ => Color.FromRgb(0xE6, 0xE6, 0xE6),
    };

    public static string Name(XboxControl control) => control switch
    {
        XboxControl.A => "A",
        XboxControl.B => "B",
        XboxControl.X => "X",
        XboxControl.Y => "Y",
        XboxControl.LeftBumper => "LB",
        XboxControl.RightBumper => "RB",
        XboxControl.LeftTrigger => "LT",
        XboxControl.RightTrigger => "RT",
        XboxControl.Back => "View",
        XboxControl.Start => "Menu",
        XboxControl.Guide => "Xbox",
        XboxControl.LeftThumb => "LS",
        XboxControl.RightThumb => "RS",
        XboxControl.DpadUp => "↑",
        XboxControl.DpadDown => "↓",
        XboxControl.DpadLeft => "←",
        XboxControl.DpadRight => "→",
        _ => control.ToString(),
    };

    public static string Description(XboxControl control) => control switch
    {
        XboxControl.LeftBumper => Loc.T("xbox.lb"),
        XboxControl.RightBumper => Loc.T("xbox.rb"),
        XboxControl.LeftTrigger => Loc.T("xbox.lt"),
        XboxControl.RightTrigger => Loc.T("xbox.rt"),
        XboxControl.Back => Loc.T("xbox.back"),
        XboxControl.Start => Loc.T("xbox.start"),
        XboxControl.Guide => Loc.T("xbox.guide"),
        XboxControl.LeftThumb => Loc.T("xbox.ls"),
        XboxControl.RightThumb => Loc.T("xbox.rs"),
        XboxControl.DpadUp => Loc.T("xbox.dpadUp"),
        XboxControl.DpadDown => Loc.T("xbox.dpadDown"),
        XboxControl.DpadLeft => Loc.T("xbox.dpadLeft"),
        XboxControl.DpadRight => Loc.T("xbox.dpadRight"),
        _ => Loc.T("xbox.button", Name(control)),
    };

    public static Color XboxColor(XboxControl control) => control switch
    {
        XboxControl.A => Color.FromRgb(0x5D, 0xC2, 0x1E),
        XboxControl.B => Color.FromRgb(0xE8, 0x3A, 0x3A),
        XboxControl.X => Color.FromRgb(0x2E, 0x8B, 0xFF),
        XboxControl.Y => Color.FromRgb(0xF2, 0xC2, 0x00),
        XboxControl.Guide => Color.FromRgb(0x10, 0x7C, 0x10),
        _ => Color.FromRgb(0x5A, 0x5F, 0x6A),
    };

    public static string Name(MouseAction action) => action switch
    {
        MouseAction.LeftClick => Loc.T("mouse.left"),
        MouseAction.RightClick => Loc.T("mouse.right"),
        MouseAction.MiddleClick => Loc.T("mouse.middle"),
        MouseAction.Back => Loc.T("mouse.back"),
        MouseAction.Forward => Loc.T("mouse.forward"),
        MouseAction.WheelUp => Loc.T("mouse.wheelUp"),
        MouseAction.WheelDown => Loc.T("mouse.wheelDown"),
        _ => action.ToString(),
    };

    public static string Short(MouseAction action) => action switch
    {
        MouseAction.LeftClick => Loc.T("mouse.short.left"),
        MouseAction.RightClick => Loc.T("mouse.short.right"),
        MouseAction.MiddleClick => Loc.T("mouse.short.middle"),
        MouseAction.WheelUp => Loc.T("mouse.short.wheelUp"),
        MouseAction.WheelDown => Loc.T("mouse.short.wheelDown"),
        _ => Name(action),
    };

    public static string KeyName(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key switch
        {
            Key.Space => Loc.T("key.space"),
            Key.Enter => Loc.T("key.enter"),
            Key.Escape => "Esc",
            Key.Back => Loc.T("key.backspace"),
            Key.Tab => "Tab",
            Key.LeftShift => Loc.T("key.lshift"),
            Key.RightShift => Loc.T("key.rshift"),
            Key.LeftCtrl => Loc.T("key.lctrl"),
            Key.RightCtrl => Loc.T("key.rctrl"),
            Key.LeftAlt => "Alt",
            Key.RightAlt => Loc.T("key.altgr"),
            Key.LWin or Key.RWin => "Win",
            Key.Up => Loc.T("key.up"),
            Key.Down => Loc.T("key.down"),
            Key.Left => Loc.T("key.left"),
            Key.Right => Loc.T("key.right"),
            Key.PageUp => Loc.T("key.pageUp"),
            Key.PageDown => Loc.T("key.pageDown"),
            Key.Delete => Loc.T("key.delete"),
            Key.Insert => Loc.T("key.insert"),
            Key.Home => Loc.T("key.home"),
            Key.End => Loc.T("key.end"),
            Key.Capital => Loc.T("key.capsLock"),
            Key.PrintScreen => Loc.T("key.printScreen"),
            Key.MediaPlayPause => Loc.T("key.playPause"),
            Key.MediaNextTrack => Loc.T("key.nextTrack"),
            Key.MediaPreviousTrack => Loc.T("key.prevTrack"),
            Key.VolumeUp => Loc.T("key.volumeUp"),
            Key.VolumeDown => Loc.T("key.volumeDown"),
            Key.VolumeMute => Loc.T("key.volumeMute"),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => Loc.T("key.numpad", key - Key.NumPad0),
            Key.None => Loc.T("key.code", virtualKey),
            _ => key.ToString(),
        };
    }

    /// <summary>Short text for badges on the controller drawing.</summary>
    public static string Short(ButtonAction action) => action.Kind switch
    {
        ActionKind.Xbox => Name(action.Xbox),
        ActionKind.Keyboard => KeyName(action.Key),
        ActionKind.Mouse => Short(action.Mouse),
        _ => "—",
    };

    public static string Long(ButtonAction action) => action.Kind switch
    {
        ActionKind.Xbox => Loc.T("action.xbox", Description(action.Xbox)),
        ActionKind.Keyboard => Loc.T("action.keyboard", KeyName(action.Key)),
        ActionKind.Mouse => Loc.T("action.mouse", Name(action.Mouse)),
        _ => Loc.T("action.none"),
    };

    public static Color BadgeColor(ButtonAction action) => action.Kind switch
    {
        ActionKind.Xbox => XboxColor(action.Xbox),
        ActionKind.Keyboard => Color.FromRgb(0x8B, 0x5C, 0xF6),
        ActionKind.Mouse => Color.FromRgb(0x0E, 0xA5, 0xA5),
        _ => Color.FromRgb(0x44, 0x47, 0x50),
    };
}
