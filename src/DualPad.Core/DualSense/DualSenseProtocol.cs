using System.Buffers.Binary;

namespace DualPad.Core.DualSense;

/// <summary>
/// DualSense report layouts. Offsets match the full input report used by DS4Windows
/// (USB report 0x01 with data at byte 1, Bluetooth report 0x31 with data at byte 2).
/// </summary>
public static class DualSenseProtocol
{
    public const ushort SonyVendorId = 0x054C;
    public const ushort DualSenseProductId = 0x0CE6;
    public const ushort DualSenseEdgeProductId = 0x0DF2;

    public const byte UsbInputReportId = 0x01;
    public const byte BtInputReportId = 0x31;
    public const byte UsbOutputReportId = 0x02;
    public const byte BtOutputReportId = 0x31;
    public const byte CalibrationFeatureId = 0x05;
    public const byte FirmwareFeatureId = 0x20;

    public const int UsbOutputLength = 48;
    public const int BtOutputLength = 78;

    public static bool IsDualSense(ushort vendorId, ushort productId) =>
        vendorId == SonyVendorId && productId is DualSenseProductId or DualSenseEdgeProductId;

    /// <summary>Decodes an input report. Returns false for reports that do not carry full state (e.g. the reduced Bluetooth report).</summary>
    public static bool TryParseInput(ReadOnlySpan<byte> report, ConnectionType connection, out DualSenseState state)
    {
        state = default;
        int offset;
        if (connection == ConnectionType.Bluetooth)
        {
            if (report.Length < 78 || report[0] != BtInputReportId) return false;
            offset = 2;
        }
        else
        {
            if (report.Length < 64 || report[0] != UsbInputReportId) return false;
            offset = 1;
        }

        var d = report[offset..];
        state.LeftX = d[0];
        state.LeftY = d[1];
        state.RightX = d[2];
        state.RightY = d[3];
        state.L2 = d[4];
        state.R2 = d[5];

        byte b0 = d[7], b1 = d[8], b2 = d[9];
        var buttons = DsButtons.None;
        buttons |= DecodeDpad(b0 & 0x0F);
        if ((b0 & 0x10) != 0) buttons |= DsButtons.Square;
        if ((b0 & 0x20) != 0) buttons |= DsButtons.Cross;
        if ((b0 & 0x40) != 0) buttons |= DsButtons.Circle;
        if ((b0 & 0x80) != 0) buttons |= DsButtons.Triangle;
        if ((b1 & 0x01) != 0) buttons |= DsButtons.L1;
        if ((b1 & 0x02) != 0) buttons |= DsButtons.R1;
        if ((b1 & 0x04) != 0) buttons |= DsButtons.L2;
        if ((b1 & 0x08) != 0) buttons |= DsButtons.R2;
        if ((b1 & 0x10) != 0) buttons |= DsButtons.Create;
        if ((b1 & 0x20) != 0) buttons |= DsButtons.Options;
        if ((b1 & 0x40) != 0) buttons |= DsButtons.L3;
        if ((b1 & 0x80) != 0) buttons |= DsButtons.R3;
        if ((b2 & 0x01) != 0) buttons |= DsButtons.PS;
        if ((b2 & 0x02) != 0) buttons |= DsButtons.TouchpadClick;
        if ((b2 & 0x04) != 0) buttons |= DsButtons.Mute;
        if ((b2 & 0x10) != 0) buttons |= DsButtons.LeftFn;
        if ((b2 & 0x20) != 0) buttons |= DsButtons.RightFn;
        if ((b2 & 0x40) != 0) buttons |= DsButtons.LeftPaddle;
        if ((b2 & 0x80) != 0) buttons |= DsButtons.RightPaddle;
        state.Buttons = buttons;

        state.GyroX = BinaryPrimitives.ReadInt16LittleEndian(d[15..]);
        state.GyroY = BinaryPrimitives.ReadInt16LittleEndian(d[17..]);
        state.GyroZ = BinaryPrimitives.ReadInt16LittleEndian(d[19..]);
        state.AccelX = BinaryPrimitives.ReadInt16LittleEndian(d[21..]);
        state.AccelY = BinaryPrimitives.ReadInt16LittleEndian(d[23..]);
        state.AccelZ = BinaryPrimitives.ReadInt16LittleEndian(d[25..]);

        state.Touch0 = DecodeTouch(d.Slice(32, 4));
        state.Touch1 = DecodeTouch(d.Slice(36, 4));

        var status = d[52];
        state.Battery = Math.Min(100, (status & 0x0F) * 10 + 5);
        state.Charge = (status >> 4) switch
        {
            0x0 => ChargeState.Discharging,
            0x1 => ChargeState.Charging,
            0x2 => ChargeState.Full,
            _ => ChargeState.Unknown,
        };
        if (state.Charge == ChargeState.Full) state.Battery = 100;
        return true;
    }

