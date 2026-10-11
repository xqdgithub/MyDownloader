using Downloader;

namespace MyDownloader.Core
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

        /// <summary>分块区间缓存（库在完成后会清空 Chunks，用此保留分块展示）。</summary>
        public (long Start, long End)[]? CachedChunkRanges;

        /// <summary>上次触发 TaskUpdated 的时间（用于进度事件节流）。</summary>
        public DateTime LastNotify;

        /// <summary>保护 LastNotify 的专用锁。</summary>
        public readonly object NotifyLock = new();
    }
}
