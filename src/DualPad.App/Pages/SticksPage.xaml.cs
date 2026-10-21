using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DualPad.App.Services;
using DualPad.Core.DualSense;
using DualPad.Core.Mapping;

namespace DualPad.App.Pages;

public partial class SticksPage : UserControl
{
    private static readonly (StickTarget Target, string LabelKey)[] Targets =
    [
        (StickTarget.LeftStick, "sticks.target.left"),
        (StickTarget.RightStick, "sticks.target.right"),
        (StickTarget.Mouse, "sticks.target.mouse"),
        (StickTarget.None, "sticks.target.off"),
    ];

    private sealed class StickPreview
    {
        public required Ellipse DeadZone, Raw, Output;
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly StickPreview _leftPreview, _rightPreview;
    private bool _loading; // controls are being filled from the profile
    private bool _saving;  // a change from this page is being applied

    public SticksPage()
    {
        InitializeComponent();
        foreach (var box in new[] { LeftTarget, RightTarget })
            foreach (var (_, labelKey) in Targets) box.Items.Add(Loc.T(labelKey));

        _leftPreview = BuildPreview(LeftPreview);
        _rightPreview = BuildPreview(RightPreview);

        _timer.Tick += (_, _) => UpdateLive();
        Loaded += (_, _) =>
        {
            LoadValues();
            AppState.Instance.PropertyChanged += OnAppStateChanged;
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            AppState.Instance.PropertyChanged -= OnAppStateChanged;
            _timer.Stop();
        };
    }

