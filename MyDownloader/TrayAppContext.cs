using MyDownloader.Classes;
using System.Windows.Forms;

namespace MyDownloader
{
    /// <summary>
    /// 应用上下文：承载托盘图标、主窗口生命周期与统一刷新定时器。
    /// </summary>
    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private MainForm? _mainForm;
        private readonly ToolStripMenuItem _pauseAllItem;
        private readonly ToolStripMenuItem _resumeAllItem;
        private readonly ToolStripMenuItem _clipboardItem;
        private bool _exiting;
        private string? _pendingClipboardUrl;
        private string? _lastCompletedPath;

        public TrayAppContext()
        {
            DownloadManager.Instance.LoadPersistedTasks();

            _tray = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "MyDownloader · 空闲",
                Visible = true
            };
            _tray.DoubleClick += (_, _) => ShowMainForm();

            _tray.BalloonTipClicked += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_pendingClipboardUrl))
                {
                    var url = _pendingClipboardUrl;
                    _pendingClipboardUrl = null;
                    ShowMainForm();
                    _mainForm?.PromptAddUrl(url);
                    return;
                }
                if (!string.IsNullOrEmpty(_lastCompletedPath) && File.Exists(_lastCompletedPath))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_lastCompletedPath}\"");
                _lastCompletedPath = null;
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, (_, _) => ShowMainForm());
            _pauseAllItem = new ToolStripMenuItem("暂停全部", null, (_, _) => DownloadManager.Instance.PauseAll());
            _resumeAllItem = new ToolStripMenuItem("继续全部", null, (_, _) => DownloadManager.Instance.ResumeAll());
            menu.Items.Add(_pauseAllItem);
            menu.Items.Add(_resumeAllItem);
            _clipboardItem = new ToolStripMenuItem("剪贴板监听", null, (_, _) =>
            {
                var s = DownloadManager.Instance.Settings;
                s.ClipboardMonitorEnabled = !s.ClipboardMonitorEnabled;
                _clipboardItem.Checked = s.ClipboardMonitorEnabled;
                DownloadManager.Instance.SaveSettings();
                _mainForm?.ApplyClipboardSetting(s.ClipboardMonitorEnabled);
            })
            { CheckOnClick = false, Checked = DownloadManager.Instance.Settings.ClipboardMonitorEnabled };
            menu.Items.Add(_clipboardItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => ExitApp());
            _tray.ContextMenuStrip = menu;

            DownloadManager.Instance.TaskCompleted += OnTaskCompleted;

            _timer = new System.Windows.Forms.Timer { Interval = 500 };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();

            ShowMainForm();

            Microsoft.Win32.SystemEvents.SessionEnding += (_, _) => ExitApp();
        }

        /// <summary>主窗口隐藏时，剪贴板检测到链接则用托盘气泡提示。</summary>
        public void NotifyClipboardUrl(string url)
        {
            _pendingClipboardUrl = url;
            _tray.ShowBalloonTip(3000, "检测到下载链接", "点击气泡添加任务", ToolTipIcon.Info);
        }

        private void ShowMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm { AppContext = this };
            }
            _mainForm.Show();
            if (_mainForm.WindowState == FormWindowState.Minimized)
                _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        }

        /// <summary>由 MainForm 在关闭时调用：隐藏而非退出。</summary>
        public void HideMainForm()
        {
            if (_mainForm == null) return;
            _mainForm.Hide();
            var s = DownloadManager.Instance.Settings;
            if (!s.TrayTipShown)
            {
                s.TrayTipShown = true;
                DownloadManager.Instance.SaveSettings();
                _tray.ShowBalloonTip(3000, "MyDownloader", "已最小化到托盘，下载继续在后台进行", ToolTipIcon.Info);
            }
        }

        private void Refresh()
        {
            var snapshots = DownloadManager.Instance.GetSnapshots();
            var active = snapshots.Where(x => x.IsActive).ToList();
            var hasRunning = snapshots.Any(x => x.Status == Downloader.DownloadStatus.Running);
            var hasPaused = snapshots.Any(x => x.Status == Downloader.DownloadStatus.Paused);

            _pauseAllItem.Enabled = hasRunning;
            _resumeAllItem.Enabled = hasPaused;

            if (active.Count == 0)
            {
                _tray.Text = "MyDownloader · 空闲";
            }
            else
            {
                var total = active.Sum(x => x.TotalBytes);
                var recv = active.Sum(x => x.ReceivedBytes);
                var pct = total > 0 ? (int)(recv * 100 / total) : 0;
                var speed = active.Sum(x => x.SpeedBytesPerSecond);
                _tray.Text = $"总进度 {pct}% · {active.Count} 个任务 · ↓ {PathTools.FormatSpeed(speed)}";
            }
            if (_tray.Text.Length > 63) _tray.Text = _tray.Text.Substring(0, 63);

            _mainForm?.RefreshFromSnapshots(snapshots);
        }

        private void OnTaskCompleted(object? sender, string id)
        {
            var snap = DownloadManager.Instance.GetSnapshot(id);
            if (snap == null) return;
            if (snap.Status == Downloader.DownloadStatus.Completed)
            {
                _lastCompletedPath = snap.FilePath;
                _tray.ShowBalloonTip(3000, "下载完成", snap.FileName, ToolTipIcon.Info);
            }
            else if (snap.Status == Downloader.DownloadStatus.Failed)
                _tray.ShowBalloonTip(3000, "下载失败", snap.FileName, ToolTipIcon.Error);
        }

        private void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            _timer.Stop();
            _timer.Dispose();
            DownloadManager.Instance.Shutdown();
            _tray.Visible = false;
            _tray.Dispose();
            _mainForm?.ForceClose();
            ExitThreadCore();
        }
    }
}
