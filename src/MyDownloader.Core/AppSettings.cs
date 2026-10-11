namespace MyDownloader.Core
{
    /// <summary>
    /// 应用设置（持久化于设置库）。
    /// </summary>
    public class AppSettings
    {
        /// <summary>默认下载目录。</summary>
        public string? DefaultDownloadDir { get; set; }

        /// <summary>是否启用剪贴板监听。</summary>
        public bool ClipboardMonitorEnabled { get; set; } = true;

        /// <summary>是否已提示过"最小化到托盘"。</summary>
        public bool TrayTipShown { get; set; }

        /// <summary>是否跟随系统代理。</summary>
        public bool UseSystemProxy { get; set; } = true;

        /// <summary>是否使用深色主题。</summary>
        public bool DarkTheme { get; set; }

        /// <summary>取默认下载目录（系统"下载"文件夹）。</summary>
        public string GetDefaultDir()
        {
            if (!string.IsNullOrWhiteSpace(DefaultDownloadDir) && Directory.Exists(DefaultDownloadDir))
                return DefaultDownloadDir!;
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            return Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
