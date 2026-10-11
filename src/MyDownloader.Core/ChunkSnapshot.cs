namespace MyDownloader.Core
{
    /// <summary>
    /// 单个下载分块的只读快照。
    /// </summary>
    public class ChunkSnapshot
    {
        public int Index { get; init; }
        public long Start { get; init; }
        public long End { get; init; }
        public long Position { get; init; }

        public long Length => End - Start + 1;

        /// <summary>分块进度百分比（0-100）。</summary>
        public double Progress => Length <= 0 ? 0 : Math.Min(100, Position * 100.0 / Length);

        /// <summary>分块状态：完成 / 下载中 / 等待。</summary>
        public string State
        {
            get
            {
                if (Length <= 0) return "完成";
                if (Position >= Length) return "完成";
                if (Position > 0) return "下载中";
                return "等待";
            }
        }
    }
}
