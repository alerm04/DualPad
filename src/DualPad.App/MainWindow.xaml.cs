using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DualPad.App.Pages;
using DualPad.App.Services;
using DualPad.Core.DualSense;
using Wpf.Ui.Controls;

namespace DualPad.App;

public partial class MainWindow : FluentWindow
{
    private readonly Dictionary<string, UserControl> _pages = [];
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = AppState.Instance;
        Navigate("home");

        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
        AppState.Instance.Notify += ShowNotification;
        AppState.Instance.Service.SessionsChanged += () => Dispatcher.BeginInvoke(UpdateStatus);
    }

    public void Navigate(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "mapping" => new MappingPage(),
                "sticks" => new SticksPage(),
                "lights" => new LightsPage(),
                "profiles" => new ProfilesPage(),
                "settings" => new SettingsPage(),
                "advanced" => new AdvancedPage(),
                _ => new HomePage(),
            };
            _pages[key] = page;
        }
        PageHost.Content = page;
    }

    /// <summary>Selects a sidebar entry from code (e.g. a button on the home page).</summary>
    public void GoTo(string key)
    {
        if (key == "mapping") NavMapping.IsChecked = true;
        else Navigate(key);
    }

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string key } && IsInitialized) Navigate(key);
    }

    private void UpdateStatus()
    {
        var state = AppState.Instance;
        var session = state.PrimarySession;
        if (session is not null)
        {
            var s = session.State;
            var connection = session.Device.Connection == ConnectionType.Bluetooth ? "Bluetooth" : "USB";
            var charge = s.Charge == ChargeState.Charging ? " · in carica" : "";
            StatusTitle.Text = session.Device.IsEdge ? "DualSense Edge" : "DualSense";
            StatusDetail.Text = $"{connection} · batteria {s.Battery}%{charge}";
            StatusDot.Fill = state.MappingEnabled ? Brushes.LimeGreen : Brushes.Orange;
            Tray.TooltipText = $"DualPad · {StatusTitle.Text} {connection} · {s.Battery}%";
        }
        else if (state.IsControllerBlocked)
        {
            StatusTitle.Text = "Controller occupato";
            StatusDetail.Text = "Chiudi DS4Windows o altre app";
            StatusDot.Fill = Brushes.OrangeRed;
            Tray.TooltipText = "DualPad · controller usato da un'altra app";
        }
        else
        {
            StatusTitle.Text = "Nessun controller";
            StatusDetail.Text = "In attesa del DualSense…";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            Tray.TooltipText = "DualPad · nessun controller";
        }
    }

    private void ShowNotification(string title, string message)
    {
        if (!AppState.Instance.Settings.Notifications || !IsVisible) return;
        var snackbar = new Snackbar(Snackbars)
        {
            Title = title,
            Content = message,
            Appearance = ControlAppearance.Secondary,
            Icon = new SymbolIcon(SymbolRegular.XboxController24),
            Timeout = TimeSpan.FromSeconds(3),
        };
        snackbar.Show();
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void OnTrayClick(object sender, RoutedEventArgs e) => ShowFromTray();

    private void OnTrayOpen(object sender, RoutedEventArgs e) => ShowFromTray();

    private void OnTrayExit(object sender, RoutedEventArgs e) => ExitApp();

    private void OnTrayPowerOff(object sender, RoutedEventArgs e)
    {
        foreach (var session in AppState.Instance.Service.Sessions) Core.ControllerService.PowerOff(session);
    }

    public void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        Tray.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && AppState.Instance.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ExitApp();
    }
}
