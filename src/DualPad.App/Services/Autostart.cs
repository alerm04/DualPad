using Microsoft.Win32;

namespace DualPad.App.Services;

/// <summary>Registers DualPad under HKCU\...\Run so it starts with Windows.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DualPad";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe) key.SetValue(ValueName, $"\"{exe}\" --minimized");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
