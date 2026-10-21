using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DualPad.App.Services;
using DualPad.Core.Mapping;

namespace DualPad.App.Pages;

public partial class LightsPage : UserControl
{
    private static readonly (string NameKey, Color Color)[] Presets =
    [
        ("color.psBlue", Color.FromRgb(0x00, 0x78, 0xFF)),
        ("color.lightBlue", Color.FromRgb(0x00, 0xC8, 0xFF)),
        ("color.xboxGreen", Color.FromRgb(0x10, 0xD0, 0x10)),
        ("color.purple", Color.FromRgb(0x8B, 0x3C, 0xFF)),
        ("color.pink", Color.FromRgb(0xFF, 0x3C, 0xB4)),
        ("color.red", Color.FromRgb(0xFF, 0x20, 0x20)),
        ("color.orange", Color.FromRgb(0xFF, 0x80, 0x00)),
        ("color.yellow", Color.FromRgb(0xFF, 0xD0, 0x00)),
        ("color.white", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("color.off", Color.FromRgb(0x00, 0x00, 0x00)),
    ];

    private bool _loading;
    private bool _saving;

    public LightsPage()
    {
        InitializeComponent();
        foreach (var (nameKey, color) in Presets)
        {
            var swatch = new Border
            {
                Width = 34,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 8),
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)),
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                ToolTip = Loc.T(nameKey),
            };
            swatch.MouseLeftButtonUp += (_, _) => SetColor(color);
            Swatches.Children.Add(swatch);
        }

        Loaded += (_, _) =>
        {
            Load();
            AppState.Instance.PropertyChanged += OnAppStateChanged;
        };
        Unloaded += (_, _) => AppState.Instance.PropertyChanged -= OnAppStateChanged;
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.ActiveProfile) && !_saving) Load();
    }

    private void Load()
    {
        _loading = true;
        var p = AppState.Instance.ActiveProfile;
        SliderR.Value = p.Lightbar.R;
        SliderG.Value = p.Lightbar.G;
        SliderB.Value = p.Lightbar.B;
        BrightHigh.IsChecked = p.Lightbar.Brightness == 0;
        BrightMid.IsChecked = p.Lightbar.Brightness == 1;
        BrightLow.IsChecked = p.Lightbar.Brightness == 2;
        ShowBattery.IsChecked = p.Lightbar.ShowBattery;
        PlayerLeds.IsChecked = p.Lightbar.PlayerLeds;
        RumbleOn.IsChecked = p.Rumble.Enabled;
        Strength.Value = p.Rumble.Strength;
        _loading = false;
        UpdatePreview();
    }

    private void SetColor(Color color)
    {
        _loading = true;
        SliderR.Value = color.R;
        SliderG.Value = color.G;
        SliderB.Value = color.B;
        _loading = false;
        Save();
    }

    private void UpdatePreview()
    {
        var color = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
        ValueR.Text = $"{(int)SliderR.Value}";
        ValueG.Text = $"{(int)SliderG.Value}";
        ValueB.Text = $"{(int)SliderB.Value}";
        HexText.Text = ShowBattery.IsChecked == true ? Loc.T("lights.batteryPreview") :$"#{color.R:X2}{color.G:X2}{color.B:X2}";
        var shown = ShowBattery.IsChecked == true ? Color.FromRgb(40, 220, 90) : color;
        PreviewGlow.Background = new SolidColorBrush(shown);
        PreviewShadow.Color = shown;
        PreviewGlow.Opacity = BrightLow.IsChecked == true ? 0.45 : BrightMid.IsChecked == true ? 0.7 : 1;
        StrengthValue.Text = $"{Strength.Value:0}%";
    }

    private void Save()
    {
        if (_loading || !IsLoaded) return;
        byte brightness = BrightLow.IsChecked == true ? (byte)2 : BrightMid.IsChecked == true ? (byte)1 : (byte)0;
        _saving = true;
        try
        {
            AppState.Instance.UpdateProfile(p => p with
            {
                Lightbar = p.Lightbar with
                {
                    R = (byte)SliderR.Value,
                    G = (byte)SliderG.Value,
                    B = (byte)SliderB.Value,
                    Brightness = brightness,
                    ShowBattery = ShowBattery.IsChecked == true,
                    PlayerLeds = PlayerLeds.IsChecked == true,
                },
                Rumble = p.Rumble with { Enabled = RumbleOn.IsChecked == true, Strength = (int)Strength.Value },
            });
        }
        finally
        {
            _saving = false;
        }
        UpdatePreview();
    }

    private void OnRgbChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        UpdatePreview();
        Save();
    }

    private void OnBrightness(object sender, RoutedEventArgs e) => Save();
    private void OnToggle(object sender, RoutedEventArgs e) => Save();

    private void OnStrength(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        UpdatePreview();
        Save();
    }

    private async void OnTestRumble(object sender, RoutedEventArgs e)
    {
        var session = AppState.Instance.PrimarySession;
        if (session is not null) await session.TestRumbleAsync();
    }
}
