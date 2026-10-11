using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace MyDownloader.App.Services
{
    /// <summary>
    /// 轻量 Toast 通知：屏幕右下角、无边框、自动关闭，支持点击动作与手动关闭。
    /// Avalonia TrayIcon 无气泡 API，故自绘。多个 Toast 会向上堆叠。
    /// </summary>
    public static class NotificationService
    {
        private static readonly List<Window> ActiveToasts = new();

        /// <summary>
        /// 显示一条 Toast。
        /// </summary>
        /// <param name="title">标题。</param>
        /// <param name="message">正文。</param>
        /// <param name="onClick">点击 Toast（非关闭按钮）时的动作；为 null 则仅关闭。</param>
        /// <param name="autoCloseSeconds">自动关闭秒数；小于等于 0 表示不自动关闭。</param>
        public static void Show(string title, string message, Action? onClick = null, double autoCloseSeconds = 6)
        {
            Dispatcher.UIThread.Post(() => ShowCore(title, message, onClick, autoCloseSeconds));
        }

        private static void ShowCore(string title, string message, Action? onClick, double autoCloseSeconds)
        {
            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };
            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                FontSize = 12
            };

            var closeButton = new Button
            {
                Content = "✕",
                FontSize = 11,
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var body = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 18, 0) };
            body.Children.Add(titleText);
            body.Children.Add(messageText);

            var grid = new Grid();
            grid.Children.Add(body);
            grid.Children.Add(closeButton);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(242, 32, 32, 32)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 12),
                BoxShadow = BoxShadows.Parse("0 4 16 0 #40000000"),
                MaxWidth = 340,
                Child = grid
            };

            var win = new Window
            {
                WindowDecorations = WindowDecorations.None,
                ShowInTaskbar = false,
                CanResize = false,
                Topmost = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                Background = Brushes.Transparent,
                Content = border,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            DispatcherTimer? timer = null;

            void Close()
            {
                timer?.Stop();
                if (ActiveToasts.Remove(win))
                {
                    win.Close();
                    Reposition();
                }
            }

            closeButton.Click += (_, _) => Close();

            // 点击 Toast 主体（非关闭按钮）触发动作后关闭。
            win.PointerPressed += (_, e) =>
            {
                onClick?.Invoke();
                Close();
            };

            win.Opened += (_, _) =>
            {
                ActiveToasts.Add(win);
                Reposition();

                if (autoCloseSeconds > 0)
                {
                    timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSeconds) };
                    timer.Tick += (_, _) => Close();
                    timer.Start();
                }
            };

            win.Show();
        }

        /// <summary>把所有 Toast 从屏幕右下角向上堆叠排布。</summary>
        private static void Reposition()
        {
            var screen = ActiveToasts.Count > 0 ? ActiveToasts[0].Screens.Primary : null;
            if (screen == null) return;

            var wa = screen.WorkingArea;
            int bottom = wa.Y + wa.Height - 20;
            foreach (var w in ActiveToasts)
            {
                var h = (int)w.Bounds.Height;
                var wd = (int)w.Bounds.Width;
                bottom -= h;
                w.Position = new PixelPoint(wa.X + wa.Width - wd - 20, bottom);
                bottom -= 10;
            }
        }
    }
}
