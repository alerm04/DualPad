using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DualPad.App.Services;
using DualPad.Core.DualSense;
using DualPad.Core.Mapping;

namespace DualPad.App.Pages;

public partial class MappingPage : UserControl
{
    private static readonly (string Title, XboxControl[] Controls)[] XboxGroups =
    [
        ("Pulsanti", [XboxControl.A, XboxControl.B, XboxControl.X, XboxControl.Y]),
        ("Dorsali e grilletti", [XboxControl.LeftBumper, XboxControl.RightBumper, XboxControl.LeftTrigger, XboxControl.RightTrigger]),
        ("Sistema", [XboxControl.Back, XboxControl.Start, XboxControl.Guide]),
        ("Pressione stick", [XboxControl.LeftThumb, XboxControl.RightThumb]),
        ("Croce direzionale", [XboxControl.DpadUp, XboxControl.DpadDown, XboxControl.DpadLeft, XboxControl.DpadRight]),
    ];

    private static readonly int[] CommonKeyCodes =
    [
        0x20, 0x0D, 0x1B, 0x09, 0xA0, 0xA2, 0xA4,  // space, enter, esc, tab, shift, ctrl, alt
        0x57, 0x41, 0x53, 0x44, 0x45, 0x51, 0x52, 0x46, 0x43, 0x56, // W A S D E Q R F C V
        0x31, 0x32, 0x33, 0x34, // 1-4
        0x26, 0x28, 0x25, 0x27, // arrows
        0x5B, 0x2C, 0xB3, 0xAF, 0xAE, 0xAD, // win, print screen, play/pause, vol+, vol-, mute
    ];

    private readonly DispatcherTimer _detectTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly List<(RadioButton Chip, ButtonAction Action)> _chips = [];
    private DsButtons _selected = DsButtons.Cross;
    private DsButtons _previousButtons;

    public MappingPage()
    {
        InitializeComponent();
        BuildXboxChoices();
        BuildMouseChoices();
        BuildCommonKeys();

        Controller.ButtonClicked += Select;
        _detectTimer.Tick += (_, _) => DetectPhysicalPress();
        Loaded += (_, _) =>
        {
            AppState.Instance.PropertyChanged += OnAppStateChanged;
            _previousButtons = AppState.Instance.PrimarySession?.State.Buttons ?? DsButtons.None;
            _detectTimer.Start();
            Select(_selected);
        };
        Unloaded += (_, _) =>
        {
            AppState.Instance.PropertyChanged -= OnAppStateChanged;
            _detectTimer.Stop();
        };
    }

