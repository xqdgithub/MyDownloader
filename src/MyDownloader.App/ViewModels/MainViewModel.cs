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
        [RelayCommand] private async Task ResumeAll() => await _manager.ResumeAllAsync();

        /// <summary>新增任务并立即刷新列表。</summary>
        public void AddTask(string url, string path)
        {
            _manager.AddTask(url, path);
            Refresh();
        }

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
        private async Task ResumeItem(TaskItemViewModel? item) { if (item != null) await _manager.ResumeAsync(item.Id); }

        [RelayCommand]
        private void CancelItem(TaskItemViewModel? item) { if (item != null) _manager.Cancel(item.Id); }

        [RelayCommand]
        private async Task RedownloadItem(TaskItemViewModel? item) { if (item != null) await _manager.RedownloadAsync(item.Id); }

        [RelayCommand]
        private async Task RemoveItem(TaskItemViewModel? item)
        {
            if (item == null) return;
            _map.Remove(item.Id);
            Items.Remove(item);
            await _manager.RemoveAsync(item.Id);
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
        public async Task Resume(string id) => await _manager.ResumeAsync(id);
        public void Cancel(string id) => _manager.Cancel(id);
        public async Task Redownload(string id) => await _manager.RedownloadAsync(id);
        public async Task Remove(string id) => await _manager.RemoveAsync(id);
        public TaskSnapshot? GetSnapshot(string id) => _manager.GetSnapshot(id);
    }
}
