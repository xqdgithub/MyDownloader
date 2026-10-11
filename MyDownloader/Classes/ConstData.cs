namespace MyDownloader.Classes
{
    public static class ConstData
    {
        /// <summary>数据目录（%LocalAppData%\MyDownloader）。</summary>
        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyDownloader");

        public static string DownloadMainDB => Path.Combine(DataDir, "DownloadMainDB.db");
        public static string DownloadSettingDB => Path.Combine(DataDir, "DownloadSettingDB.db");

        public const string DownloadTaskInfoList = "DownloadTaskInfoList";
        public const string AppSettings = "AppSettings";

        /// <summary>并发下载上限。</summary>
        public const int MaxConcurrentDownloads = 3;
    }
}
