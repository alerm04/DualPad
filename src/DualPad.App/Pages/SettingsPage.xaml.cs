using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DualPad.App.Services;
using DualPad.Core.Output;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace DualPad.App.Pages;

public partial class SettingsPage : UserControl
{
    private bool _loading;
    private bool _installing;
    private readonly List<Button> _installButtons = [];

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
        if (LanguageBox.Items.Count == 0)
        {
            LanguageBox.Items.Add(new LanguageInfo("", Loc.T("settings.language.system")));
            foreach (var language in Loc.Languages) LanguageBox.Items.Add(language);
        }
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<LanguageInfo>().FirstOrDefault(l => l.Code == s.Language) ?? LanguageBox.Items[0];

        var installed = HidHideService.IsInstalled;
        HideController.IsChecked = s.HideControllerWithHidHide && installed;
        HideController.IsEnabled = installed;
        UnhideButton.Visibility = s.HiddenDevices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        HidHideStatus.Text = Loc.T(installed ? "settings.hide.captionAdmin" : "settings.hide.notInstalled");

        BuildDriverList();
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

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not LanguageInfo language) return;
        AppState.Instance.UpdateSettings(s => s with { Language = language.Code });
        // Text is loaded when pages are built, so the new language shows after a restart.
        LanguageRestart.Visibility = Visibility.Visible;
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
    private void BuildDriverList()
    {
        DriverList.Children.Clear();
        _installButtons.Clear();
        var missing = 0;
        foreach (var dependency in Dependencies.All)
        {
            var status = Dependencies.GetStatus(dependency);
            if (!status.Installed) missing++;
            DriverList.Children.Add(MakeDriverRow(dependency, status));
        }

        var missingRequired = Dependencies.All.Count(d => d.Required && !Dependencies.GetStatus(d).Installed);
        DriversSummary.Text = missing == 0
            ? Loc.T("drivers.allInstalled")
            : missingRequired > 0
                ? Loc.T("drivers.missingRequired", missingRequired)
                : Loc.T("drivers.optionalOnly");
        InstallMissing.Visibility = missing > 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallMissing.IsEnabled = !_installing;
    }

    private UIElement MakeDriverRow(Dependency dependency, DependencyStatus status)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new SymbolIcon(dependency.Kind switch
        {
            DependencyKind.ViGEmBus => SymbolRegular.XboxController24,
            DependencyKind.HidHide => SymbolRegular.EyeOff24,
            _ => SymbolRegular.Code24,
        })
        { FontSize = 26, Foreground = (Brush)FindResource("BrandBrush"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
        grid.Children.Add(icon);

        var info = new StackPanel { Margin = new Thickness(14, 0, 16, 0) };
        Grid.SetColumn(info, 1);
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(new TextBlock { Text = dependency.Name, FontSize = 15, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new Border
        {
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(8, 1, 8, 2),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
            Background = dependency.Required ? (Brush)FindResource("BrandBrush") : new SolidColorBrush(Color.FromRgb(0x5A, 0x5F, 0x6A)),
            Child = new TextBlock
            {
                Text = Loc.T(dependency.Required ? "drivers.required" : "drivers.optional"),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
            },
        });
        info.Children.Add(title);
        info.Children.Add(new TextBlock { Text = Loc.T(dependency.DescriptionKey), Style = (Style)FindResource("Caption"), Margin = new Thickness(0, 4, 0, 0) });

        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var statusIcon = new SymbolIcon(status.Installed ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.ErrorCircle24)
        {
            FontSize = 16,
            Filled = true,
            Foreground = new SolidColorBrush(status.Installed ? Color.FromRgb(0x4A, 0xDE, 0x80)
                : dependency.Required ? Color.FromRgb(0xF8, 0x71, 0x71) : Color.FromRgb(0xF5, 0x9E, 0x0B)),
        };
        var ring = new ProgressRing { IsIndeterminate = true, Width = 16, Height = 16, Visibility = Visibility.Collapsed };
        var statusText = new TextBlock
        {
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = status.Installed
                ? (status.Version is null ? Loc.T("drivers.installed") : Loc.T("drivers.installedVersion", status.Version))
                    + (dependency.Kind == DependencyKind.DotNetRuntime ? $" ({Dependencies.Architecture})" : "")
                : Loc.T("drivers.notInstalled"),
        };
        statusRow.Children.Add(statusIcon);
        statusRow.Children.Add(ring);
        statusRow.Children.Add(statusText);
        info.Children.Add(statusRow);
        grid.Children.Add(info);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(actions, 2);
        var install = new Button
        {
            Content = Loc.T(status.Installed ? "drivers.update" : "drivers.install"),
            Icon = new SymbolIcon(status.Installed ? SymbolRegular.ArrowSync24 : SymbolRegular.ArrowDownload24),
            Appearance = status.Installed ? ControlAppearance.Secondary : ControlAppearance.Primary,
            IsEnabled = !_installing,
            MinWidth = 120,
        };
        install.Click += async (_, _) =>
        {
            statusIcon.Visibility = Visibility.Collapsed;
            ring.Visibility = Visibility.Visible;
            statusText.Text = Loc.T("drivers.installing");
            await InstallAsync([dependency], line => statusText.Text = line);
        };
        _installButtons.Add(install);
        actions.Children.Add(install);
        var more = new Button { Content = Loc.T("drivers.info"), Icon = new SymbolIcon(SymbolRegular.Open24), Margin = new Thickness(8, 0, 0, 0), Appearance = ControlAppearance.Transparent };
        more.Click += (_, _) => Dependencies.OpenUrl(dependency.InfoUrl);
        actions.Children.Add(more);
        grid.Children.Add(actions);

        return new Border { Style = (Style)FindResource("Panel"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(16, 14, 16, 14), Child = grid };
    }

    private async Task InstallAsync(IReadOnlyList<Dependency> dependencies, Action<string>? onProgress = null)
    {
        if (_installing) return;
        _installing = true;
        foreach (var button in _installButtons) button.IsEnabled = false;
        InstallMissing.IsEnabled = false;
        DriverResult.IsOpen = false;

        var messages = new List<string>();
        var allOk = true;
        var needsAppRestart = false;
        var progress = new Progress<string>(line => onProgress?.Invoke(line));
        foreach (var dependency in dependencies)
        {
            var result = await Dependencies.InstallAsync(dependency, progress);
            allOk &= result.Success;
            messages.Add($"{dependency.Name}: {result.Message}");
            if (result.Success && dependency.Kind is DependencyKind.ViGEmBus or DependencyKind.DotNetRuntime) needsAppRestart = true;
        }

        _installing = false;
        DriverResult.Severity = allOk ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        DriverResult.Title = Loc.T(allOk ? "drivers.done" : "drivers.failed");
        DriverResult.Message = string.Join("\n", messages);
        DriverResult.IsOpen = true;
        RestartRow.Visibility = needsAppRestart ? Visibility.Visible : Visibility.Collapsed;
        Load();
    }

    private async void OnInstallMissing(object sender, RoutedEventArgs e)
    {
        var missing = Dependencies.All.Where(d => !Dependencies.GetStatus(d).Installed).ToList();
        if (missing.Count == 0) return;
        DriversSummary.Text = Loc.T("drivers.installingList", string.Join(", ", missing.Select(d => d.Name)));
        await InstallAsync(missing, line => DriversSummary.Text = line);
    }

    private void OnRestartApp(object sender, RoutedEventArgs e) => App.Restart();
}
