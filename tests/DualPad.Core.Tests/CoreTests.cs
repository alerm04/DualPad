using System.Buffers.Binary;
using DualPad.Core.DualSense;
using DualPad.Core.Input;
using DualPad.Core.Mapping;
using DualPad.Core.Profiles;

namespace DualPad.Core.Tests;

internal sealed class RecordingSink : IInputSink
{
    public readonly List<string> Events = [];
    public int MovedX, MovedY, WheelTotal;

    public void MoveMouse(int dx, int dy) { MovedX += dx; MovedY += dy; }
    public void Wheel(int delta) => WheelTotal += delta * 120;
    public void WheelRaw(int amount) => WheelTotal += amount;
    public void MouseButton(MouseAction button, bool down) => Events.Add($"{button}:{(down ? "down" : "up")}");
    public void Key(int virtualKey, bool down) => Events.Add($"key{virtualKey}:{(down ? "down" : "up")}");
}

public class ProtocolTests
{
    private static byte[] UsbReport(Action<Span<byte>> fill)
    {
        var report = new byte[64];
        report[0] = 0x01;
        var d = report.AsSpan(1);
        d[0] = d[1] = d[2] = d[3] = 128;
        d[7] = 0x08; // dpad released
        d[32] = 0x80; // touch 0 inactive
        d[36] = 0x80; // touch 1 inactive
        fill(d);
        return report;
    }

    [Fact]
    public void Crc32_MatchesStandardCheckValue()
    {
        // CRC-32/ISO-HDLC of "123456789" is 0xCBF43926; with a seed byte we hash seed + data.
        var data = "123456789"u8.ToArray();
        Assert.Equal(0xCBF43926u, Crc32.Compute(data[0], data.AsSpan(1)));
    }

    [Fact]
    public void ParsesButtonsSticksAndTriggers()
    {
        var report = UsbReport(d =>
        {
            d[0] = 0; d[1] = 255; d[4] = 77; d[5] = 200;
            d[7] = 0x20 | 0x02; // cross + dpad right
            d[8] = 0x01 | 0x20; // L1 + options
            d[9] = 0x01 | 0x04; // PS + mute
            d[52] = 0x15; // charging, level 5
        });

        Assert.True(DualSenseProtocol.TryParseInput(report, ConnectionType.Usb, out var s));
        Assert.Equal(0, s.LeftX);
        Assert.Equal(255, s.LeftY);
        Assert.Equal(77, s.L2);
        Assert.Equal(200, s.R2);
        Assert.Equal(DsButtons.Cross | DsButtons.DpadRight | DsButtons.L1 | DsButtons.Options | DsButtons.PS | DsButtons.Mute, s.Buttons);
        Assert.Equal(55, s.Battery);
        Assert.Equal(ChargeState.Charging, s.Charge);
    }

    [Fact]
    public void ParsesTouchCoordinates()
    {
        var report = UsbReport(d =>
        {
            // id 3 active at x=1500 (0x5DC), y=700 (0x2BC)
            d[32] = 0x03;
            d[33] = 0xDC;
            d[34] = 0x05 | (0x0C << 4);
            d[35] = 0x2B;
        });

        Assert.True(DualSenseProtocol.TryParseInput(report, ConnectionType.Usb, out var s));
        Assert.Equal(new TouchPoint(true, 3, 1500, 700), s.Touch0);
        Assert.False(s.Touch1.Active);
    }

    [Fact]
    public void ParsesBluetoothReportWithTwoByteHeader()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[2 + 7] = 0x80 | 0x08; // triangle, dpad released
        report[2 + 32] = report[2 + 36] = 0x80;
        Assert.True(DualSenseProtocol.TryParseInput(report, ConnectionType.Bluetooth, out var s));
        Assert.Equal(DsButtons.Triangle, s.Buttons);
    }

    [Fact]
    public void RejectsReducedBluetoothReport()
    {
        var report = new byte[78];
        report[0] = 0x01;
        Assert.False(DualSenseProtocol.TryParseInput(report, ConnectionType.Bluetooth, out _));
    }

    [Fact]
    public void BluetoothOutputCarriesValidCrcAndValues()
    {
        var output = new DualSenseOutput { RumbleLeft = 10, RumbleRight = 20, LightR = 1, LightG = 2, LightB = 3, PlayerLeds = 0x04 };
        var report = DualSenseOutputBuilder.Build(output, ConnectionType.Bluetooth, 5);

        Assert.Equal(78, report.Length);
        Assert.Equal(0x31, report[0]);
        Assert.Equal(0x50, report[1]);
        Assert.Equal(20, report[3 + 2]);
        Assert.Equal(10, report[3 + 3]);
        Assert.Equal([1, 2, 3], report[(3 + 44)..(3 + 47)]);
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(report.AsSpan(74));
        Assert.Equal(Crc32.Compute(Crc32.OutputSeed, report.AsSpan(0, 74)), crc);
    }

    [Fact]
    public void UsbOutputUsesReport2()
    {
        var report = DualSenseOutputBuilder.Build(new DualSenseOutput { LightB = 9 }, ConnectionType.Usb, 0, 48);
        Assert.Equal(48, report.Length);
        Assert.Equal(0x02, report[0]);
        Assert.Equal(9, report[1 + 46]);
    }
}

