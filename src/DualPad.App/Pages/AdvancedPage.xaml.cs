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
            LiveRate.Text = "Collega il controller";
            ConnectionTip.Text = "";
            return;
        }

        if (!_running)
        {
            var (intervals, _) = session.Stats.Snapshot();
            LiveRate.Text = intervals.Average > 0 ? $"Ora: {1000 / intervals.Average:0} report/s" : "";
        }

        ConnectionTip.Text = session.Device.Connection == ConnectionType.Bluetooth
            ? "Sei collegato via Bluetooth: per scendere ancora di latenza usa il cavo USB-C, che elimina il tratto radio."
            : "Sei collegato via USB: è già la connessione con la latenza più bassa.";
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
        RateDetail.Text = $"un report ogni {Ms(r.ReportIntervals.Average)}";
        JitterValue.Text = Ms(r.ReportIntervals.P99);
        JitterDetail.Text = $"massimo {Ms(r.ReportIntervals.Max)}";
        ProcessingValue.Text = Ms(r.Processing.Median);
        ProcessingDetail.Text = $"99% entro {Ms(r.Processing.P99)}";
        if (r.DeliveryMeasured)
        {
            DeliveryValue.Text = Ms(r.Delivery.Median);
            DeliveryDetail.Text = $"99% entro {Ms(r.Delivery.P99)} · {r.Delivery.Count} campioni";
        }
        else
        {
            DeliveryValue.Text = "—";
            DeliveryDetail.Text = "muovi gli stick durante il test";
        }

        var bluetooth = session.Device.Connection == ConnectionType.Bluetooth;
        var added = r.AddedMedianMs;
        Verdict.Severity = added < 1 ? InfoBarSeverity.Success : added < 3 ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        Verdict.Title = added < 1 ? "Ottimo: DualPad non rallenta il controller" : added < 3 ? "Buono" : "Ritardo più alto del previsto";
        Verdict.Message =
            $"DualPad aggiunge circa {Ms(added)} tra l'arrivo dei dati e il gioco. " +
            $"In più, in media, un input aspetta {Ms(r.PollingDelayMs)} il report successivo del controller ({(bluetooth ? "Bluetooth" : "USB")}). " +
            (added >= 3 ? "Prova ad attivare la priorità alta e a chiudere i programmi pesanti in background." :
                bluetooth ? "Il margine rimasto è nel collegamento radio: con il cavo USB è più basso." : "");
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
