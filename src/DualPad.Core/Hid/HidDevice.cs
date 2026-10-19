using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DualPad.Core.Hid;

/// <summary>Information about a HID interface found during enumeration.</summary>
public sealed record HidDeviceInfo(string Path, ushort VendorId, ushort ProductId, ushort InputReportLength, ushort OutputReportLength, ushort FeatureReportLength)
{
    /// <summary>True when the interface is reached through the Bluetooth HID profile.</summary>
    public bool IsBluetooth => Path.Contains("00001124-0000-1000-8000-00805f9b34fb", StringComparison.OrdinalIgnoreCase);

    /// <summary>Device instance id derived from the interface path, e.g. HID\VID_054C&amp;PID_0CE6\7&amp;...&amp;0000.</summary>
    public string InstanceId
    {
        get
        {
            var p = Path;
            if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) p = p[4..];
            var lastHash = p.LastIndexOf('#');
            if (lastHash > 0 && p.IndexOf('{', lastHash) == lastHash + 1) p = p[..lastHash];
            return p.Replace('#', '\\').ToUpperInvariant();
        }
    }
}

/// <summary>
/// An open HID device. Reads use an overlapped handle so they can be cancelled; writes go through a
/// second synchronous handle so input and output never contend for the same stream.
/// </summary>
public sealed class HidDevice : IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly SafeFileHandle _writeHandle;
    private readonly FileStream _readStream;
    private readonly FileStream _writeStream;

    public HidDeviceInfo Info { get; }

    private HidDevice(SafeFileHandle handle, SafeFileHandle writeHandle, HidDeviceInfo info)
    {
        _handle = handle;
        _writeHandle = writeHandle;
        _readStream = new FileStream(handle, FileAccess.ReadWrite, 0, isAsync: true);
        _writeStream = new FileStream(writeHandle, FileAccess.ReadWrite, 0, isAsync: false);
        Info = info;
    }

    public static IReadOnlyList<HidDeviceInfo> Enumerate(Func<ushort, ushort, bool> filter)
    {
        var result = new List<HidDeviceInfo>();
        NativeMethods.HidD_GetHidGuid(out var hidGuid);
        var set = NativeMethods.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return result;

        try
        {
            var data = new NativeMethods.SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>() };
            for (var i = 0; NativeMethods.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data); i++)
            {
                var path = GetInterfacePath(set, ref data);
                if (path is null) continue;

                // Open without access rights: enough to query attributes even when another app owns the device.
                using var probe = NativeMethods.CreateFile(path, 0, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
                if (probe.IsInvalid) continue;

                var attributes = new NativeMethods.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<NativeMethods.HIDD_ATTRIBUTES>() };
                if (!NativeMethods.HidD_GetAttributes(probe, ref attributes)) continue;
                if (!filter(attributes.VendorID, attributes.ProductID)) continue;

                var caps = GetCaps(probe);
                if (caps is null) continue;

                result.Add(new HidDeviceInfo(path, attributes.VendorID, attributes.ProductID,
                    caps.Value.InputReportByteLength, caps.Value.OutputReportByteLength, caps.Value.FeatureReportByteLength));
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(set);
        }

        return result;
    }

    /// <summary>Opens the device for reading and writing. Returns null when it is held exclusively by another process.</summary>
    public static HidDevice? TryOpen(HidDeviceInfo info)
    {
        var handle = NativeMethods.CreateFile(info.Path, NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        var writeHandle = NativeMethods.CreateFile(info.Path, NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
        if (writeHandle.IsInvalid)
        {
            writeHandle.Dispose();
            handle.Dispose();
            return null;
        }

        NativeMethods.HidD_SetNumInputBuffers(handle, 3);
        return new HidDevice(handle, writeHandle, info);
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        _readStream.ReadAsync(buffer, cancellationToken);

    /// <summary>Writes an output report, falling back to HidD_SetOutputReport when WriteFile is rejected.</summary>
    public bool Write(byte[] report)
    {
        try
        {
            _writeStream.Write(report, 0, report.Length);
            return true;
        }
        catch (IOException)
        {
            return NativeMethods.HidD_SetOutputReport(_writeHandle, report, report.Length);
        }
    }

    /// <summary>Reads a feature report; buffer[0] must contain the report id.</summary>
    public bool GetFeature(byte[] buffer) => NativeMethods.HidD_GetFeature(_handle, buffer, buffer.Length);

    public string? GetSerialNumber()
    {
        var buffer = new byte[256];
        if (!NativeMethods.HidD_GetSerialNumberString(_handle, buffer, buffer.Length)) return null;
        var text = System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        return text.Length == 0 ? null : text;
    }

    public void Dispose()
    {
        _readStream.Dispose();
        _writeStream.Dispose();
        _handle.Dispose();
        _writeHandle.Dispose();
    }

    private static string? GetInterfacePath(IntPtr set, ref NativeMethods.SP_DEVICE_INTERFACE_DATA data)
    {
        NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
        if (required <= 0) return null;

        var detail = Marshal.AllocHGlobal(required);
        try
        {
            // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on x64, 6 on x86.
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
            if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out _, IntPtr.Zero)) return null;
            return Marshal.PtrToStringUni(detail + 4);
        }
        finally
        {
            Marshal.FreeHGlobal(detail);
        }
    }

    private static NativeMethods.HIDP_CAPS? GetCaps(SafeFileHandle handle)
    {
        if (!NativeMethods.HidD_GetPreparsedData(handle, out var preparsed)) return null;
        try
        {
            var caps = new NativeMethods.HIDP_CAPS { Reserved = new ushort[17] };
            return NativeMethods.HidP_GetCaps(preparsed, ref caps) == NativeMethods.HIDP_STATUS_SUCCESS ? caps : null;
        }
        finally
        {
            NativeMethods.HidD_FreePreparsedData(preparsed);
        }
    }
}
