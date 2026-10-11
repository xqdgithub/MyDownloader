using Downloader;
using QDX.Serivices.StorageServices;
using QDX.Serivices.StorageServices.SQLiteDataProvider;
using System.Collections.Concurrent;

namespace MyDownloader.Core
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

        /// <summary>串行化"检查活动数 + 出队 + 启动"，避免并发调用 PumpQueue 突破并发上限。</summary>
        private readonly object _scheduleLock = new();

        /// <summary>当前占用的活动槽位（显式计数，不依赖库状态，避免库重置状态导致计数失真）。</summary>
        private int _activeCount;

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
                // Running/Created 视为暂停；Stopped（取消）保持 Stopped，不参与 ResumeAll 自动恢复。
                if (package.Status is DownloadStatus.Running or DownloadStatus.Created)
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
            var status = package?.Status ?? DownloadStatus.None;
            var isRunning = status == DownloadStatus.Running;
            var chunks = package?.Chunks;
            var chunkSnapshots = chunks == null
                ? Array.Empty<ChunkSnapshot>()
                : chunks.Select((c, i) => new ChunkSnapshot
                {
                    Index = i,
                    Start = c.Start,
                    End = c.End,
                    Position = c.Position,
                    TaskStatus = status
                }).ToArray();

            return new TaskSnapshot
            {
                Id = h.Id,
                FileName = Path.GetFileName(h.FilePath),
                FilePath = h.FilePath,
                Url = h.Url,
                TotalBytes = h.TotalBytes > 0 ? h.TotalBytes : (package?.TotalFileSize ?? 0),
                ReceivedBytes = h.ReceivedBytes,
                // 非下载中时速度归零，避免暂停/取消后仍显示上一刻速度。
                SpeedBytesPerSecond = isRunning ? h.SpeedBytesPerSecond : 0,
                Status = status,
                ErrorMessage = h.ErrorMessage,
                ChunkSnapshots = chunkSnapshots
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
            // 检查与置位放入调度锁内，避免并发重复入队。
            lock (_scheduleLock)
            {
                if (!_tasks.TryGetValue(id, out var h)) return;
                // 重复入队防护：已在队列中则直接返回，避免重复创建 Service。
                if (h.IsQueued) return;
                h.IsQueued = true;
                _queue.Enqueue(id);
            }
            PumpQueue();
        }

        private void PumpQueue()
        {
            // 整体串行化：检查活动槽位、出队、启动必须原子，防止多线程并发突破上限。
            lock (_scheduleLock)
            {
                while (_activeCount < ConstData.MaxConcurrentDownloads && _queue.TryDequeue(out var id))
                {
                    if (!_tasks.TryGetValue(id, out var h)) continue;
                    h.IsQueued = false;
                    StartHandle(h);
                }
            }
        }

        private void StartHandle(TaskHandle h)
        {
            // 占用一个活动槽位（在 _scheduleLock 内调用，计数与判断保持原子）。
            Interlocked.Increment(ref _activeCount);

            h.Service = new DownloadService(DownloadConfigFactory.Create(_settings.UseSystemProxy));
            h.Service.DownloadProgressChanged += (_, e) => OnProgress(h, e);
            h.Service.DownloadFileCompleted += (_, e) => OnCompleted(h, e);

            if (h.Info.DownloadPackage?.Chunks is not { Length: > 0 })
            {
                h.Info.DownloadPackage = h.Service.Package;
            }
            h.Info.DownloadPackage!.Status = DownloadStatus.Running;

            _ = RunAsync(h);
        }

        private async Task RunAsync(TaskHandle h)
        {
            try
            {
                var package = h.Info.DownloadPackage!;
                if (package.Chunks is { Length: > 0 })
                {
                    // 续传前校验：服务端大小必须与持久化一致，否则重下
                    long remoteSize;
                    try
                    {
                        var probeConfig = DownloadConfigFactory.Create(_settings.UseSystemProxy);
                        remoteSize = await new Request(h.Url, probeConfig.RequestConfiguration).GetFileSize();
                    }
                    catch
                    {
                        remoteSize = -1;
                    }

                    // 续传校验：本地文件长度必须足以支撑已记录的分块位置，且服务端大小一致，否则重下。
                    var expectedLocal = package.Chunks?.Sum(c => c.Position) ?? 0;
                    var localExists = File.Exists(h.FilePath);
                    long localLength = localExists ? new FileInfo(h.FilePath).Length : 0;
                    if (!localExists
                        || (remoteSize > 0 && remoteSize != package.TotalFileSize)
                        || localLength < expectedLocal)
                    {
                        // 不可续传：清空分块，从头下载
                        package.Chunks = null;
                        h.ReceivedBytes = 0;
                    }
                }

                if (package.Chunks is { Length: > 0 })
                {
                    package.FileName = h.FilePath;
                    package.Urls = new[] { h.Url };
                    // 库会把 Service.Package 接管为该 package，二者此后为同一对象。
                    await h.Service.DownloadFileTaskAsync(package, new[] { h.Url });
                }
                else
                {
                    // 从头下载：使用 Service 的空包，并让 Info 指向同一对象，
                    // 避免进度/状态写在 Service.Package 而 Info 仍停在旧包导致状态卡死。
                    h.Info.DownloadPackage = h.Service.Package;
                    h.Info.DownloadPackage.Status = DownloadStatus.Running;
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
                // 释放活动槽位（与 StartHandle 的 Increment 配对）。
                Interlocked.Decrement(ref _activeCount);
                Persist();
                TaskUpdated?.Invoke(this, h.Id);
                if (h.Info.DownloadPackage?.Status is DownloadStatus.Completed or DownloadStatus.Failed)
                    TaskCompleted?.Invoke(this, h.Id);
                PumpQueue();
            }
        }

        private void OnProgress(TaskHandle h, DownloadProgressChangedEventArgs e)
        {
            // 进度数值每次都更新（供快照读取最新值）。
            h.ReceivedBytes = e.ReceivedBytesSize;
            h.TotalBytes = e.TotalBytesToReceive;
            h.SpeedBytesPerSecond = e.AverageBytesPerSecondSpeed;
            ThrottledPersist();

            // 通知节流：距上次通知不足 200ms 则跳过，避免事件风暴。
            lock (h.NotifyLock)
            {
                var now = DateTime.Now;
                if ((now - h.LastNotify).TotalMilliseconds < 200) return;
                h.LastNotify = now;
            }
            TaskUpdated?.Invoke(this, h.Id);
        }

        private void OnCompleted(TaskHandle h, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            // 取消时库也会带上 OperationCanceledException，此时 e.Cancelled=true，不算失败。
            if (e.Cancelled)
            {
                if (h.Info.DownloadPackage != null)
                    h.Info.DownloadPackage.Status = DownloadStatus.Stopped;
                TaskUpdated?.Invoke(this, h.Id);
                return;
            }

            // 库失败时不重抛异常，错误信息只能从 e.Error 读取。
            if (e.Error != null)
            {
                h.ErrorMessage = e.Error.Message;
                if (h.Info.DownloadPackage?.Status != DownloadStatus.Completed)
                    h.Info.DownloadPackage!.Status = DownloadStatus.Failed;
            }
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

        public async Task ResumeAsync(string id)
        {
            if (!_tasks.TryGetValue(id, out var h)) return;

            // Service 为空（尚未启动或已释放）：重新入队，基于持久化分块续传。
            if (h.Service == null)
            {
                EnqueueAndPump(id);
                return;
            }

            var status = h.Info.DownloadPackage?.Status;
            if (status == DownloadStatus.Paused)
            {
                h.Service.Resume();
                h.Info.DownloadPackage!.Status = DownloadStatus.Running;
                Persist();
                TaskUpdated?.Invoke(this, id);
            }
            else if (status is DownloadStatus.Stopped or DownloadStatus.Failed)
            {
                // 已取消或失败：异步释放旧 Service 后重新入队续传（不阻塞 UI）。
                var old = h.Service;
                h.Service = null!;
                if (status == DownloadStatus.Failed)
                {
                    h.ErrorMessage = null;
                    h.Info.DownloadPackage!.Status = DownloadStatus.None;
                }
                EnqueueAndPump(id);
                await DisposeServiceAsync(old);
            }
        }

        public void Cancel(string id)
        {
            if (_tasks.TryGetValue(id, out var h) && h.Service != null)
            {
                h.Service.CancelAsync();
            }
        }

        /// <summary>
        /// 异步释放 DownloadService。库的 Dispose() 会同步阻塞（内部 Clear().Wait()），
        /// 故使用非阻塞的 DisposeAsync()，避免卡住 UI 线程。
        /// </summary>
        private static async Task DisposeServiceAsync(DownloadService? service)
        {
            if (service == null) return;
            try { await service.DisposeAsync().ConfigureAwait(false); } catch { /* 忽略与运行中 RunAsync 的竞争 */ }
        }

        public async Task RemoveAsync(string id)
        {
            if (_tasks.TryRemove(id, out var h))
            {
                try { h.Service?.CancelAsync(); } catch { /* 忽略 */ }
                Persist();
                await DisposeServiceAsync(h.Service);
            }
        }

        /// <summary>覆盖式重新下载：清空分块与目标文件后从头下载，不生成 (1) 副本。</summary>
        public async Task RedownloadAsync(string id)
        {
            if (!_tasks.TryGetValue(id, out var h)) return;

            try { h.Service?.CancelAsync(); } catch { /* 忽略 */ }
            var old = h.Service;
            h.Service = null!;

            // 先等待旧 Service 完全停止，再删文件/重启，避免与仍在写入的下载竞争。
            await DisposeServiceAsync(old);

            // 删除已存在的目标文件，避免残留。
            try { if (File.Exists(h.FilePath)) File.Delete(h.FilePath); } catch { /* 忽略 */ }

            // 重置包与进度，从头开始。
            h.Info.DownloadPackage = new DownloadPackage { Status = DownloadStatus.None, SaveProgress = 0 };
            h.Info.StartTime = DateTime.Now;
            h.Info.EndTime = DateTime.MinValue;
            h.ReceivedBytes = 0;
            h.TotalBytes = 0;
            h.SpeedBytesPerSecond = 0;
            h.ErrorMessage = null;

            Persist();
            EnqueueAndPump(id);
            TaskUpdated?.Invoke(this, id);
        }

        public void PauseAll()
        {
            foreach (var h in _tasks.Values.Where(x => x.Info.DownloadPackage?.Status == DownloadStatus.Running))
                Pause(h.Id);
        }

        public async Task ResumeAllAsync()
        {
            foreach (var h in _tasks.Values.Where(x => x.Info.DownloadPackage?.Status == DownloadStatus.Paused))
                await ResumeAsync(h.Id);
        }

        /// <summary>应用退出：停所有下载并强制落盘。</summary>
        public void Shutdown()
        {
            foreach (var h in _tasks.Values)
            {
                try { h.Service?.CancelAsync(); } catch { /* 忽略 */ }
            }
            Persist();
            // 退出时异步释放，不阻塞调用线程；进程即将结束，无需等待。
            foreach (var h in _tasks.Values)
            {
                _ = DisposeServiceAsync(h.Service);
            }
        }

        // ---- 持久化（经 _dbLock 串行化，DbContext 非线程安全）----

        private DateTime _lastPersist = DateTime.MinValue;

        private void ThrottledPersist()
        {
            lock (_dbLock)
            {
                var now = DateTime.Now;
                if ((now - _lastPersist).TotalSeconds < 2) return;
                _lastPersist = now;
            }
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
