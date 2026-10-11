using Avalonia.Data.Converters;
using Avalonia.Media;

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
}
