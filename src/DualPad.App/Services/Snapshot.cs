using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DualPad.Core.DualSense;

namespace DualPad.App.Services;

/// <summary>Renders every page to PNG files (used to review the UI without driving the desktop).</summary>
internal static class Snapshot
{
    public static async void Run(string directory, bool live)
    {
        Directory.CreateDirectory(directory);
        var state = AppState.Instance;
        if (live)
        {
            state.MappingEnabled = false;
            state.Start();
            await Task.Delay(2500);
        }

        var window = new MainWindow
        {
            Width = 1280,
            Height = 840,
            Left = -20000,
            Top = 0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
        };
        window.Show();

        foreach (var page in new[] { "home", "mapping", "sticks", "lights", "profiles", "settings" })
        {
            window.Navigate(page);
            await Task.Delay(700);
            Save(window, Path.Combine(directory, page + ".png"));
        }

        Application.Current.Shutdown();
    }

    private static void Save(Window window, string file)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        var root = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        // Mica is drawn by the compositor, so paint the theme background behind the content first.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var background = (Brush?)Application.Current.TryFindResource("SolidBackgroundFillColorBaseBrush") ?? Brushes.Black;
            dc.DrawRectangle(background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        bitmap.Render(visual);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
