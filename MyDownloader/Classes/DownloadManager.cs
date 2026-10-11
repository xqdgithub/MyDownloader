using Downloader;
using QDX.Serivices.StorageServices;
using QDX.Serivices.StorageServices.SQLiteDataProvider;
using System.Collections.Concurrent;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 与 UI 无关的单例下载引擎：统一调度、并发控制、事件聚合与持久化。
    /// </summary>
    public sealed class DownloadManager
    {
        private static readonly Lazy<DownloadManager> _instance = new(() => new DownloadManager());
        public static DownloadManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, TaskHandle> _tasks = new();
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly object _dbLock = new();

        private readonly IDataProvider _mainProvider;
        private readonly IDataProvider _settingProvider;
        private AppSettings _settings;

        /// <summary>任务状态变化或进度节流后触发。</summary>
        public event EventHandler<string>? TaskUpdated;

        /// <summary>任务到达终态（完成/失败）时触发。</summary>
        public event EventHandler<string>? TaskCompleted;

        public AppSettings Settings => _settings;

        private DownloadManager()
        {
            Directory.CreateDirectory(ConstData.DataDir);
            _mainProvider = new SqLiteDataProvider { SqliteDataBase = ConstData.DownloadMainDB };
            _settingProvider = new SqLiteDataProvider { SqliteDataBase = ConstData.DownloadSettingDB };
            _settings = _settingProvider.Get<AppSettings>(ConstData.AppSettings) ?? new AppSettings();
        }

        /// <summary>应用启动时加载任务列表；未完成任务标记为暂停。</summary>
        public void LoadPersistedTasks()
        {
            List<DownloadTaskDto> dtos;
            lock (_dbLock)
            {
                dtos = _mainProvider.Get<List<DownloadTaskDto>>(ConstData.DownloadTaskInfoList)
                       ?? new List<DownloadTaskDto>();
            }
            foreach (var dto in dtos)
            {
                var package = dto.ToPackage();
                if (package.Status is DownloadStatus.Running
                    or DownloadStatus.Paused or DownloadStatus.Stopped or DownloadStatus.Created)
                {
                    package.Status = DownloadStatus.Paused;
                }
                var info = new DownloadTaskInfo
                {
                    ID = dto.Id,
                    Url = dto.Url,
                    FilePath = dto.FilePath,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    DownloadPackage = package
                };
                _tasks[info.ID] = new TaskHandle
                {
                    Id = info.ID,
                    Url = info.Url ?? string.Empty,
                    FilePath = info.FilePath ?? string.Empty,
                    Info = info,
                    ReceivedBytes = package.Chunks?.Sum(c => c.Position) ?? 0,
                    TotalBytes = package.TotalFileSize
                };
            }
        }

        /// <summary>获取全部任务快照。</summary>
        public IReadOnlyList<TaskSnapshot> GetSnapshots()
        {
            return _tasks.Values.Select(BuildSnapshot).ToList();
        }

        public TaskSnapshot? GetSnapshot(string id)
            => _tasks.TryGetValue(id, out var h) ? BuildSnapshot(h) : null;

        private static TaskSnapshot BuildSnapshot(TaskHandle h)
        {
            var package = h.Info.DownloadPackage;
            return new TaskSnapshot
            {
                Id = h.Id,
                FileName = Path.GetFileName(h.FilePath),
                FilePath = h.FilePath,
                Url = h.Url,
                TotalBytes = h.TotalBytes > 0 ? h.TotalBytes : (package?.TotalFileSize ?? 0),
                ReceivedBytes = h.ReceivedBytes,
                SpeedBytesPerSecond = h.SpeedBytesPerSecond,
                Status = package?.Status ?? DownloadStatus.None,
                ErrorMessage = h.ErrorMessage
            };
        }

        /// <summary>新增任务（自动排重文件名）并加入调度队列。</summary>
        public string AddTask(string url, string filePath)
        {
            var uniquePath = PathTools.EnsureUniquePath(filePath);
            var info = new DownloadTaskInfo
            {
                ID = Guid.NewGuid().ToString(),
                Url = url,
                FilePath = uniquePath,
                StartTime = DateTime.Now,
                DownloadPackage = new DownloadPackage { Status = DownloadStatus.None, SaveProgress = 0 }
            };
            var handle = new TaskHandle
            {
                Id = info.ID,
                Url = url,
                FilePath = uniquePath,
                Info = info
            };
            _tasks[info.ID] = handle;
            Persist();
            EnqueueAndPump(info.ID);
            return info.ID;
        }

        private void EnqueueAndPump(string id)
        {
            if (_tasks.TryGetValue(id, out var h)) h.IsQueued = true;
            _queue.Enqueue(id);
            PumpQueue();
        }

        private void PumpQueue()
        {
            while (CountActive() < ConstData.MaxConcurrentDownloads && _queue.TryDequeue(out var id))
            {
                if (!_tasks.TryGetValue(id, out var h)) continue;
                h.IsQueued = false;
                StartHandle(h);
            }
        }

        private int CountActive()
            => _tasks.Values.Count(h => h.Info.DownloadPackage?.Status == DownloadStatus.Running);

        private void StartHandle(TaskHandle h)
        {
            h.Service = new DownloadService(DownloadConfigFactory.Create(_settings.UseSystemProxy));
            h.Service.DownloadProgressChanged += (_, e) => OnProgress(h, e);
            h.Service.ChunkDownloadProgressChanged += (_, e) => OnProgress(h, e);
            h.Service.DownloadFileCompleted += (_, e) => OnCompleted(h, e);

            h.Info.DownloadPackage = h.Service.Package;
            h.Info.DownloadPackage.Status = DownloadStatus.Running;

            _ = RunAsync(h);
        }

        private async Task RunAsync(TaskHandle h)
        {
            try
            {
                var package = h.Info.DownloadPackage!;
                // 续传：已有分块则走 package 重载（不会删除文件）
                if (package.Chunks is { Length: > 0 })
                {
                    package.FileName = h.FilePath;
                    package.Urls = new[] { h.Url };
                    await h.Service.DownloadFileTaskAsync(package, new[] { h.Url });
                }
                else
                {
                    await h.Service.DownloadFileTaskAsync(h.Url, h.FilePath);
                }
            }
            catch (Exception ex)
            {
                h.ErrorMessage = ex.Message;
                if (h.Info.DownloadPackage != null) h.Info.DownloadPackage.Status = DownloadStatus.Failed;
            }
            finally
            {
                Persist();
                TaskUpdated?.Invoke(this, h.Id);
                if (h.Info.DownloadPackage?.Status is DownloadStatus.Completed or DownloadStatus.Failed)
                    TaskCompleted?.Invoke(this, h.Id);
                PumpQueue();
            }
        }

        private void OnProgress(TaskHandle h, DownloadProgressChangedEventArgs e)
        {
            h.ReceivedBytes = e.ReceivedBytesSize;
            h.TotalBytes = e.TotalBytesToReceive;
            h.SpeedBytesPerSecond = e.AverageBytesPerSecondSpeed;
            ThrottledPersist();
            TaskUpdated?.Invoke(this, h.Id);
        }

        private void OnCompleted(TaskHandle h, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            TaskUpdated?.Invoke(this, h.Id);
        }

        // ---- 控制命令 ----

        public void Pause(string id)
        {
            if (_tasks.TryGetValue(id, out var h) && h.Service != null)
            {
                h.Service.Pause();
                if (h.Info.DownloadPackage != null) h.Info.DownloadPackage.Status = DownloadStatus.Paused;
                Persist();
                TaskUpdated?.Invoke(this, id);
            }
        }

        public void Resume(string id)
        {
            if (!_tasks.TryGetValue(id, out var h)) return;
            if (h.Service == null)
            {
                EnqueueAndPump(id);
                return;
            }
            if (h.Info.DownloadPackage?.Status == DownloadStatus.Paused)
            {
                h.Service.Resume();
                h.Info.DownloadPackage.Status = DownloadStatus.Running;
                Persist();
                TaskUpdated?.Invoke(this, id);
            }
        }

        public void Cancel(string id)
        {
            if (_tasks.TryGetValue(id, out var h) && h.Service != null)
            {
                h.Service.CancelAsync();
            }
        }

        public void Remove(string id)
        {
            if (_tasks.TryRemove(id, out var h))
            {
                h.Service?.CancelAsync();
                Persist();
            }
        }

        public void PauseAll()
        {
            foreach (var h in _tasks.Values.Where(x => x.Info.DownloadPackage?.Status == DownloadStatus.Running))
                Pause(h.Id);
        }

        public void ResumeAll()
        {
            foreach (var h in _tasks.Values.Where(x => x.Info.DownloadPackage?.Status == DownloadStatus.Paused))
                Resume(h.Id);
        }

        /// <summary>应用退出：停所有下载并强制落盘。</summary>
        public void Shutdown()
        {
            foreach (var h in _tasks.Values)
            {
                try { h.Service?.CancelAsync(); } catch { /* 忽略 */ }
            }
            Persist();
        }

        // ---- 持久化（经 _dbLock 串行化，DbContext 非线程安全）----

        private DateTime _lastPersist = DateTime.MinValue;

        private void ThrottledPersist()
        {
            var now = DateTime.Now;
            if ((now - _lastPersist).TotalSeconds < 2) return;
            _lastPersist = now;
            Persist();
        }

        public void Persist()
        {
            lock (_dbLock)
            {
                var list = _tasks.Values.Select(h => DownloadTaskDto.FromTaskInfo(h.Info)).ToList();
                _mainProvider.Set(ConstData.DownloadTaskInfoList, list);
            }
        }

        public void SaveSettings()
        {
            lock (_dbLock)
            {
                _settingProvider.Set(ConstData.AppSettings, _settings);
            }
        }
    }
}
