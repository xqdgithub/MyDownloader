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
        [ObservableProperty] private string _progressText = "0%";
        [ObservableProperty] private string _speedText = string.Empty;
        [ObservableProperty] private string _etaText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private DownloadStatus _status;
        [ObservableProperty] private Avalonia.Media.IBrush _statusBrush = Avalonia.Media.Brushes.Gray;

        public bool IsRunning => Status == DownloadStatus.Running;
        public bool IsPaused => Status == DownloadStatus.Paused;
        public bool IsStopped => Status == DownloadStatus.Stopped;
        public bool IsCompleted => Status == DownloadStatus.Completed;
        public bool IsFailed => Status == DownloadStatus.Failed;

        /// <summary>是否处于活动状态（可暂停 / 取消）。</summary>
        public bool IsActive => Status == DownloadStatus.Running;

        /// <summary>暂停按钮是否可用（仅下载中）。</summary>
        public bool CanPause => IsRunning;

        /// <summary>继续按钮是否可用（暂停 / 取消 / 失败均可继续或重试）。</summary>
        public bool CanResume => Status is DownloadStatus.Paused or DownloadStatus.Stopped or DownloadStatus.Failed;

        /// <summary>取消按钮是否可用（下载中 / 已暂停）。</summary>
        public bool CanCancel => Status is DownloadStatus.Running or DownloadStatus.Paused;

        /// <summary>打开文件夹按钮是否可用（已完成）。</summary>
        public bool CanOpen => IsCompleted;

        /// <summary>重新下载是否可用（非下载中）。</summary>
        public bool CanRedownload => Status != DownloadStatus.Running;

        /// <summary>继续/重试按钮文案。</summary>
        public string ResumeText => Status == DownloadStatus.Failed ? "重试" : "继续";

        public void Apply(TaskSnapshot snap)
        {
            FileName = snap.FileName;
            SizeText = PathTools.FormatSize(snap.TotalBytes);
            Progress = snap.Progress;
            ProgressText = $"{snap.Progress:0}%";
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
            StatusBrush = StatusToBrush(snap.Status);
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsStopped));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanResume));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanOpen));
            OnPropertyChanged(nameof(CanRedownload));
            OnPropertyChanged(nameof(ResumeText));
        }

        private static Avalonia.Media.IBrush StatusToBrush(DownloadStatus status) => status switch
        {
            DownloadStatus.Completed => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#52C41A")),
            DownloadStatus.Failed => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FF4D4F")),
            DownloadStatus.Paused => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FAAD14")),
            DownloadStatus.Stopped => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8C8C8C")),
            DownloadStatus.Running => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1677FF")),
            _ => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8C8C8C"))
        };
    }
}
