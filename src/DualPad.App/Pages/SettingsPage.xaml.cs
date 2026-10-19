using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DualPad.App.Services;
using DualPad.Core.Output;

namespace DualPad.App.Pages;

public partial class SettingsPage : UserControl
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        _loading = true;
        var s = AppState.Instance.Settings;
        StartWithWindows.IsChecked = Autostart.IsEnabled;
        StartMinimized.IsChecked = s.StartMinimized;
        MinimizeToTray.IsChecked = s.MinimizeToTray;
        Notifications.IsChecked = s.Notifications;
        foreach (ComboBoxItem item in Theme.Items)
            if ((string)item.Tag == s.Theme) Theme.SelectedItem = item;

        var installed = HidHideService.IsInstalled;
        HideController.IsChecked = s.HideControllerWithHidHide && installed;
        HideController.IsEnabled = installed;
        HidHideDownload.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        UnhideButton.Visibility = s.HiddenDevices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        HidHideStatus.Text = installed
            ? "Evita il doppio input nei giochi che leggono anche il controller PlayStation. Windows chiederà i permessi di amministratore."
            : "HidHide non è installato. Installalo per evitare il doppio input nei giochi.";

        ViGEmStatus.Text = AppState.Instance.IsViGEmInstalled ? "Installato e funzionante" : "Non installato: serve per emulare il controller Xbox";
        VersionText.Text = $"DualPad {typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)}";
        _loading = false;
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Autostart.Set(StartWithWindows.IsChecked == true);
        AppState.Instance.UpdateSettings(s => s with
        {
            StartWithWindows = StartWithWindows.IsChecked == true,
            StartMinimized = StartMinimized.IsChecked == true,
            MinimizeToTray = MinimizeToTray.IsChecked == true,
            Notifications = Notifications.IsChecked == true,
        });
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Theme.SelectedItem is not ComboBoxItem { Tag: string theme }) return;
        AppState.Instance.UpdateSettings(s => s with { Theme = theme });
        App.ApplyTheme(theme);
    }

    private void OnHideChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enabled = HideController.IsChecked == true;
        AppState.Instance.UpdateSettings(s => s with { HideControllerWithHidHide = enabled });
        if (enabled) AppState.Instance.HideControllers();
        Load();
    }

    private async void OnUnhide(object sender, RoutedEventArgs e)
    {
        UnhideButton.IsEnabled = false;
        var ok = await AppState.Instance.UnhideControllersAsync();
        if (ok) AppState.Instance.UpdateSettings(s => s with { HideControllerWithHidHide = false });
        UnhideButton.IsEnabled = true;
        Load();
    }

    private void OnOpenData(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Path.GetDirectoryName(AppState.Instance.Store.ProfilesDirectory)!) { UseShellExecute = true });
}
