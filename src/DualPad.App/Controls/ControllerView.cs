using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DualPad.App.Services;
using DualPad.Core;
using DualPad.Core.DualSense;
using DualPad.Core.Mapping;

namespace DualPad.App.Controls;

/// <summary>
/// Vector drawing of a DualSense that mirrors the live controller state. In interactive mode every
/// control is clickable and shows a badge with what it is mapped to.
/// </summary>
public sealed class ControllerView : UserControl
{
    private const double BodyTop = 50;
    private static readonly Rect TouchArea = new(272, 66 + BodyTop, 216, 130);

    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive), typeof(bool), typeof(ControllerView), new PropertyMetadata(false, (d, _) => ((ControllerView)d).RefreshBadges()));

    public static readonly DependencyProperty SelectedButtonProperty = DependencyProperty.Register(
        nameof(SelectedButton), typeof(DsButtons), typeof(ControllerView),
        new FrameworkPropertyMetadata(DsButtons.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ControllerView)d).RefreshSelection()));

    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(Profile), typeof(ControllerView), new PropertyMetadata(null, (d, _) => ((ControllerView)d).RefreshBadges()));

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    public DsButtons SelectedButton
    {
        get => (DsButtons)GetValue(SelectedButtonProperty);
        set => SetValue(SelectedButtonProperty, value);
    }

    public Profile? Profile
    {
        get => (Profile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    /// <summary>Raised when the user clicks a control on the drawing.</summary>
    public event Action<DsButtons>? ButtonClicked;

    private sealed class Part
    {
        public required DsButtons Button;
        public required Shape Shape;
        public required Brush Normal;
        public required Point BadgeAnchor;
        public Border? Badge;
        public bool Pressed;
    }

    private readonly Canvas _canvas = new() { Width = 760, Height = 520 };
    private readonly Dictionary<DsButtons, Part> _parts = [];
    private readonly Ellipse _leftCap, _rightCap;
    private readonly Ellipse _touch0, _touch1;
    private readonly Rectangle _l2Fill, _r2Fill;
    private readonly Path _lightLeft, _lightRight;
    private readonly Brush _pressedBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3B, 0x8E, 0xFF)));
    private readonly Brush _selectedStroke = Frozen(new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)));
    private readonly Brush _hoverStroke = Frozen(new SolidColorBrush(Color.FromArgb(0xC0, 0x9C, 0xD8, 0xFF)));
    private DualSenseState _lastState;
    private Color _lastLight;
    private bool _hasState;

    public ControllerView()
    {
        var shadow = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = 0.35, Direction = 270 };

        // Triggers and bumpers sit above the body.
        _l2Fill = AddTrigger(DsButtons.L2, 150, new Point(122, 28));
        _r2Fill = AddTrigger(DsButtons.R2, 500, new Point(638, 28));
        AddRoundRect(DsButtons.L1, 152, 62, 106, 22, 10, Dark, new Point(126, 74));
        AddRoundRect(DsButtons.R1, 502, 62, 106, 22, 10, Dark, new Point(634, 74));

        var body = new Path
        {
            Data = Geometry.Parse("M 190,60 C 260,48 500,48 570,60 C 640,72 690,110 708,180 C 730,260 745,350 735,410 C 728,455 690,475 655,460 C 615,445 590,400 565,355 C 545,322 520,310 480,310 L 280,310 C 240,310 215,322 195,355 C 170,400 145,445 105,460 C 70,475 32,455 25,410 C 15,350 30,260 52,180 C 70,110 120,72 190,60 Z"),
            Fill = Frozen(new LinearGradientBrush(Color.FromRgb(0xFA, 0xFB, 0xFC), Color.FromRgb(0xD5, 0xD9, 0xE0), 90)),
            Stroke = Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0xBE, 0xC8))),
            StrokeThickness = 1.5,
            Effect = shadow,
            RenderTransform = new TranslateTransform(0, BodyTop),
        };
        _canvas.Children.Add(body);

        var plate = new Path
        {
            Data = Geometry.Parse("M 255,58 L 505,58 C 515,120 530,200 535,250 C 538,290 520,310 480,310 L 280,310 C 240,310 222,290 225,250 C 230,200 245,120 255,58 Z"),
            Fill = Frozen(new LinearGradientBrush(Color.FromRgb(0x24, 0x27, 0x2E), Color.FromRgb(0x0E, 0x10, 0x14), 90)),
            RenderTransform = new TranslateTransform(0, BodyTop),
            IsHitTestVisible = false,
        };
        _canvas.Children.Add(plate);

        _lightLeft = AddLightStrip("M 258,74 L 270,196");
        _lightRight = AddLightStrip("M 502,74 L 490,196");

        // Touchpad (clickable as the touchpad button).
        var touchpad = new Path
        {
            Data = Geometry.Parse("M 272,66 L 488,66 Q 500,66 498,80 L 488,182 Q 486,196 472,196 L 288,196 Q 274,196 272,182 L 262,80 Q 260,66 272,66 Z"),
            RenderTransform = new TranslateTransform(0, BodyTop),
            Stroke = Frozen(new SolidColorBrush(Color.FromRgb(0xC4, 0xC9, 0xD1))),
            StrokeThickness = 1,
        };
        var touchFill = Frozen(new LinearGradientBrush(Color.FromRgb(0xF4, 0xF5, 0xF7), Color.FromRgb(0xE1, 0xE4, 0xE9), 90));
        AddPart(DsButtons.TouchpadClick, touchpad, touchFill, new Point(380, 66 + BodyTop + 8));

        _touch0 = AddTouchDot();
        _touch1 = AddTouchDot();

        AddRoundRect(DsButtons.Create, 228, 70 + BodyTop, 14, 30, 7, Dark, new Point(222, 60 + BodyTop));
        AddRoundRect(DsButtons.Options, 518, 70 + BodyTop, 14, 30, 7, Dark, new Point(538, 60 + BodyTop));

        // D-pad.
        const double dx = 170, dy = 175 + BodyTop;
        AddRoundRect(DsButtons.DpadUp, dx - 15, dy - 52, 30, 38, 6, Dark, new Point(dx, dy - 62));
        AddRoundRect(DsButtons.DpadDown, dx - 15, dy + 14, 30, 38, 6, Dark, new Point(dx, dy + 62));
        AddRoundRect(DsButtons.DpadLeft, dx - 52, dy - 15, 38, 30, 6, Dark, new Point(dx - 70, dy));
        AddRoundRect(DsButtons.DpadRight, dx + 14, dy - 15, 38, 30, 6, Dark, new Point(dx + 70, dy));
        AddGlyph("▲", dx, dy - 34, 11); AddGlyph("▼", dx, dy + 33, 11); AddGlyph("◀", dx - 33, dy, 11); AddGlyph("▶", dx + 33, dy, 11);

        // Face buttons.
        const double fx = 590, fy = 175 + BodyTop;
        AddFaceButton(DsButtons.Triangle, fx, fy - 44, new Point(fx, fy - 76));
        AddFaceButton(DsButtons.Cross, fx, fy + 44, new Point(fx, fy + 76));
        AddFaceButton(DsButtons.Square, fx - 44, fy, new Point(fx - 82, fy));
        AddFaceButton(DsButtons.Circle, fx + 44, fy, new Point(fx + 82, fy));

        // Sticks: the cap is the L3/R3 button and moves with the stick.
        _leftCap = AddStick(DsButtons.L3, 290, 270 + BodyTop, new Point(232, 300 + BodyTop));
        _rightCap = AddStick(DsButtons.R3, 470, 270 + BodyTop, new Point(528, 300 + BodyTop));

        var ps = new Ellipse { Width = 26, Height = 26 };
        Place(ps, 380 - 13, 262 + BodyTop - 13);
        AddPart(DsButtons.PS, ps, Dark, new Point(380, 236 + BodyTop));
        AddGlyph("PS", 380, 262 + BodyTop, 9);

        AddRoundRect(DsButtons.Mute, 366, 290 + BodyTop, 28, 9, 4.5, Dark, new Point(380, 312 + BodyTop));

        Content = new Viewbox { Child = _canvas, Stretch = Stretch.Uniform };
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnRendering;
            AppState.Instance.PropertyChanged += OnAppStateChanged;
            RefreshBadges();
        };
        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            AppState.Instance.PropertyChanged -= OnAppStateChanged;
        };
    }

    private static Brush Dark { get; } = Frozen(new LinearGradientBrush(Color.FromRgb(0x4A, 0x50, 0x5A), Color.FromRgb(0x2C, 0x30, 0x37), 90));

    private void OnAppStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.ActiveProfile) && GetBindingExpression(ProfileProperty) is null) RefreshBadges();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var session = AppState.Instance.PrimarySession;
        var state = session?.State ?? new DualSenseState { LeftX = 128, LeftY = 128, RightX = 128, RightY = 128 };
        if (!_hasState || !state.Equals(_lastState))
        {
            _lastState = state;
            _hasState = true;
            ApplyState(state);
        }

        var light = LightColor(session);
        if (light != _lastLight)
        {
            _lastLight = light;
            var brush = Frozen(new SolidColorBrush(light));
            _lightLeft.Stroke = brush;
            _lightRight.Stroke = brush;
            ((DropShadowEffect)_lightLeft.Effect).Color = light;
            ((DropShadowEffect)_lightRight.Effect).Color = light;
        }
    }

    private static Color LightColor(ControllerSession? session)
    {
        if (session is null) return Color.FromRgb(0x30, 0x34, 0x3C);
        var light = session.Profile.Lightbar;
        if (light.ShowBattery)
        {
            var (r, g, b) = ControllerSession.BatteryColor(session.Battery);
            return Color.FromRgb(r, g, b);
        }
        return Color.FromRgb(light.R, light.G, light.B);
    }

    private void ApplyState(in DualSenseState state)
    {
        foreach (var part in _parts.Values)
        {
            var pressed = part.Button switch
            {
                DsButtons.L2 => state.L2 > 20,
                DsButtons.R2 => state.R2 > 20,
                _ => state.IsPressed(part.Button),
            };
            if (pressed == part.Pressed) continue;
            part.Pressed = pressed;
            part.Shape.Fill = pressed ? _pressedBrush : part.Normal;
        }

        MoveCap(_leftCap, 290, 270 + BodyTop, state.LeftX, state.LeftY);
        MoveCap(_rightCap, 470, 270 + BodyTop, state.RightX, state.RightY);
        _l2Fill.Height = 40 * state.L2 / 255.0;
        _r2Fill.Height = 40 * state.R2 / 255.0;
        PlaceTouch(_touch0, state.Touch0);
        PlaceTouch(_touch1, state.Touch1);
    }

    private static void MoveCap(Ellipse cap, double cx, double cy, byte x, byte y)
    {
        Canvas.SetLeft(cap, cx - cap.Width / 2 + (x - 127.5) / 127.5 * 13);
        Canvas.SetTop(cap, cy - cap.Height / 2 + (y - 127.5) / 127.5 * 13);
    }

    private static void PlaceTouch(Ellipse dot, TouchPoint touch)
    {
        dot.Visibility = touch.Active ? Visibility.Visible : Visibility.Hidden;
        if (!touch.Active) return;
        Canvas.SetLeft(dot, TouchArea.X + touch.X / (double)DualSenseState.TouchpadWidth * TouchArea.Width - dot.Width / 2);
        Canvas.SetTop(dot, TouchArea.Y + touch.Y / (double)DualSenseState.TouchpadHeight * TouchArea.Height - dot.Height / 2);
    }

    private void RefreshBadges()
    {
        var profile = Profile ?? AppState.Instance.ActiveProfile;
        foreach (var part in _parts.Values)
        {
            if (part.Badge is not null)
            {
                _canvas.Children.Remove(part.Badge);
                part.Badge = null;
            }
            part.Shape.Cursor = IsInteractive ? Cursors.Hand : null;
            if (!IsInteractive) continue;

            var action = profile.GetAction(part.Button);
            part.Badge = MakeBadge(action, part.BadgeAnchor);
            _canvas.Children.Add(part.Badge);
        }
        RefreshSelection();
    }

    private Border MakeBadge(ButtonAction action, Point anchor)
    {
        var text = new TextBlock
        {
            Text = Labels.Short(action),
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var badge = new Border
        {
            Background = Frozen(new SolidColorBrush(Labels.BadgeColor(action))),
            BorderBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF))),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 1, 7, 2),
            MinWidth = 22,
            Height = 20,
            Child = text,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 },
        };
        badge.Loaded += (_, _) =>
        {
            Canvas.SetLeft(badge, anchor.X - badge.ActualWidth / 2);
            Canvas.SetTop(badge, anchor.Y - badge.ActualHeight / 2);
        };
        Canvas.SetLeft(badge, anchor.X - 11);
        Canvas.SetTop(badge, anchor.Y - 10);
        Panel.SetZIndex(badge, 100);
        return badge;
    }

    private void RefreshSelection()
    {
        foreach (var part in _parts.Values)
        {
            var selected = IsInteractive && part.Button == SelectedButton;
            part.Shape.Stroke = selected ? _selectedStroke : DefaultStroke(part);
            part.Shape.StrokeThickness = selected ? 3 : part.Button == DsButtons.TouchpadClick ? 1 : 0;
            part.Shape.Effect = selected ? new DropShadowEffect { Color = Color.FromRgb(0x4C, 0xC2, 0xFF), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 } : null;
        }
    }

    private static Brush? DefaultStroke(Part part) =>
        part.Button == DsButtons.TouchpadClick ? Frozen(new SolidColorBrush(Color.FromRgb(0xC4, 0xC9, 0xD1))) : null;

    private void AddPart(DsButtons button, Shape shape, Brush fill, Point badgeAnchor)
    {
        shape.Fill = fill;
        var part = new Part { Button = button, Shape = shape, Normal = fill, BadgeAnchor = badgeAnchor };
        _parts[button] = part;
        _canvas.Children.Add(shape);

        shape.MouseEnter += (_, _) =>
        {
            if (IsInteractive && SelectedButton != button) { shape.Stroke = _hoverStroke; shape.StrokeThickness = 2; }
        };
        shape.MouseLeave += (_, _) => RefreshSelection();
        shape.MouseLeftButtonUp += (_, e) =>
        {
            if (!IsInteractive) return;
            SelectedButton = button;
            ButtonClicked?.Invoke(button);
            e.Handled = true;
        };
    }

    private void AddRoundRect(DsButtons button, double x, double y, double w, double h, double r, Brush fill, Point badge)
    {
        var rect = new Rectangle { Width = w, Height = h, RadiusX = r, RadiusY = r };
        Place(rect, x, y);
        AddPart(button, rect, fill, badge);
    }

    private Rectangle AddTrigger(DsButtons button, double x, Point badge)
    {
        var shape = new Rectangle { Width = 110, Height = 46, RadiusX = 14, RadiusY = 14 };
        Place(shape, x, 6);
        AddPart(button, shape, Dark, badge);

        // Analog level, drawn inside the trigger from the bottom up.
        var fill = new Rectangle
        {
            Width = 98,
            Height = 0,
            RadiusX = 8,
            RadiusY = 8,
            Fill = Frozen(new SolidColorBrush(Color.FromArgb(0xD0, 0x3B, 0x8E, 0xFF))),
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        var host = new Grid { Width = 98, Height = 40, IsHitTestVisible = false };
        host.Children.Add(fill);
        Place(host, x + 6, 9);
        _canvas.Children.Add(host);
        AddGlyph(button == DsButtons.L2 ? "L2" : "R2", x + 55, 29, 13);
        return fill;
    }

    private void AddFaceButton(DsButtons button, double cx, double cy, Point badge)
    {
        var circle = new Ellipse { Width = 40, Height = 40 };
        Place(circle, cx - 20, cy - 20);
        AddPart(button, circle, Dark, badge);
        AddGlyph(Labels.Glyph(button), cx, cy, 20, Labels.GlyphColor(button));
    }

    private Ellipse AddStick(DsButtons button, double cx, double cy, Point badge)
    {
        var well = new Ellipse
        {
            Width = 84,
            Height = 84,
            Fill = Frozen(new RadialGradientBrush(Color.FromRgb(0x05, 0x06, 0x08), Color.FromRgb(0x1C, 0x1F, 0x25))),
            IsHitTestVisible = false,
        };
        Place(well, cx - 42, cy - 42);
        _canvas.Children.Add(well);

        var cap = new Ellipse { Width = 56, Height = 56 };
        Place(cap, cx - 28, cy - 28);
        var capFill = Frozen(new RadialGradientBrush(Color.FromRgb(0x50, 0x56, 0x60), Color.FromRgb(0x24, 0x27, 0x2D)) { GradientOrigin = new Point(0.4, 0.35) });
        AddPart(button, cap, capFill, badge);
        return cap;
    }

    private Path AddLightStrip(string data)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = 5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stroke = Brushes.Transparent,
            RenderTransform = new TranslateTransform(0, BodyTop),
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 0, Opacity = 1 },
            IsHitTestVisible = false,
        };
        _canvas.Children.Add(path);
        return path;
    }

    private Ellipse AddTouchDot()
    {
        var dot = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = Frozen(new SolidColorBrush(Color.FromArgb(0xE0, 0x3B, 0x8E, 0xFF))),
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };
        Panel.SetZIndex(dot, 50);
        _canvas.Children.Add(dot);
        return dot;
    }

    private void AddGlyph(string text, double cx, double cy, double size, Color? color = null)
    {
        var glyph = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeights.Bold,
            Foreground = Frozen(new SolidColorBrush(color ?? Color.FromRgb(0xE4, 0xE7, 0xEC))),
            IsHitTestVisible = false,
        };
        glyph.Loaded += (_, _) =>
        {
            Canvas.SetLeft(glyph, cx - glyph.ActualWidth / 2);
            Canvas.SetTop(glyph, cy - glyph.ActualHeight / 2);
        };
        Place(glyph, cx - size / 2, cy - size * 0.66);
        _canvas.Children.Add(glyph);
    }

    private static void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
