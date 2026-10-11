namespace MyDownloader.Classes
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
        /// 根据剩余字节与速度计算 ETA；速度为 0 或负时返回 "--"。
        /// </summary>
        public static string FormatEta(long remainingBytes, double bytesPerSecond)
        {
            if (bytesPerSecond <= 0 || remainingBytes <= 0) return "--";
            var seconds = (long)(remainingBytes / bytesPerSecond);
            if (seconds > 359999) return "--";
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
    }
}
