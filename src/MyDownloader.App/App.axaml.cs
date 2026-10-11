using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
                        desktop.Shutdown();
                    });
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
