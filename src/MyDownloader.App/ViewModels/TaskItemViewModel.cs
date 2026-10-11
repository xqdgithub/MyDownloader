using CommunityToolkit.Mvvm.ComponentModel;
using Downloader;
using MyDownloader.Core;

namespace MyDownloader.App.ViewModels
{
    /// <summary>列表行 ViewModel，按 Id 增量更新。</summary>
    public partial class TaskItemViewModel : ObservableObject
    {
        public string Id { get; }

        public TaskItemViewModel(string id) => Id = id;

        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _sizeText = string.Empty;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private string _speedText = string.Empty;
        [ObservableProperty] private string _etaText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private DownloadStatus _status;

        public bool IsRunning => Status == DownloadStatus.Running;
        public bool IsPaused => Status == DownloadStatus.Paused;
        public bool IsStopped => Status == DownloadStatus.Stopped;
        public bool IsCompleted => Status == DownloadStatus.Completed;
        public bool CanPause => IsRunning;
        public bool CanResume => IsPaused || IsStopped;
        public bool CanOpen => IsCompleted;

        public void Apply(TaskSnapshot snap)
        {
            FileName = snap.FileName;
            var size = DownloadTools.FitByteUnit(snap.TotalBytes, out string unit);
            SizeText = $"{size:0.00}{unit}";
            Progress = snap.Progress;
            SpeedText = snap.SpeedText;
            EtaText = snap.Eta;
            Status = snap.Status;
            StatusText = snap.Status switch
            {
                DownloadStatus.Completed => "已完成",
                DownloadStatus.Failed => "失败",
                DownloadStatus.Paused => "已暂停",
                DownloadStatus.Stopped => "已取消",
                DownloadStatus.Running => "下载中",
                _ => "未开始"
            };
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsStopped));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanResume));
            OnPropertyChanged(nameof(CanOpen));
        }
    }
}
