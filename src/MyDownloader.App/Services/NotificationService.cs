using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace MyDownloader.App.Services
{
    /// <summary>
    /// 轻量 Toast 通知：屏幕右下角、无边框、自动关闭。
    /// Avalonia TrayIcon 无气泡 API，故自绘。
    /// </summary>
    public static class NotificationService
    {
        public static void Show(string title, string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var text = new StackPanel { Spacing = 4 };
                text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold });
                text.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });

                var win = new Window
                {
                    SystemDecorations = WindowDecorations.None,
                    ShowInTaskbar = false,
                    CanResize = false,
                    Topmost = true,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Background = Brushes.Transparent,
                    Content = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(240, 32, 32, 32)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 12),
                        MaxWidth = 320,
                        Child = text
                    }
                };

                win.Opened += (_, _) =>
                {
                    var screen = win.Screens.Primary;
                    if (screen != null)
                    {
                        var wa = screen.WorkingArea;
                        win.Position = new PixelPoint(
                            wa.X + wa.Width - (int)win.Bounds.Width - 20,
                            wa.Y + wa.Height - (int)win.Bounds.Height - 20);
                    }
                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    t.Tick += (_, _) => { t.Stop(); win.Close(); };
                    t.Start();
                };
                win.Show();
            });
        }
    }
}
