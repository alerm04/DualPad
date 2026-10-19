using System.Windows;
using System.Windows.Threading;
using DualPad.App.Services;
using Wpf.Ui.Appearance;

namespace DualPad.App;

public partial class App : Application
{
    private const string MutexName = "DualPad.SingleInstance";
    private const string ShowEventName = "DualPad.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private bool _started;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length >= 2 && e.Args[0] == "--snapshot")
        {
            base.OnStartup(e);
            _started = true;
            ApplyTheme(e.Args.Contains("--light") ? "Light" : "Dark");
            Snapshot.Run(e.Args[1], live: e.Args.Contains("--live"));
            return;
        }

        _mutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            // Another DualPad is running: bring it to the front instead of starting twice.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        _started = true;
        var state = AppState.Instance;
        ApplyTheme(state.Settings.Theme);

        var window = new MainWindow();
        MainWindow = window;
        var minimized = e.Args.Contains("--minimized") || state.Settings.StartMinimized;
        if (!minimized) window.Show();

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(window.ShowFromTray);
        }) { IsBackground = true };
        listener.Start();

        state.Start();
    }

    /// <summary>Starts a fresh DualPad and closes this one (e.g. after installing a driver).</summary>
    public static void Restart()
    {
        var app = (App)Current;
        app._mutex?.ReleaseMutex();
        app._mutex?.Dispose();
        app._mutex = null;
        if (Environment.ProcessPath is { } exe) System.Diagnostics.Process.Start(exe);
        (app.MainWindow as MainWindow)?.ExitApp();
    }

    public static void ApplyTheme(string theme)
    {
        var applied = theme switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };
        ApplicationThemeManager.Apply(applied, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: false);
        // Brand accent instead of the Windows accent, so toggles and primary buttons match the app.
        ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromRgb(0x3B, 0x8E, 0xFF), applied);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "DualPad - errore", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_started) AppState.Instance.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
