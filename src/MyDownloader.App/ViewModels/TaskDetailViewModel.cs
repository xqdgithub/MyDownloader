using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using MyDownloader.Core;

namespace MyDownloader.App.ViewModels
{
    /// <summary>详情窗口 ViewModel：总进度 + 动态分块进度。</summary>
    public partial class TaskDetailViewModel : ObservableObject
    {
        private readonly DownloadManager _manager = DownloadManager.Instance;
        private readonly DispatcherTimer _timer;
        public string TaskId { get; }

        public ObservableCollection<ChunkSnapshot> Chunks { get; } = new();

        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _url = string.Empty;
        [ObservableProperty] private string _filePath = string.Empty;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private string _speedText = string.Empty;
        [ObservableProperty] private string _overviewText = string.Empty;
        [ObservableProperty] private string _etaText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;

        // 按钮可用状态
        [ObservableProperty] private bool _canPause;
        [ObservableProperty] private bool _canResume;
        [ObservableProperty] private bool _canCancel;
        [ObservableProperty] private bool _canRedownload;
        [ObservableProperty] private string _resumeText = "继续";

        public TaskDetailViewModel(string taskId)
        {
            TaskId = taskId;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();
            Refresh();
        }

        public void Refresh()
        {
            var snap = _manager.GetSnapshot(TaskId);
            if (snap == null) return;

            FileName = snap.FileName;
            Url = snap.Url ?? string.Empty;
            FilePath = snap.FilePath;
            Progress = snap.Progress;
            SpeedText = snap.SpeedText;
            EtaText = snap.Eta;
            OverviewText = $"{snap.ReceivedBytes}/{snap.TotalBytes}";
            StatusText = snap.Status switch
            {
                DownloadStatus.Completed => "已完成",
                DownloadStatus.Failed => "失败: " + snap.ErrorMessage,
                DownloadStatus.Paused => "已暂停",
                DownloadStatus.Stopped => "已取消",
                DownloadStatus.Running => "下载中",
                _ => "未开始"
            };

            if (Chunks.Count != snap.ChunkSnapshots.Count)
            {
                Chunks.Clear();
                foreach (var c in snap.ChunkSnapshots) Chunks.Add(c);
            }
            else
            {
                for (int i = 0; i < Chunks.Count; i++) Chunks[i] = snap.ChunkSnapshots[i];
            }

            // 按状态启用/禁用按钮
            CanPause = snap.Status == DownloadStatus.Running;
            CanResume = snap.Status is DownloadStatus.Paused or DownloadStatus.Stopped or DownloadStatus.Failed;
            CanCancel = snap.Status is DownloadStatus.Running or DownloadStatus.Paused;
            CanRedownload = snap.Status != DownloadStatus.Running;
            ResumeText = snap.Status == DownloadStatus.Failed ? "重试" : "继续";
        }

        [RelayCommand] private void Pause() => _manager.Pause(TaskId);
        [RelayCommand] private async Task Resume() => await _manager.ResumeAsync(TaskId);
        [RelayCommand] private void Cancel() => _manager.Cancel(TaskId);
        [RelayCommand] private async Task Redownload() => await _manager.RedownloadAsync(TaskId);

        public void Stop() => _timer.Stop();
    }
}
