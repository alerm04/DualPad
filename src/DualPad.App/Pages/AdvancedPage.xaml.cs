using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DualPad.App.Services;
using DualPad.Core;
using DualPad.Core.Diagnostics;
using DualPad.Core.DualSense;
using Wpf.Ui.Controls;

namespace DualPad.App.Pages;

public partial class AdvancedPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _running;

    public AdvancedPage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => RefreshLive();
        Loaded += (_, _) =>
        {
            var s = AppState.Instance.Settings;
            HighPriority.IsChecked = s.HighPriority;
            PowerOffOnExit.IsChecked = s.PowerOffOnExit;
            RefreshLive();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private static string Ms(double value) => value < 1
        ? value.ToString("0.00", CultureInfo.CurrentCulture) + " ms"
        : value.ToString("0.0", CultureInfo.CurrentCulture) + " ms";

    private void RefreshLive()
    {
        var session = AppState.Instance.PrimarySession;
        StartTest.IsEnabled = session is not null && !_running;
        PowerOffNow.IsEnabled = session?.Device.Connection == ConnectionType.Bluetooth;

        if (session is null)
        {
            LiveRate.Text = Loc.T("advanced.connect");
            ConnectionTip.Text = "";
            return;
        }

        if (!_running)
        {
            var (intervals, _) = session.Stats.Snapshot();
            LiveRate.Text = intervals.Average > 0 ? Loc.T("advanced.liveRate", Math.Round(1000 / intervals.Average)) : "";
        }

        ConnectionTip.Text = Loc.T(session.Device.Connection == ConnectionType.Bluetooth ? "advanced.tip.bt" : "advanced.tip.usb");
    }

    private async void OnStartTest(object sender, RoutedEventArgs e)
    {
        var session = AppState.Instance.PrimarySession;
        if (session is null || _running) return;

        _running = true;
        StartTest.IsEnabled = false;
        TestProgress.Value = 0;
        TestProgress.Visibility = Visibility.Visible;
        TestHint.Visibility = Visibility.Visible;
        Verdict.IsOpen = false;

        var mappingWasOff = !AppState.Instance.MappingEnabled;
        if (mappingWasOff) AppState.Instance.MappingEnabled = true;

        var result = await LatencyProbe.RunAsync(session, TimeSpan.FromSeconds(10), new Progress<double>(p => TestProgress.Value = p));

        if (mappingWasOff) AppState.Instance.MappingEnabled = false;
        _running = false;
        TestProgress.Visibility = Visibility.Collapsed;
        TestHint.Visibility = Visibility.Collapsed;
        ShowResult(result, session);
        RefreshLive();
    }

    private void ShowResult(LatencyResult r, ControllerSession session)
    {
        Tiles.Visibility = Visibility.Visible;

        RateValue.Text = $"{r.ReportRateHz:0} Hz";
        RateDetail.Text = Loc.T("advanced.rateDetail", Ms(r.ReportIntervals.Average));
        JitterValue.Text = Ms(r.ReportIntervals.P99);
        JitterDetail.Text = Loc.T("advanced.max", Ms(r.ReportIntervals.Max));
        ProcessingValue.Text = Ms(r.Processing.Median);
        ProcessingDetail.Text = Loc.T("advanced.within", Ms(r.Processing.P99));
        if (r.DeliveryMeasured)
        {
            DeliveryValue.Text = Ms(r.Delivery.Median);
            DeliveryDetail.Text = Loc.T("advanced.withinSamples", Ms(r.Delivery.P99), r.Delivery.Count);
        }
        else
        {
            DeliveryValue.Text = "—";
            DeliveryDetail.Text = Loc.T("advanced.moveSticks");
        }

        var bluetooth = session.Device.Connection == ConnectionType.Bluetooth;
        var added = r.AddedMedianMs;
        Verdict.Severity = added < 1 ? InfoBarSeverity.Success : added < 3 ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        Verdict.Title = Loc.T(added < 1 ? "advanced.verdict.great" : added < 3 ? "advanced.verdict.good" : "advanced.verdict.high");
        Verdict.Message =
            Loc.T("advanced.verdict.added", Ms(added)) +
            Loc.T("advanced.verdict.polling", Ms(r.PollingDelayMs), bluetooth ? "Bluetooth" : "USB") +
            (added >= 3 ? Loc.T("advanced.verdict.tryPriority") : bluetooth ? Loc.T("advanced.verdict.radio") : "");
        Verdict.IsOpen = true;
    }

    private void OnHighPriority(object sender, RoutedEventArgs e)
    {
        AppState.Instance.UpdateSettings(s => s with { HighPriority = HighPriority.IsChecked == true });
        AppState.Instance.ApplyProcessPriority();
    }

    private void OnPowerOffOnExit(object sender, RoutedEventArgs e) =>
        AppState.Instance.UpdateSettings(s => s with { PowerOffOnExit = PowerOffOnExit.IsChecked == true });

    private void OnPowerOffNow(object sender, RoutedEventArgs e)
    {
        foreach (var session in AppState.Instance.Service.Sessions) ControllerService.PowerOff(session);
    }
}
