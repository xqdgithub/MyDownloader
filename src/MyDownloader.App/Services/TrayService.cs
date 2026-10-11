using Avalonia.Controls;
using Avalonia.Threading;
using MyDownloader.Core;

namespace MyDownloader.App.Services
{
    /// <summary>托盘图标与菜单（单击显示主窗口，Tooltip 显示总进度）。</summary>
    public class TrayService : IDisposable
    {
        private readonly TrayIcon _tray;
        private readonly NativeMenuItem _pauseAll;
        private readonly NativeMenuItem _resumeAll;
        private readonly DispatcherTimer _timer;
        private readonly Action _showMainWindow;
        private readonly Action _exit;

        public TrayService(Action showMainWindow, Action exit)
        {
            _showMainWindow = showMainWindow;
            _exit = exit;

            var menu = new NativeMenu();
            var show = new NativeMenuItem("显示主窗口");
            show.Click += (_, _) => _showMainWindow();
            menu.Add(show);

            _pauseAll = new NativeMenuItem("暂停全部");
            _pauseAll.Click += (_, _) => DownloadManager.Instance.PauseAll();
            menu.Add(_pauseAll);

            _resumeAll = new NativeMenuItem("继续全部");
            _resumeAll.Click += (_, _) => DownloadManager.Instance.ResumeAll();
            menu.Add(_resumeAll);

            menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) => _exit();
            menu.Add(exitItem);

            _tray = new TrayIcon
            {
                ToolTipText = "MyDownloader · 空闲",
                Menu = menu,
                IsVisible = true
            };
            _tray.Clicked += (_, _) => _showMainWindow();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => RefreshTooltip();
            _timer.Start();
        }

        private void RefreshTooltip()
        {
            var snaps = DownloadManager.Instance.GetSnapshots();
            var active = snaps.Where(s => s.IsActive).ToList();
            var hasRunning = snaps.Any(s => s.Status == Downloader.DownloadStatus.Running);
            var hasPaused = snaps.Any(s => s.Status == Downloader.DownloadStatus.Paused);

            _pauseAll.IsEnabled = hasRunning;
            _resumeAll.IsEnabled = hasPaused;

            string text;
            if (active.Count == 0)
            {
                text = "MyDownloader · 空闲";
            }
            else
            {
                var total = active.Sum(s => s.TotalBytes);
                var recv = active.Sum(s => s.ReceivedBytes);
                var pct = total > 0 ? (int)(recv * 100 / total) : 0;
                var speed = active.Sum(s => s.SpeedBytesPerSecond);
                text = $"总进度 {pct}% · {active.Count} 个任务 · ↓ {PathTools.FormatSpeed(speed)}";
            }
            _tray.ToolTipText = text.Length > 63 ? text[..63] : text;
        }

        public void Dispose()
        {
            _timer.Stop();
            _tray.IsVisible = false;
            _tray.Dispose();
        }
    }
}
