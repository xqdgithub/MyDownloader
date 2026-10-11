using Downloader;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 单个下载任务的运行时句柄（仅 DownloadManager 内部使用）。
    /// </summary>
    internal class TaskHandle
    {
        public string Id { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DownloadService Service { get; set; } = null!;
        public DownloadTaskInfo Info { get; set; } = null!;

        // 实时进度（由库事件更新）
        public long ReceivedBytes;
        public long TotalBytes;
        public double SpeedBytesPerSecond;
        public string? ErrorMessage;

        /// <summary>是否已入队等待调度。</summary>
        public bool IsQueued;
    }
}
