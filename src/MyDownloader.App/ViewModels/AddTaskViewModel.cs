using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDownloader.Core;

namespace MyDownloader.App.ViewModels
{
    /// <summary>添加任务对话框 ViewModel。</summary>
    public partial class AddTaskViewModel : ObservableObject
    {
        [ObservableProperty] private string _url = string.Empty;
        [ObservableProperty] private string _filePath = string.Empty;
        [ObservableProperty] private string? _error;

        public bool Confirmed { get; private set; }

        public AddTaskViewModel() { }

        public AddTaskViewModel(string url)
        {
            Url = url;
            UpdatePath();
        }

        partial void OnUrlChanged(string value) => UpdatePath();

        private void UpdatePath()
        {
            if (string.IsNullOrWhiteSpace(Url)) return;
            var dir = DownloadManager.Instance.Settings.GetDefaultDir();
            var file = PathTools.BuildFileName(Url, DateTime.Now);
            FilePath = Path.Combine(dir, file);
        }

        [RelayCommand]
        private void Confirm()
        {
            Error = null;
            if (!IsHttpUrl(Url)) { Error = "请输入合法的 http/https 地址"; return; }
            if (string.IsNullOrWhiteSpace(FilePath)) { Error = "保存路径不能为空"; return; }
            Confirmed = true;
            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);

        public Action<bool>? RequestClose;

        private static bool IsHttpUrl(string s)
            => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
