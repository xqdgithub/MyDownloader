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

        public void Pause(string id) => _manager.Pause(id);
        public void Resume(string id) => _manager.Resume(id);
        public void Cancel(string id) => _manager.Cancel(id);
        public void Redownload(string id) => _manager.Redownload(id);
        public void Remove(string id) => _manager.Remove(id);
        public TaskSnapshot? GetSnapshot(string id) => _manager.GetSnapshot(id);
    }
}