    private static Profile Profile => AppState.Instance.ActiveProfile;

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.ActiveProfile) && !_saving) LoadValues();
    }

    private void LoadValues()
    {
        _loading = true;
        var t = Profile.Touchpad;
        TouchMouse.IsChecked = t.Mode == TouchpadMode.Mouse;
        Sensitivity.Value = t.Sensitivity;
        TouchAccel.IsChecked = t.Acceleration;
        TapClick.IsChecked = t.TapToClick;
        TwoFingerScroll.IsChecked = t.TwoFingerScroll;
        RightZone.IsChecked = t.RightClickZone;
        LoadStick(Profile.LeftStick, LeftTarget, LeftDead, LeftCurve, LeftMouseSpeed, LeftInvert);
        LoadStick(Profile.RightStick, RightTarget, RightDead, RightCurve, RightMouseSpeed, RightInvert);
        TriggerDead.Value = Profile.Triggers.DeadZone;
        _loading = false;
        UpdateLabels();
    }

    private static void LoadStick(StickSettings s, ComboBox target, Slider dead, Slider curve, Slider speed, Wpf.Ui.Controls.ToggleSwitch invert)
    {
        target.SelectedIndex = Array.FindIndex(Targets, x => x.Target == s.Target);
        dead.Value = s.DeadZone;
        curve.Value = s.Curve;
        speed.Value = s.MouseSpeed;
        invert.IsChecked = s.InvertY;
    }

    private void UpdateLabels()
    {
        SensitivityValue.Text = $"{Sensitivity.Value:0.0}×";
        LeftDeadValue.Text = $"{LeftDead.Value:P0}";
        RightDeadValue.Text = $"{RightDead.Value:P0}";
        LeftCurveValue.Text = $"{LeftCurve.Value:0.0}";
        RightCurveValue.Text = $"{RightCurve.Value:0.0}";
        TriggerDeadValue.Text = $"{TriggerDead.Value:0}";
        LeftMouseRow.Visibility = LeftTarget.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        RightMouseRow.Visibility = RightTarget.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        RightZoneShade.Visibility = RightZoneLabel.Visibility = RightZone.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SetDeadZoneRing(_leftPreview, LeftDead.Value);
        SetDeadZoneRing(_rightPreview, RightDead.Value);
    }

    private void SaveTouch()
    {
        AppState.Instance.UpdateProfile(p => p with
        {
            Touchpad = p.Touchpad with
            {
                Mode = TouchMouse.IsChecked == true ? TouchpadMode.Mouse : TouchpadMode.None,
                Sensitivity = Math.Round(Sensitivity.Value, 2),
                Acceleration = TouchAccel.IsChecked == true,
                TapToClick = TapClick.IsChecked == true,
                TwoFingerScroll = TwoFingerScroll.IsChecked == true,
                RightClickZone = RightZone.IsChecked == true,
            },
        });
    }

    private void SaveSticks()
    {
        AppState.Instance.UpdateProfile(p => p with
        {
            LeftStick = ReadStick(p.LeftStick, LeftTarget, LeftDead, LeftCurve, LeftMouseSpeed, LeftInvert),
            RightStick = ReadStick(p.RightStick, RightTarget, RightDead, RightCurve, RightMouseSpeed, RightInvert),
            Triggers = p.Triggers with { DeadZone = (int)TriggerDead.Value },
        });
    }

    private static StickSettings ReadStick(StickSettings s, ComboBox target, Slider dead, Slider curve, Slider speed, Wpf.Ui.Controls.ToggleSwitch invert) => s with
    {
        Target = target.SelectedIndex >= 0 ? Targets[target.SelectedIndex].Target : s.Target,
        DeadZone = Math.Round(dead.Value, 2),
        Curve = Math.Round(curve.Value, 2),
        MouseSpeed = speed.Value,
        InvertY = invert.IsChecked == true,
    };

    private void Run(Action save)
    {
        if (!IsLoaded || _loading) return;
        _saving = true;
        try { save(); }
        finally { _saving = false; }
        UpdateLabels();
    }

    private void OnTouchChanged(object sender, RoutedEventArgs e) => Run(SaveTouch);
    private void OnTouchSlider(object sender, RoutedPropertyChangedEventArgs<double> e) => Run(SaveTouch);
    private void OnStickChanged(object sender, SelectionChangedEventArgs e) => Run(SaveSticks);
    private void OnStickSlider(object sender, RoutedPropertyChangedEventArgs<double> e) => Run(SaveSticks);
    private void OnStickToggle(object sender, RoutedEventArgs e) => Run(SaveSticks);
    private void OnTriggerSlider(object sender, RoutedPropertyChangedEventArgs<double> e) => Run(SaveSticks);

    private void UpdateLive()
    {
        var state = AppState.Instance.PrimarySession?.State ?? new DualSenseState { LeftX = 128, LeftY = 128, RightX = 128, RightY = 128 };
        PlaceFinger(Finger0, state.Touch0);
        PlaceFinger(Finger1, state.Touch1);
        MovePreview(_leftPreview, state.LeftX, state.LeftY, Profile.LeftStick);
        MovePreview(_rightPreview, state.RightX, state.RightY, Profile.RightStick);
        L2Bar.Value = state.L2;
        R2Bar.Value = state.R2;
    }

    private void PlaceFinger(Ellipse dot, TouchPoint touch)
    {
        dot.Visibility = touch.Active ? Visibility.Visible : Visibility.Hidden;
        if (!touch.Active) return;
        Canvas.SetLeft(dot, touch.X / (double)DualSenseState.TouchpadWidth * 288 - 11);
        Canvas.SetTop(dot, touch.Y / (double)DualSenseState.TouchpadHeight * 162 - 11);
    }

    private static StickPreview BuildPreview(Canvas canvas)
    {
        var stroke = new SolidColorBrush(Color.FromArgb(0x60, 0x9C, 0xA3, 0xAF));
        canvas.Children.Add(new Ellipse { Width = 110, Height = 110, Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x9C, 0xA3, 0xAF)), Stroke = stroke });
        canvas.Children.Add(new Line { X1 = 55, Y1 = 4, X2 = 55, Y2 = 106, Stroke = stroke });
        canvas.Children.Add(new Line { X1 = 4, Y1 = 55, X2 = 106, Y2 = 55, Stroke = stroke });
        var dead = new Ellipse { Fill = new SolidColorBrush(Color.FromArgb(0x40, 0xF5, 0x9E, 0x0B)) };
        var raw = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)) };
        var output = new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x8E, 0xFF)), Stroke = Brushes.White, StrokeThickness = 2 };
        canvas.Children.Add(dead);
        canvas.Children.Add(raw);
        canvas.Children.Add(output);
        return new StickPreview { DeadZone = dead, Raw = raw, Output = output };
    }

    private static void SetDeadZoneRing(StickPreview preview, double deadZone)
    {
        var size = Math.Max(2, deadZone * 110);
        preview.DeadZone.Width = preview.DeadZone.Height = size;
        Canvas.SetLeft(preview.DeadZone, 55 - size / 2);
        Canvas.SetTop(preview.DeadZone, 55 - size / 2);
    }

    private static void MovePreview(StickPreview preview, byte x, byte y, StickSettings settings)
    {
        var rx = (x - 127.5) / 127.5;
        var ry = (y - 127.5) / 127.5;
        Canvas.SetLeft(preview.Raw, 55 + rx * 50 - 5);
        Canvas.SetTop(preview.Raw, 55 + ry * 50 - 5);

        var (ox, oy) = MappingEngine.NormalizeStick(x, y, settings);
        Canvas.SetLeft(preview.Output, 55 + ox * 50 - 7);
        Canvas.SetTop(preview.Output, 55 - oy * 50 - 7);
    }
}
