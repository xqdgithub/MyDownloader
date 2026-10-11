using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using MyDownloader.App.Services;
using MyDownloader.App.Views;
using MyDownloader.Core;

namespace MyDownloader.App
{
    public partial class App : Application
    {
        private TrayService? _tray;
        private MainWindow? _mainWindow;
        private ClipboardMonitor? _clipboardMonitor;
        private bool _exiting;

        /// <summary>在资源管理器中打开并选中文件。</summary>
        private static void OpenInExplorer(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch { /* 忽略 */ }
        }

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                DownloadManager.Instance.LoadPersistedTasks();
                ThemeService.Apply(DownloadManager.Instance.Settings);

                // 任务完成/失败时弹 Toast 通知；完成时点击可打开文件所在文件夹。
                DownloadManager.Instance.TaskCompleted += (_, id) =>
                {
                    var snap = DownloadManager.Instance.GetSnapshot(id);
                    if (snap == null) return;
                    if (snap.Status == Downloader.DownloadStatus.Completed)
                        NotificationService.Show("下载完成", snap.FileName,
                            onClick: () => OpenInExplorer(snap.FilePath));
                    else if (snap.Status == Downloader.DownloadStatus.Failed)
                        NotificationService.Show("下载失败", snap.FileName);
                };

                _mainWindow = new MainWindow();
                _mainWindow.Closing += (_, e) =>
                {
                    if (!_exiting)
                    {
                        e.Cancel = true;
                        _mainWindow.Hide();
                    }
                };

                var clip = new ClipboardMonitor(
                    onUrl: url =>
                    {
                        // 永不自动弹框（避免打断复制操作），只弹可点击的 Toast；
                        // 点击后显示主窗口并弹出添加任务对话框。
                        NotificationService.Show("检测到下载链接", url,
                            onClick: () =>
                            {
                                _mainWindow.Show();
                                _mainWindow.WindowState = WindowState.Normal;
                                _mainWindow.Activate();
                                _mainWindow.PromptAddUrl(url);
                            });
                    },
                    readClipboard: () => _mainWindow.Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null));
                _clipboardMonitor = clip;
                _mainWindow.Opened += (_, _) => clip.Attach(_mainWindow);

                desktop.MainWindow = _mainWindow;

                _tray = new TrayService(
                    showMainWindow: () =>
                    {
                        _mainWindow.Show();
                        _mainWindow.WindowState = WindowState.Normal;
                        _mainWindow.Activate();
                    },
                    exit: () =>
                    {
                        _exiting = true;
                        DownloadManager.Instance.Shutdown();
                        _tray?.Dispose();
                        _clipboardMonitor?.Dispose();
                        desktop.Shutdown();
                    });
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
