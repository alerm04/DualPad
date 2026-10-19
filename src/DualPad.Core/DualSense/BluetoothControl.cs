using System.Globalization;
using System.Runtime.InteropServices;

namespace DualPad.Core.DualSense;

/// <summary>
/// Turns a Bluetooth controller off by asking the local radio to drop the link
/// (IOCTL_BTH_DISCONNECT_DEVICE). The DualSense powers down when its connection is closed.
/// </summary>
public static class BluetoothControl
{
    private const uint IOCTL_BTH_DISCONNECT_DEVICE = 0x41000C;

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public int dwSize;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS parameters, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(IntPtr device, uint code, ref ulong input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>Parses a MAC address such as "0c27568e6df3" or "0C:27:56:8E:6D:F3".</summary>
    public static bool TryParseAddress(string? mac, out ulong address)
    {
        address = 0;
        if (mac is null) return false;
        var hex = new string(mac.Where(Uri.IsHexDigit).ToArray());
        return hex.Length == 12 && ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
    }

    /// <summary>Disconnects (and so powers off) the Bluetooth device with the given MAC address.</summary>
    public static bool Disconnect(string mac)
    {
        if (!TryParseAddress(mac, out var address)) return false;

        var parameters = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>() };
        var find = BluetoothFindFirstRadio(ref parameters, out var radio);
        if (find == IntPtr.Zero) return false;

        var success = false;
        try
        {
            do
            {
                var target = address;
                success |= DeviceIoControl(radio, IOCTL_BTH_DISCONNECT_DEVICE, ref target, sizeof(ulong), IntPtr.Zero, 0, out _, IntPtr.Zero);
                CloseHandle(radio);
            }
            while (!success && BluetoothFindNextRadio(find, out radio));
        }
        finally
        {
            BluetoothFindRadioClose(find);
        }
        return success;
    }
}
