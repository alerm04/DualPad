using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DualPad.App.Services;
using DualPad.Core.DualSense;
using Wpf.Ui.Controls;

namespace DualPad.App.Pages;

public partial class HomePage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public HomePage()
    {
        InitializeComponent();
        DataContext = AppState.Instance;
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var state = AppState.Instance;
        var session = state.PrimarySession;

        ViGEmBar.IsOpen = !state.IsViGEmInstalled;
        ViGEmActions.Visibility = state.IsViGEmInstalled ? Visibility.Collapsed : Visibility.Visible;
        BlockedBar.IsOpen = state.IsControllerBlocked;
        BlockedActions.Visibility = state.IsControllerBlocked && Process.GetProcessesByName("DS4Windows").Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = session is null ? Visibility.Visible : Visibility.Collapsed;
        Controller.Opacity = session is null ? 0.12 : 1;

        if (session is null)
        {
            ConnectionIcon.Symbol = SymbolRegular.PlugDisconnected24;
            ConnectionText.Text = Loc.T(state.IsControllerBlocked ? "home.busy" : "home.notConnected");
            BatteryText.Text = "—";
            BatteryBar.Value = 0;
            XboxText.Text = Loc.T(state.IsViGEmInstalled ? "home.xbox.waiting" : "home.xbox.noDriver");
            return;
        }

        var s = session.State;
        var bt = session.Device.Connection == ConnectionType.Bluetooth;
        ConnectionIcon.Symbol = bt ? SymbolRegular.BluetoothConnected24 : SymbolRegular.UsbPlug24;
        ConnectionText.Text = (session.Device.IsEdge ? "DualSense Edge · " : "DualSense · ") + (bt ? "Bluetooth" : "USB");

        BatteryText.Text = s.Charge switch
        {
            ChargeState.Charging => Loc.T("home.charging", s.Battery),
            ChargeState.Full => Loc.T("home.full"),
            _ => $"{s.Battery}%",
        };
        BatteryBar.Value = s.Battery;
        BatteryIcon.Symbol = s.Charge == ChargeState.Charging ? SymbolRegular.BatteryCharge24 : s.Battery switch
        {
            <= 10 => SymbolRegular.BatteryWarning24,
            <= 30 => SymbolRegular.Battery224,
            <= 60 => SymbolRegular.Battery524,
            <= 90 => SymbolRegular.Battery824,
            _ => SymbolRegular.Battery924,
        };

        XboxText.ToolTip = session.XboxError;
        if (session.Xbox is null) XboxText.Text = Loc.T(state.IsViGEmInstalled ? "home.xbox.failed" : "home.xbox.noDriver");
        else if (!state.MappingEnabled) XboxText.Text = Loc.T("home.xbox.paused");
        else XboxText.Text = session.Xbox.PlayerIndex >= 0 ? Loc.T("home.xbox.activePlayer", session.Xbox.PlayerIndex + 1) : Loc.T("home.xbox.active");
    }

    private void OnEditMapping(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.GoTo("mapping");

    private void OnOpenBluetooth(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });

    private void OnCloseDs4Windows(object sender, RoutedEventArgs e)
    {
        foreach (var process in Process.GetProcessesByName("DS4Windows"))
        {
            try
            {
                if (!process.CloseMainWindow() || !process.WaitForExit(3000)) process.Kill();
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        AppState.Instance.Service.ScanNow();
    }
}
