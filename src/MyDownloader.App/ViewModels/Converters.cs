using Avalonia.Data.Converters;
using Avalonia.Media;
using Downloader;

namespace MyDownloader.App.ViewModels
{
    /// <summary>常用值转换器。</summary>
    public static class Converters
    {
        /// <summary>分块状态文本 → 状态色。</summary>
        public static readonly IValueConverter ChunkStateToBrush =
            new FuncValueConverter<string, IBrush>(s => new SolidColorBrush(s switch
            {
                "完成" => Color.Parse("#52C41A"),
                "下载中" => Color.Parse("#1677FF"),
                "已暂停" => Color.Parse("#FAAD14"),
                "已取消" => Color.Parse("#8C8C8C"),
                "已失败" => Color.Parse("#FF4D4F"),
                _ => Color.Parse("#8C8C8C")
            }));
    }

    /// <summary>状态色统一来源（进度条与状态标签共用）。</summary>
    public static class StatusColors
    {
        /// <summary>任务状态 → 画刷。</summary>
        public static IBrush Brush(DownloadStatus status) => new SolidColorBrush(status switch
        {
            DownloadStatus.Completed => Color.Parse("#52C41A"),
            DownloadStatus.Failed => Color.Parse("#FF4D4F"),
            DownloadStatus.Paused => Color.Parse("#FAAD14"),
            DownloadStatus.Stopped => Color.Parse("#8C8C8C"),
            DownloadStatus.Running => Color.Parse("#1677FF"),
            _ => Color.Parse("#8C8C8C")
        });
    }
}
