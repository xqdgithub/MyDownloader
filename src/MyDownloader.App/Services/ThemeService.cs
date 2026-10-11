using Avalonia;
using Avalonia.Styling;
using MyDownloader.Core;

namespace MyDownloader.App.Services
{
    /// <summary>浅/深色主题切换，持久化到设置。</summary>
    public static class ThemeService
    {
        public static void Apply(AppSettings settings)
        {
            if (Application.Current == null) return;
            Application.Current.RequestedThemeVariant =
                settings.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public static void Toggle()
        {
            var s = DownloadManager.Instance.Settings;
            s.DarkTheme = !s.DarkTheme;
            DownloadManager.Instance.SaveSettings();
            Apply(s);
        }
    }
}