    private static Profile Profile => AppState.Instance.ActiveProfile;

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.ActiveProfile) or nameof(AppState.PrimarySession)) Refresh();
    }

    private void Select(DsButtons button)
    {
        _selected = button;
        Refresh();
        var action = Profile.GetAction(button);
        // Open the tab that matches the current assignment.
        (action.Kind switch
        {
            ActionKind.Keyboard => TabKeyboard,
            ActionKind.Mouse => TabMouse,
            ActionKind.None => TabNone,
            _ => TabXbox,
        }).IsChecked = true;
    }

    private void DetectPhysicalPress()
    {
        var session = AppState.Instance.PrimarySession;
        if (session is null || DetectSwitch.IsChecked != true || KeyCapture.IsKeyboardFocused) return;

        var buttons = session.State.Buttons;
        var pressed = buttons & ~_previousButtons;
        _previousButtons = buttons;
        if (pressed == DsButtons.None) return;

        foreach (var candidate in Profile.MappableButtons)
        {
            if ((pressed & candidate) == 0) continue;
            if (candidate != _selected) Select(candidate);
            return;
        }
    }

    private void Refresh()
    {
        var action = Profile.GetAction(_selected);
        Controller.SelectedButton = _selected;
        SelectedGlyph.Text = Labels.Glyph(_selected);
        SelectedGlyph.FontSize = SelectedGlyph.Text.Length > 2 ? 14 : 22;
        SelectedGlyph.Foreground = new SolidColorBrush(Labels.GlyphColor(_selected));
        SelectedName.Text = Labels.Name(_selected);
        SelectedAction.Text = Labels.Long(action);

        var standard = Profile.XboxLayout().GetValueOrDefault(_selected, ButtonAction.None);
        DefaultHint.Text = "Predefinito: " + Labels.Long(standard);

        foreach (var (chip, chipAction) in _chips) chip.IsChecked = chipAction == action;
        KeyCaptureText.Text = action.Kind == ActionKind.Keyboard ? Labels.KeyName(action.Key) : "Clicca qui e premi un tasto";

        RefreshOverview();
    }

    private void RefreshOverview()
    {
        Overview.Children.Clear();
        var isEdge = AppState.Instance.PrimarySession?.Device.IsEdge == true;
        foreach (var button in Profile.MappableButtons)
        {
            var action = Profile.GetAction(button);
            var edgeOnly = button is DsButtons.LeftFn or DsButtons.RightFn or DsButtons.LeftPaddle or DsButtons.RightPaddle;
            if (edgeOnly && !isEdge && action.Kind == ActionKind.None) continue;
            Overview.Children.Add(MakeOverviewChip(button, action));
        }
    }

    private UIElement MakeOverviewChip(DsButtons button, ButtonAction action)
    {
        var changed = action != Profile.XboxLayout().GetValueOrDefault(button, ButtonAction.None);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = Labels.Glyph(button),
            Foreground = new SolidColorBrush(Labels.GlyphColor(button)),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock { Text = "  →  ", Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(Labels.BadgeColor(action)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 0, 6, 1),
            Child = new TextBlock { Text = Labels.Short(action), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12 },
        });

        var chip = new Border
        {
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(10, 5, 10, 5),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(button == _selected ? 2 : 1),
            BorderBrush = button == _selected ? (Brush)FindResource("BrandBrush")
                : changed ? new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)) : (Brush)FindResource("ControlStrokeColorDefaultBrush"),
            Background = (Brush)FindResource("ControlFillColorDefaultBrush"),
            Cursor = Cursors.Hand,
            Child = content,
            ToolTip = Labels.Name(button) + " · " + Labels.Long(action) + (changed ? " (personalizzato)" : ""),
        };
        chip.MouseLeftButtonUp += (_, _) => Select(button);
        return chip;
    }

    private void BuildXboxChoices()
    {
        foreach (var (title, controls) in XboxGroups)
        {
            XboxPanel.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle"), Margin = new Thickness(0, 4, 0, 8) });
            var wrap = new WrapPanel();
            foreach (var control in controls)
            {
                var action = ButtonAction.ToXbox(control);
                wrap.Children.Add(MakeChip(action, XboxChipContent(control), Labels.Description(control)));
            }
            XboxPanel.Children.Add(wrap);
        }
    }

    private static UIElement XboxChipContent(XboxControl control)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = new SolidColorBrush(Labels.XboxColor(control)),
            Child = new TextBlock
            {
                Text = Labels.Name(control),
                FontSize = Labels.Name(control).Length > 2 ? 9 : 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });
        return panel;
    }

    private void BuildMouseChoices()
    {
        foreach (var mouse in Enum.GetValues<MouseAction>())
            MouseChoices.Children.Add(MakeChip(ButtonAction.ToMouse(mouse), new TextBlock { Text = Labels.Name(mouse) }, null));
    }

    private void BuildCommonKeys()
    {
        foreach (var vk in CommonKeyCodes)
            CommonKeys.Children.Add(MakeChip(ButtonAction.ToKey(vk), new TextBlock { Text = Labels.KeyName(vk) }, null, compact: true));
    }

    private RadioButton MakeChip(ButtonAction action, object content, string? tooltip, bool compact = false)
    {
        var chip = new RadioButton
        {
            Style = (Style)FindResource("ChoiceChip"),
            Content = content,
            ToolTip = tooltip,
            GroupName = "Target",
            Padding = compact ? new Thickness(10, 6, 10, 6) : new Thickness(12, 8, 12, 8),
        };
        chip.Click += (_, _) => Assign(action);
        _chips.Add((chip, action));
        return chip;
    }

    private void Assign(ButtonAction action)
    {
        AppState.Instance.SetButton(_selected, action);
        Refresh();
    }

    private void OnKindTab(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        XboxPanel.Visibility = TabXbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        KeyboardPanel.Visibility = TabKeyboard.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        MousePanel.Visibility = TabMouse.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        NonePanel.Visibility = TabNone.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (TabNone.IsChecked == true && Profile.GetAction(_selected).Kind != ActionKind.None) Assign(ButtonAction.None);
    }

    private void OnKeyCaptureClick(object sender, MouseButtonEventArgs e) => KeyCapture.Focus();

    private void OnKeyCaptureFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        KeyCapture.BorderBrush = (Brush)FindResource("BrandBrush");
        KeyCaptureText.Text = "Premi un tasto…";
        KeyCaptureHint.Text = "Esc assegna Esc · clicca altrove per annullare";
    }

    private void OnKeyCaptureBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        KeyCapture.BorderBrush = (Brush)FindResource("ControlStrokeColorDefaultBrush");
        KeyCaptureHint.Text = "Il tasto premuto verrà assegnato";
        Refresh();
    }

    private void OnKeyCaptured(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        e.Handled = true;
        if (key == Key.None) return;
        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;
        Assign(ButtonAction.ToKey(vk));
        Keyboard.ClearFocus();
        Controller.Focus();
    }

    private void OnResetButton(object sender, RoutedEventArgs e)
    {
        Assign(Profile.XboxLayout().GetValueOrDefault(_selected, ButtonAction.None));
        Select(_selected);
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show("Riportare tutti i tasti del profilo al layout Xbox standard?", "Ripristina layout Xbox",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        AppState.Instance.UpdateProfile(p => p with { Buttons = Profile.XboxLayout() });
        Select(_selected);
    }
}
