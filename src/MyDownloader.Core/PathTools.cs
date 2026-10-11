namespace MyDownloader.Core
{
    /// <summary>
    /// 路径、文件名与显示格式化工具。
    /// </summary>
    public static class PathTools
    {
        /// <summary>
        /// 从 URL 推导文件名；去查询串；无法推导时返回空串。
        /// </summary>
        public static string ExtractFileName(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            var withoutQuery = url.Split('?')[0].Split('#')[0];
            var last = withoutQuery.Split('/').LastOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(last)) return string.Empty;
            return Uri.UnescapeDataString(last);
        }

        /// <summary>
        /// 无法推导文件名时，生成默认文件名。
        /// </summary>
        public static string BuildFileName(string url, DateTime now)
        {
            var name = ExtractFileName(url);
            return string.IsNullOrEmpty(name) ? $"download_{now:yyyyMMddHHmmss}" : name;
        }

        /// <summary>
        /// 常见可下载文件扩展名（小写，含点）。
        /// </summary>
        private static readonly HashSet<string> DownloadExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".img",
            ".exe", ".msi", ".apk", ".dmg", ".deb", ".rpm", ".appx", ".msix",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".epub", ".mobi",
            ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a",
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".ts", ".m4v",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".tif", ".tiff",
            ".bin", ".dat", ".jar", ".war", ".pkg", ".torrent"
        };

        /// <summary>
        /// 判断链接是否"看起来像"可下载的文件（http/https 且路径末段带常见文件扩展名）。
        /// 用于剪贴板监听，避免对普通网页地址提示。
        /// </summary>
        public static bool LooksLikeDownloadUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            var name = ExtractFileName(url);
            if (string.IsNullOrEmpty(name)) return false;

            var ext = Path.GetExtension(name);
            return !string.IsNullOrEmpty(ext) && DownloadExtensions.Contains(ext);
        }

        /// <summary>
        /// 若目标路径已存在，追加 (1)、(2)… 序号。
        /// </summary>
        public static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (int i = 1; ; i++)
            {
                var candidate = Path.Combine(dir, $"{name}({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
        }

        /// <summary>
        /// 格式化为可读速度；速度为 0 或负时返回 "--"。
        /// </summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "--";
            double v = bytesPerSecond;
            string unit;
            if (v >= 1024 * 1024 * 1024) { v /= 1024 * 1024 * 1024; unit = "GB"; }
            else if (v >= 1024 * 1024) { v /= 1024 * 1024; unit = "MB"; }
            else if (v >= 1024) { v /= 1024; unit = "KB"; }
            else { unit = "B"; }
            return $"{v.ToString("#0.00")}{unit}/s";
        }

        /// <summary>
        /// 格式化为可读体积（自动单位）；0 返回 "0 B"。
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            double v = bytes;
            string unit;
            if (v >= 1024 * 1024 * 1024) { v /= 1024 * 1024 * 1024; unit = "GB"; }
            else if (v >= 1024 * 1024) { v /= 1024 * 1024; unit = "MB"; }
            else if (v >= 1024) { v /= 1024; unit = "KB"; }
            else { unit = "B"; }
            return $"{v.ToString("#0.00")} {unit}";
        }

        /// <summary>
        /// 格式化为"已下载 / 总量"的汇总文本。
        /// </summary>
        public static string FormatOverview(long receivedBytes, long totalBytes)
        {
            return $"{FormatSize(receivedBytes)} / {FormatSize(totalBytes)}";
        }

        /// <summary>
        /// 根据剩余字节与速度计算 ETA；速度为 0 或负时返回 "--"。
        /// </summary>
        public static string FormatEta(long remainingBytes, double bytesPerSecond)
        {
            if (bytesPerSecond <= 0 || remainingBytes <= 0) return "--";
            var seconds = (long)(remainingBytes / bytesPerSecond);
            if (seconds <= 0 || seconds > 359999) return "--";
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
    }
}
