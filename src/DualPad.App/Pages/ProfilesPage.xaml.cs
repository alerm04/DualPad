using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DualPad.App.Services;
using DualPad.Core.Mapping;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace DualPad.App.Pages;

public partial class ProfilesPage : UserControl
{
    public ProfilesPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppState.Instance.Profiles.CollectionChanged += OnProfilesChanged;
            AppState.Instance.PropertyChanged += OnAppStateChanged;
            Rebuild();
        };
        Unloaded += (_, _) =>
        {
            AppState.Instance.Profiles.CollectionChanged -= OnProfilesChanged;
            AppState.Instance.PropertyChanged -= OnAppStateChanged;
        };
    }

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.ActiveProfile)) Rebuild();
    }

    private void Rebuild()
    {
        List.Children.Clear();
        foreach (var profile in AppState.Instance.Profiles) List.Children.Add(MakeCard(profile));
    }

    private UIElement MakeCard(Profile profile)
    {
        var state = AppState.Instance;
        var isActive = ReferenceEquals(profile, state.ActiveProfile);
        var standard = Profile.XboxLayout();
        var changed = profile.Buttons.Count(kv => standard.GetValueOrDefault(kv.Key) != kv.Value);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromRgb(profile.Lightbar.R, profile.Lightbar.G, profile.Lightbar.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(2),
            Child = new SymbolIcon(SymbolRegular.XboxController24) { FontSize = 20, Foreground = Brushes.White },
        };
        grid.Children.Add(dot);

        var info = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(info, 1);
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        var name = new TextBlock { Text = profile.Name, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var editor = new TextBox { Text = profile.Name, MinWidth = 220, Visibility = Visibility.Collapsed };
        nameRow.Children.Add(name);
        nameRow.Children.Add(editor);
        if (isActive)
        {
            nameRow.Children.Add(new Border
            {
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 1, 8, 2),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)FindResource("BrandBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "Attivo", Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold },
            });
        }
        info.Children.Add(nameRow);
        var touch = profile.Touchpad.Mode == TouchpadMode.Mouse ? "touchpad come mouse" : "touchpad disattivato";
        info.Children.Add(new TextBlock
        {
            Text = (changed == 0 ? "Layout Xbox standard" : $"{changed} tasti personalizzati") + " · " + touch,
            Style = (Style)FindResource("Caption"),
            Margin = new Thickness(0, 2, 0, 0),
        });
        grid.Children.Add(info);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(actions, 2);
        if (!isActive) actions.Children.Add(MakeButton("Usa", SymbolRegular.Checkmark24, () => state.ActiveProfile = profile, primary: true));
        actions.Children.Add(MakeButton("Rinomina", SymbolRegular.Edit24, () => BeginRename(name, editor, profile)));
        actions.Children.Add(MakeButton("Duplica", SymbolRegular.Copy24, () => state.CreateProfile(profile.Name + " copia", profile)));
        actions.Children.Add(MakeButton("Esporta", SymbolRegular.ArrowExport24, () => Export(profile)));
        if (state.Profiles.Count > 1)
            actions.Children.Add(MakeButton("Elimina", SymbolRegular.Delete24, () => Delete(profile)));
        grid.Children.Add(actions);

        return new Border
        {
            Style = (Style)FindResource("Panel"),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(16, 14, 16, 14),
            BorderBrush = isActive ? (Brush)FindResource("BrandBrush") : (Brush)FindResource("CardStrokeColorDefaultBrush"),
            Child = grid,
        };
    }

    private static Button MakeButton(string? text, SymbolRegular icon, Action onClick, bool primary = false, string? tooltip = null)
    {
        var button = new Button
        {
            Content = text,
            Icon = new SymbolIcon(icon),
            Margin = new Thickness(6, 0, 0, 0),
            Appearance = primary ? ControlAppearance.Primary : ControlAppearance.Secondary,
            ToolTip = tooltip,
            MinWidth = text is null ? 36 : 0,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static void BeginRename(TextBlock name, TextBox editor, Profile profile)
    {
        name.Visibility = Visibility.Collapsed;
        editor.Visibility = Visibility.Visible;
        editor.Focus();
        editor.SelectAll();

        void Commit()
        {
            editor.KeyDown -= OnKey;
            editor.LostFocus -= OnLost;
            AppState.Instance.RenameProfile(profile, editor.Text);
            name.Visibility = Visibility.Visible;
            editor.Visibility = Visibility.Collapsed;
        }

        void OnKey(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Commit();
            else if (e.Key == Key.Escape) { editor.Text = profile.Name; Commit(); }
        }

        void OnLost(object s, RoutedEventArgs e) => Commit();

        editor.KeyDown += OnKey;
        editor.LostFocus += OnLost;
    }

    private static void Delete(Profile profile)
    {
        var result = System.Windows.MessageBox.Show($"Eliminare il profilo \"{profile.Name}\"?", "Elimina profilo",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes) AppState.Instance.DeleteProfile(profile);
    }

    private static void Export(Profile profile)
    {
        var dialog = new SaveFileDialog { FileName = profile.Name + ".json", Filter = "Profilo DualPad (*.json)|*.json" };
        if (dialog.ShowDialog() == true) AppState.Instance.Store.Export(profile, dialog.FileName);
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        var profile = AppState.Instance.CreateProfile("Nuovo profilo");
        AppState.Instance.ActiveProfile = profile;
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Profilo DualPad (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        if (AppState.Instance.ImportProfile(dialog.FileName) is null)
            System.Windows.MessageBox.Show("Il file non è un profilo DualPad valido.", "Importa profilo", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppState.Instance.Store.ProfilesDirectory) { UseShellExecute = true });
}