public class MappingTests
{
    private static DualSenseState Neutral() => new() { LeftX = 128, LeftY = 128, RightX = 128, RightY = 128 };

    [Theory]
    [InlineData(DsButtons.Cross, XboxControl.A)]
    [InlineData(DsButtons.Circle, XboxControl.B)]
    [InlineData(DsButtons.Square, XboxControl.X)]
    [InlineData(DsButtons.Triangle, XboxControl.Y)]
    [InlineData(DsButtons.L1, XboxControl.LeftBumper)]
    [InlineData(DsButtons.R1, XboxControl.RightBumper)]
    [InlineData(DsButtons.Create, XboxControl.Back)]
    [InlineData(DsButtons.Options, XboxControl.Start)]
    [InlineData(DsButtons.PS, XboxControl.Guide)]
    [InlineData(DsButtons.L3, XboxControl.LeftThumb)]
    [InlineData(DsButtons.R3, XboxControl.RightThumb)]
    [InlineData(DsButtons.DpadUp, XboxControl.DpadUp)]
    [InlineData(DsButtons.DpadLeft, XboxControl.DpadLeft)]
    public void DefaultProfileUsesXboxPositions(DsButtons button, XboxControl expected)
    {
        var state = Neutral();
        state.Buttons = button;
        var report = new MappingEngine().Process(state, Profile.CreateDefault(), new RecordingSink());
        Assert.Equal(XboxReport.Flag(expected), report.Buttons);
    }

    [Fact]
    public void AnalogTriggersAndSticksPassThrough()
    {
        var state = Neutral();
        state.L2 = 255;
        state.R2 = 3; // inside dead zone
        state.LeftX = 255;
        state.LeftY = 0; // pushed up
        var report = new MappingEngine().Process(state, Profile.CreateDefault(), new RecordingSink());

        Assert.Equal(255, report.LeftTrigger);
        Assert.Equal(0, report.RightTrigger);
        Assert.True(report.LeftX > 20000);
        Assert.True(report.LeftY > 20000);
        Assert.Equal(0, report.RightX);
    }

    [Fact]
    public void StickDeadZoneSuppressesDrift()
    {
        var (x, y) = MappingEngine.NormalizeStick(134, 122, new StickSettings { DeadZone = 0.08 });
        Assert.Equal((0.0, 0.0), (x, y));
    }

    [Fact]
    public void RemappedButtonSendsKeyOnPressAndRelease()
    {
        var profile = Profile.CreateDefault();
        profile.Buttons[DsButtons.Cross] = ButtonAction.ToKey(0x20);
        var engine = new MappingEngine();
        var sink = new RecordingSink();

        var state = Neutral();
        state.Buttons = DsButtons.Cross;
        var report = engine.Process(state, profile, sink);
        engine.Process(state, profile, sink);
        state.Buttons = DsButtons.None;
        engine.Process(state, profile, sink);

        Assert.Equal(0, report.Buttons);
        Assert.Equal(["key32:down", "key32:up"], sink.Events);
    }

    [Fact]
    public void TouchpadClickOnRightHalfIsRightClick()
    {
        var engine = new MappingEngine();
        var sink = new RecordingSink();
        var state = Neutral();
        state.Touch0 = new TouchPoint(true, 1, 1700, 500);
        engine.Process(state, Profile.CreateDefault(), sink);
        state.Buttons = DsButtons.TouchpadClick;
        engine.Process(state, Profile.CreateDefault(), sink);
        state.Buttons = DsButtons.None;
        engine.Process(state, Profile.CreateDefault(), sink);

        Assert.Equal(["RightClick:down", "RightClick:up"], sink.Events);
    }

    [Fact]
    public void TouchpadMovesCursor()
    {
        var engine = new MappingEngine();
        var sink = new RecordingSink();
        var profile = Profile.CreateDefault();
        var state = Neutral();
        for (var i = 0; i < 20; i++)
        {
            state.Touch0 = new TouchPoint(true, 1, 400 + i * 20, 500);
            engine.Process(state, profile, sink);
        }

        Assert.True(sink.MovedX > 100, $"moved {sink.MovedX}");
        Assert.Equal(0, sink.MovedY);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void ProfileRoundTripsThroughJson()
    {
        var profile = Profile.CreateDefault("Test") with { Touchpad = new TouchpadSettings { Sensitivity = 1.7 } };
        profile.Buttons[DsButtons.Mute] = ButtonAction.ToMouse(MouseAction.MiddleClick);
        var copy = ProfileStore.Deserialize(ProfileStore.Serialize(profile))!;

        Assert.Equal("Test", copy.Name);
        Assert.Equal(1.7, copy.Touchpad.Sensitivity);
        Assert.Equal(ButtonAction.ToMouse(MouseAction.MiddleClick), copy.Buttons[DsButtons.Mute]);
        Assert.Equal(ButtonAction.ToXbox(XboxControl.A), copy.Buttons[DsButtons.Cross]);
    }
}
