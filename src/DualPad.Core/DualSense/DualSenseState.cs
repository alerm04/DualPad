namespace DualPad.Core.DualSense;

/// <summary>Physical controls of a DualSense controller.</summary>
[Flags]
public enum DsButtons : uint
{
    None = 0,
    Cross = 1 << 0,
    Circle = 1 << 1,
    Square = 1 << 2,
    Triangle = 1 << 3,
    L1 = 1 << 4,
    R1 = 1 << 5,
    L2 = 1 << 6,
    R2 = 1 << 7,
    Create = 1 << 8,
    Options = 1 << 9,
    L3 = 1 << 10,
    R3 = 1 << 11,
    PS = 1 << 12,
    TouchpadClick = 1 << 13,
    Mute = 1 << 14,
    DpadUp = 1 << 15,
    DpadDown = 1 << 16,
    DpadLeft = 1 << 17,
    DpadRight = 1 << 18,
    // DualSense Edge extra controls.
    LeftFn = 1 << 19,
    RightFn = 1 << 20,
    LeftPaddle = 1 << 21,
    RightPaddle = 1 << 22,
}

public enum ConnectionType { Usb, Bluetooth }

public enum ChargeState { Discharging, Charging, Full, Unknown }

public readonly record struct TouchPoint(bool Active, byte Id, int X, int Y);

/// <summary>One decoded input report.</summary>
public struct DualSenseState
{
    public const int TouchpadWidth = 1920;
    public const int TouchpadHeight = 1080;

    public byte LeftX, LeftY, RightX, RightY;
    public byte L2, R2;
    public DsButtons Buttons;
    public TouchPoint Touch0, Touch1;
    public short GyroX, GyroY, GyroZ;
    public short AccelX, AccelY, AccelZ;
    /// <summary>Battery level 0..100.</summary>
    public int Battery;
    public ChargeState Charge;

    public readonly bool IsPressed(DsButtons button) => (Buttons & button) != 0;
}
