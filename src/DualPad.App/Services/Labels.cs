using System.Windows.Input;
using System.Windows.Media;
using DualPad.Core.DualSense;
using DualPad.Core.Mapping;

namespace DualPad.App.Services;

/// <summary>Italian display names and colors for controls and actions.</summary>
public static class Labels
{
    public static string Name(DsButtons button) => button switch
    {
        DsButtons.Cross => "Croce",
        DsButtons.Circle => "Cerchio",
        DsButtons.Square => "Quadrato",
        DsButtons.Triangle => "Triangolo",
        DsButtons.L1 => "L1",
        DsButtons.R1 => "R1",
        DsButtons.L2 => "L2 (grilletto)",
        DsButtons.R2 => "R2 (grilletto)",
        DsButtons.Create => "Crea",
        DsButtons.Options => "Options",
        DsButtons.L3 => "L3 (stick sinistro)",
        DsButtons.R3 => "R3 (stick destro)",
        DsButtons.PS => "Tasto PS",
        DsButtons.TouchpadClick => "Clic touchpad",
        DsButtons.Mute => "Microfono",
        DsButtons.DpadUp => "Croce direzionale su",
        DsButtons.DpadDown => "Croce direzionale giù",
        DsButtons.DpadLeft => "Croce direzionale sinistra",
        DsButtons.DpadRight => "Croce direzionale destra",
        DsButtons.LeftFn => "Fn sinistro (Edge)",
        DsButtons.RightFn => "Fn destro (Edge)",
        DsButtons.LeftPaddle => "Levetta posteriore sinistra (Edge)",
        DsButtons.RightPaddle => "Levetta posteriore destra (Edge)",
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
        DsButtons.Create => "Crea",
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
        XboxControl.LeftBumper => "Dorsale sinistro",
        XboxControl.RightBumper => "Dorsale destro",
        XboxControl.LeftTrigger => "Grilletto sinistro",
        XboxControl.RightTrigger => "Grilletto destro",
        XboxControl.Back => "Visualizza",
        XboxControl.Start => "Menu",
        XboxControl.Guide => "Pulsante Xbox",
        XboxControl.LeftThumb => "Pressione stick sinistro",
        XboxControl.RightThumb => "Pressione stick destro",
        XboxControl.DpadUp => "Croce su",
        XboxControl.DpadDown => "Croce giù",
        XboxControl.DpadLeft => "Croce sinistra",
        XboxControl.DpadRight => "Croce destra",
        _ => "Pulsante " + Name(control),
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
        MouseAction.LeftClick => "Clic sinistro",
        MouseAction.RightClick => "Clic destro",
        MouseAction.MiddleClick => "Clic centrale",
        MouseAction.Back => "Indietro",
        MouseAction.Forward => "Avanti",
        MouseAction.WheelUp => "Rotella su",
        MouseAction.WheelDown => "Rotella giù",
        _ => action.ToString(),
    };

    public static string Short(MouseAction action) => action switch
    {
        MouseAction.LeftClick => "Clic sx",
        MouseAction.RightClick => "Clic dx",
        MouseAction.MiddleClick => "Clic cent.",
        MouseAction.WheelUp => "Rotella ↑",
        MouseAction.WheelDown => "Rotella ↓",
        _ => Name(action),
    };

    public static string KeyName(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key switch
        {
            Key.Space => "Spazio",
            Key.Enter => "Invio",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.LeftShift => "Shift sx",
            Key.RightShift => "Shift dx",
            Key.LeftCtrl => "Ctrl sx",
            Key.RightCtrl => "Ctrl dx",
            Key.LeftAlt => "Alt",
            Key.RightAlt => "Alt Gr",
            Key.LWin or Key.RWin => "Win",
            Key.Up => "Freccia ↑",
            Key.Down => "Freccia ↓",
            Key.Left => "Freccia ←",
            Key.Right => "Freccia →",
            Key.PageUp => "Pag ↑",
            Key.PageDown => "Pag ↓",
            Key.Delete => "Canc",
            Key.Insert => "Ins",
            Key.Home => "Home",
            Key.End => "Fine",
            Key.Capital => "Bloc Maiusc",
            Key.PrintScreen => "Stamp",
            Key.MediaPlayPause => "Play/Pausa",
            Key.MediaNextTrack => "Traccia succ.",
            Key.MediaPreviousTrack => "Traccia prec.",
            Key.VolumeUp => "Volume +",
            Key.VolumeDown => "Volume −",
            Key.VolumeMute => "Muto",
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (key - Key.NumPad0),
            Key.None => $"Tasto {virtualKey}",
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
        ActionKind.Xbox => "Xbox · " + Description(action.Xbox),
        ActionKind.Keyboard => "Tastiera · " + KeyName(action.Key),
        ActionKind.Mouse => "Mouse · " + Name(action.Mouse),
        _ => "Nessuna azione",
    };

    public static Color BadgeColor(ButtonAction action) => action.Kind switch
    {
        ActionKind.Xbox => XboxColor(action.Xbox),
        ActionKind.Keyboard => Color.FromRgb(0x8B, 0x5C, 0xF6),
        ActionKind.Mouse => Color.FromRgb(0x0E, 0xA5, 0xA5),
        _ => Color.FromRgb(0x44, 0x47, 0x50),
    };
}
