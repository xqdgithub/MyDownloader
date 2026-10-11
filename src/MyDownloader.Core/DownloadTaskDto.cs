using Downloader;

namespace MyDownloader.Core
{
    /// <summary>
    /// 分块持久化数据（仅白名单字段，避免序列化 ConcurrentStream）。
    /// </summary>
    public class ChunkDto
    {
        public long Start { get; set; }
        public long End { get; set; }
        public long Position { get; set; }
        public int MaxTryAgainOnFailover { get; set; }
        public int Timeout { get; set; }
    }

    /// <summary>
    /// 下载任务的持久化 DTO。禁止直接序列化 DownloadPackage（其 Storage 会打开文件句柄）。
    /// </summary>
    public class DownloadTaskDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? FilePath { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DownloadStatus Status { get; set; }
        public string? ErrorMessage { get; set; }

        // package 白名单
        public long TotalFileSize { get; set; }
        public string? FileName { get; set; }
        public string[]? Urls { get; set; }
        public bool IsSupportDownloadInRange { get; set; }
        public ChunkDto[]? Chunks { get; set; }

        public static DownloadTaskDto FromTaskInfo(DownloadTaskInfo info)
        {
            var package = info.DownloadPackage;
            return new DownloadTaskDto
            {
                Id = info.ID,
                Url = info.Url,
                FilePath = info.FilePath,
                StartTime = info.StartTime,
                EndTime = info.EndTime,
                Status = package?.Status ?? DownloadStatus.None,
                TotalFileSize = package?.TotalFileSize ?? 0,
                FileName = package?.FileName ?? info.FilePath,
                Urls = package?.Urls,
                IsSupportDownloadInRange = package?.IsSupportDownloadInRange ?? true,
                Chunks = package?.Chunks == null
                    ? null
                    : package.Chunks.Select(c => new ChunkDto
                    {
                        Start = c.Start,
                        End = c.End,
                        Position = c.Position,
                        MaxTryAgainOnFailover = c.MaxTryAgainOnFailover,
                        Timeout = c.Timeout
                    }).ToArray()
            };
        }

        /// <summary>
        /// 还原为 DownloadPackage。不创建任何文件句柄或后台任务，Storage 保持为 null。
        /// </summary>
        public DownloadPackage ToPackage()
        {
            var package = new DownloadPackage
            {
                Status = Status,
                TotalFileSize = TotalFileSize,
                FileName = FileName,
                Urls = Urls,
                IsSupportDownloadInRange = IsSupportDownloadInRange
            };
            if (Chunks != null)
            {
                package.Chunks = Chunks.Select(c => new Chunk(c.Start, c.End)
                {
                    Position = c.Position,
                    MaxTryAgainOnFailover = c.MaxTryAgainOnFailover,
                    Timeout = c.Timeout
                }).ToArray();
            }
            return package;
        }
    }
}