    private static DsButtons DecodeDpad(int hat) => hat switch
    {
        0 => DsButtons.DpadUp,
        1 => DsButtons.DpadUp | DsButtons.DpadRight,
        2 => DsButtons.DpadRight,
        3 => DsButtons.DpadDown | DsButtons.DpadRight,
        4 => DsButtons.DpadDown,
        5 => DsButtons.DpadDown | DsButtons.DpadLeft,
        6 => DsButtons.DpadLeft,
        7 => DsButtons.DpadUp | DsButtons.DpadLeft,
        _ => DsButtons.None,
    };

    private static TouchPoint DecodeTouch(ReadOnlySpan<byte> t)
    {
        // Bit 7 of the first byte is set while the finger is NOT touching.
        var active = (t[0] & 0x80) == 0;
        var id = (byte)(t[0] & 0x7F);
        var x = t[1] | ((t[2] & 0x0F) << 8);
        var y = (t[2] >> 4) | (t[3] << 4);
        return new TouchPoint(active, id, x, y);
    }
}

/// <summary>Values written to the controller: rumble, lightbar and LEDs.</summary>
public struct DualSenseOutput
{
    public byte RumbleLeft;   // heavy / low frequency motor
    public byte RumbleRight;  // light / high frequency motor
    public byte LightR, LightG, LightB;
    /// <summary>Player LED bar: 5 bits, bit 0 = leftmost.</summary>
    public byte PlayerLeds;
    /// <summary>0 = high, 1 = medium, 2 = low.</summary>
    public byte LedBrightness;
    public bool MuteLed;
}

public static class DualSenseOutputBuilder
{
    // Player LED patterns that match the PS5 console for players 1..4.
    public static readonly byte[] PlayerPatterns = [0x04, 0x0A, 0x15, 0x1B, 0x1F];

    /// <summary>Builds a complete output report for the given connection.</summary>
    /// <param name="sequence">Bluetooth sequence number, 0..15.</param>
    /// <param name="reportLength">Length expected by the HID driver; at least the protocol length.</param>
    public static byte[] Build(in DualSenseOutput output, ConnectionType connection, byte sequence, int reportLength = 0)
    {
        byte[] report;
        int c; // start of the common block
        if (connection == ConnectionType.Bluetooth)
        {
            report = new byte[Math.Max(DualSenseProtocol.BtOutputLength, reportLength)];
            report[0] = DualSenseProtocol.BtOutputReportId;
            report[1] = (byte)((sequence & 0x0F) << 4);
            report[2] = 0x10; // tag
            c = 3;
        }
        else
        {
            report = new byte[Math.Max(DualSenseProtocol.UsbOutputLength, reportLength)];
            report[0] = DualSenseProtocol.UsbOutputReportId;
            c = 1;
        }

        // valid_flag0: compatible vibration + haptics select (rumble emulation on the voice-coil actuators).
        report[c + 0] = 0x01 | 0x02;
        // valid_flag1: mic mute LED, lightbar, player indicator.
        report[c + 1] = 0x01 | 0x04 | 0x10;
        report[c + 2] = output.RumbleRight;
        report[c + 3] = output.RumbleLeft;
        report[c + 8] = output.MuteLed ? (byte)1 : (byte)0;
        // valid_flag2: lightbar setup control + compatible vibration v2 (newer firmware).
        report[c + 38] = 0x02 | 0x04;
        report[c + 41] = 0x02; // lightbar setup: fade out the blue boot animation so our color shows
        report[c + 42] = output.LedBrightness;
        report[c + 43] = (byte)(output.PlayerLeds & 0x1F);
        report[c + 44] = output.LightR;
        report[c + 45] = output.LightG;
        report[c + 46] = output.LightB;

        if (connection == ConnectionType.Bluetooth)
        {
            var crc = Crc32.Compute(Crc32.OutputSeed, report.AsSpan(0, 74));
            BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(74), crc);
        }

        return report;
    }
}
