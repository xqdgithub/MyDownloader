using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using MyDownloader.Core;

namespace MyDownloader.App.ViewModels
{
    /// <summary>主窗口 ViewModel：500ms 拉快照增量刷新。</summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly DownloadManager _manager = DownloadManager.Instance;
        private readonly Dictionary<string, TaskItemViewModel> _map = new();
        private readonly DispatcherTimer _timer;

        public ObservableCollection<TaskItemViewModel> Items { get; } = new();

        [ObservableProperty] private string _totalSpeedText = "--";
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _completedCount;
        [ObservableProperty] private TaskItemViewModel? _selectedItem;

        public MainViewModel()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();
            Refresh();
        }

        public void Refresh()
        {
            var snapshots = _manager.GetSnapshots();

            foreach (var snap in snapshots)
            {
                if (!_map.TryGetValue(snap.Id, out var vm))
                {
                    vm = new TaskItemViewModel(snap.Id);
                    _map[snap.Id] = vm;
                    Items.Add(vm);
                }
                vm.Apply(snap);
            }

            var ids = snapshots.Select(s => s.Id).ToHashSet();
            foreach (var gone in _map.Keys.Where(k => !ids.Contains(k)).ToList())
            {
                Items.Remove(_map[gone]);
                _map.Remove(gone);
            }

            var active = snapshots.Where(s => s.IsActive).ToList();
            ActiveCount = active.Count;
            CompletedCount = snapshots.Count(s => s.Status == DownloadStatus.Completed);
            TotalSpeedText = PathTools.FormatSpeed(active.Sum(s => s.SpeedBytesPerSecond));
        }

        [RelayCommand] private void PauseAll() => _manager.PauseAll();
        [RelayCommand] private void ResumeAll() => _manager.ResumeAll();

        /// <summary>由 View 注入：打开添加对话框，返回 (url, path) 或 null。</summary>
        public Func<string?, Task<(string url, string path)?>>? RequestAddTask;

        /// <summary>由 View 注入：打开详情窗口。</summary>
        public Action<string>? RequestShowDetail;

        [RelayCommand]
        private async Task AddTaskAsync()
        {
            var result = await (RequestAddTask?.Invoke(null) ?? Task.FromResult<(string, string)?>(null));
            if (result is { } r) _manager.AddTask(r.url, r.path);
        }

        [RelayCommand]
        private void ViewDetail(TaskItemViewModel? item)
        {
            if (item != null) RequestShowDetail?.Invoke(item.Id);
        }

        [RelayCommand]
        private void PauseItem(TaskItemViewModel? item) { if (item != null) _manager.Pause(item.Id); }

        [RelayCommand]
        private void ResumeItem(TaskItemViewModel? item) { if (item != null) _manager.Resume(item.Id); }

        [RelayCommand]
        private void CancelItem(TaskItemViewModel? item) { if (item != null) _manager.Cancel(item.Id); }

        [RelayCommand]
        private void RedownloadItem(TaskItemViewModel? item) { if (item != null) _manager.Redownload(item.Id); }

        [RelayCommand]
        private void RemoveItem(TaskItemViewModel? item)
        {
            if (item == null) return;
            _manager.Remove(item.Id);
            _map.Remove(item.Id);
            Items.Remove(item);
        }

        [RelayCommand]
        private void OpenItem(TaskItemViewModel? item)
        {
            if (item == null) return;
            var snap = _manager.GetSnapshot(item.Id);
            if (snap != null && System.IO.File.Exists(snap.FilePath))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{snap.FilePath}\"");
        }

        public void Pause(string id) => _manager.Pause(id);
        public void Resume(string id) => _manager.Resume(id);
        public void Cancel(string id) => _manager.Cancel(id);
        public void Redownload(string id) => _manager.Redownload(id);
        public void Remove(string id) => _manager.Remove(id);
        public TaskSnapshot? GetSnapshot(string id) => _manager.GetSnapshot(id);
    }
}
