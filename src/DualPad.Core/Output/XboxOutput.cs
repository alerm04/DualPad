using DualPad.Core.Mapping;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DualPad.Core.Output;

public sealed class ViGEmUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>A virtual Xbox 360 controller created through the ViGEmBus driver.</summary>
public sealed class XboxOutput : IDisposable
{
    private static readonly object ClientLock = new();
    private static ViGEmClient? _client;

    private readonly IXbox360Controller _controller;
    private XboxReport _last;
    private bool _hasLast;

    /// <summary>Raised when a game sets vibration: (large/left motor, small/right motor).</summary>
    public event Action<byte, byte>? Rumble;

    /// <summary>XInput slot 0..3 assigned by Windows, or -1 until known.</summary>
    public int PlayerIndex { get; private set; } = -1;
    public event Action<int>? PlayerIndexChanged;

    private XboxOutput(IXbox360Controller controller)
    {
        _controller = controller;
        _controller.AutoSubmitReport = false;
        _controller.FeedbackReceived += OnFeedback;
    }

    public static bool IsDriverInstalled()
    {
        try
        {
            GetClient();
            return true;
        }
        catch (ViGEmUnavailableException)
        {
            return false;
        }
    }

    public static XboxOutput Create()
    {
        var controller = GetClient().CreateXbox360Controller();
        var output = new XboxOutput(controller);
        controller.Connect();
        return output;
    }

    /// <summary>Sends the report to the driver. Returns false when nothing changed and no call was needed.</summary>
    public bool Submit(in XboxReport report)
    {
        if (_hasLast && report.Equals(_last)) return false;
        _last = report;
        _hasLast = true;

        _controller.SetButtonsFull(report.Buttons);
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, report.LeftTrigger);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, report.RightTrigger);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, report.LeftX);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, report.LeftY);
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, report.RightX);
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, report.RightY);
        _controller.SubmitReport();
        return true;
    }

    private void OnFeedback(object? sender, Xbox360FeedbackReceivedEventArgs e)
    {
        if (e.LedNumber != PlayerIndex && e.LedNumber is >= 0 and <= 3)
        {
            PlayerIndex = e.LedNumber;
            PlayerIndexChanged?.Invoke(PlayerIndex);
        }
        Rumble?.Invoke(e.LargeMotor, e.SmallMotor);
    }

    private static ViGEmClient GetClient()
    {
        lock (ClientLock)
        {
            if (_client is not null) return _client;
            try
            {
                _client = new ViGEmClient();
                return _client;
            }
            catch (VigemBusNotFoundException ex)
            {
                throw new ViGEmUnavailableException("Il driver ViGEmBus non è installato.", ex);
            }
            catch (Exception ex) when (ex is VigemBusAccessFailedException or VigemBusVersionMismatchException)
            {
                throw new ViGEmUnavailableException("Impossibile accedere al driver ViGEmBus.", ex);
            }
        }
    }

    public void Dispose()
    {
        _controller.FeedbackReceived -= OnFeedback;
        try { _controller.Disconnect(); } catch (VigemTargetNotPluggedInException) { }
    }
}
