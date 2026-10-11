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

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                DownloadManager.Instance.LoadPersistedTasks();
                ThemeService.Apply(DownloadManager.Instance.Settings);

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
                        if (_mainWindow.IsVisible)
                        {
                            _mainWindow.PromptAddUrl(url);
                        }
                        else
                        {
                            NotificationService.Show("检测到下载链接", url);
                        }
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
