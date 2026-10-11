using Downloader;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 任务只读快照，供 UI 与托盘读取（线程安全：每次刷新重建）。
    /// </summary>
    public class TaskSnapshot
    {
        public string Id { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string? Url { get; init; }
        public long TotalBytes { get; init; }
        public long ReceivedBytes { get; init; }
        public double SpeedBytesPerSecond { get; init; }
        public DownloadStatus Status { get; init; }
        public string? ErrorMessage { get; init; }

        /// <summary>进度百分比（0-100）。</summary>
        public double Progress => TotalBytes <= 0 ? 0 : Math.Min(100, ReceivedBytes * 100.0 / TotalBytes);

        /// <summary>剩余时间显示。</summary>
        public string Eta => PathTools.FormatEta(Math.Max(0, TotalBytes - ReceivedBytes), SpeedBytesPerSecond);

        /// <summary>速度显示。</summary>
        public string SpeedText => PathTools.FormatSpeed(SpeedBytesPerSecond);

        public bool IsActive => Status == DownloadStatus.Running || Status == DownloadStatus.Paused || Status == DownloadStatus.Created;
    }
}
