using System.Runtime.InteropServices;
using DualPad.Core.Mapping;

namespace DualPad.Core.Input;

/// <summary>Keyboard and mouse injection through SendInput.</summary>
public interface IInputSink
{
    void MoveMouse(int dx, int dy);
    void Wheel(int delta);
    void WheelRaw(int amount);
    void MouseButton(MouseAction button, bool down);
    void Key(int virtualKey, bool down);
}

public sealed class InputSimulator : IInputSink
{
    public static InputSimulator Instance { get; } = new();

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080, MOUSEEVENTF_XUP = 0x0100;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;
    private const int WHEEL_DELTA = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public int mouseData;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    public void MoveMouse(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        SendMouse(MOUSEEVENTF_MOVE, dx, dy, 0);
    }

    /// <summary>Scrolls by <paramref name="delta"/> notches (positive = up).</summary>
    public void Wheel(int delta)
    {
        if (delta != 0) SendMouse(MOUSEEVENTF_WHEEL, 0, 0, delta * WHEEL_DELTA);
    }

    /// <summary>Scrolls by a raw wheel amount (120 = one notch) for smooth scrolling.</summary>
    public void WheelRaw(int amount)
    {
        if (amount != 0) SendMouse(MOUSEEVENTF_WHEEL, 0, 0, amount);
    }

    public void MouseButton(MouseAction button, bool down)
    {
        switch (button)
        {
            case MouseAction.LeftClick: SendMouse(down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP, 0, 0, 0); break;
            case MouseAction.RightClick: SendMouse(down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP, 0, 0, 0); break;
            case MouseAction.MiddleClick: SendMouse(down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP, 0, 0, 0); break;
            case MouseAction.Back: SendMouse(down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP, 0, 0, 1); break;
            case MouseAction.Forward: SendMouse(down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP, 0, 0, 2); break;
            case MouseAction.WheelUp: if (down) Wheel(1); break;
            case MouseAction.WheelDown: if (down) Wheel(-1); break;
        }
    }

    public void Key(int virtualKey, bool down)
    {
        var scan = (ushort)MapVirtualKey((uint)virtualKey, 0);
        var flags = KEYEVENTF_SCANCODE | (down ? 0 : KEYEVENTF_KEYUP);
        if (IsExtended(virtualKey)) flags |= KEYEVENTF_EXTENDEDKEY;
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)virtualKey, wScan = scan, dwFlags = flags } },
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static void SendMouse(uint flags, int dx, int dy, int data)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags } },
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static bool IsExtended(int vk) => vk is
        0x21 or 0x22 or 0x23 or 0x24 // PageUp, PageDown, End, Home
        or 0x25 or 0x26 or 0x27 or 0x28 // arrows
        or 0x2D or 0x2E // Insert, Delete
        or 0x5B or 0x5C or 0x5D // Win keys, Apps
        or 0x6F // Divide
        or 0xA3 or 0xA5 // RControl, RMenu
        or 0x90; // NumLock
}
