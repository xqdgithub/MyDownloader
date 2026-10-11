# MyDownloader 优化设计（下载体验聚焦）

日期：2026-10-11
状态：待审阅

## 1. 目标与范围

个人下载器，核心目标是**下载体验**，不做大而全。本次优化只解决四个真实痛点，并保持轻量。

**要解决**
1. 不能后台常驻 —— 关掉窗口下载就中断。
2. 进度反馈差 —— 看不到实时进度、速度、剩余时间。
3. 启动下载麻烦 —— 复制链接后还要手动粘贴、选路径。
4. 重启丢任务 —— 未完成任务不能续传。

**明确不做（YAGNI）**
设置窗口、搜索/筛选/排序、浏览器扩展、独立下载进程、桌面悬浮窗、开机自启、分块数/限速可配 UI、手动代理。

## 2. 现状

- 单进程 WinForms（net8.0-windows），UI 用 AntdUI。
- 下载核心为本地维护的 `Downloader` 库（多分块并行）。
- 持久化用 `QDX.Service` 的 SQLite（JSON 存储）。
- 三个窗体：`MainForm`（任务列表）、`DownloadTaskInfoForm`（单任务下载）、`DownloadTaskSelectForm`（选 URL/路径）。

**现有缺陷**
- `MainForm.cs:78-89` 删除条件 `x.ID == data.Id || string.IsNullOrEmpty(x.ID)` 错乱。
- `DownloadTaskInfoForm.cs:77-91` 更新分支 `info = downloadTaskInfo` 未写回列表。
- `SqLiteDataProvider.Set`：DB 已有行但缓存未命中时会插入重复行（`SqLiteDataProvider.cs:107-117`）。
- `DownloadService` 创建于窗口内，窗口关闭即中断下载。
- 默认保存目录写死"我的文档"；DB 与路径使用相对 `./`。

## 3. 总体架构

```
Program.Main
  └─ Application.Run(new TrayAppContext())
       ├─ DownloadManager (单例，下载引擎)
       ├─ NotifyIcon (托盘)
       └─ MainForm (主窗口，可反复开关)
```

- `DownloadManager`：与 UI 无关的单例下载引擎。
- `TrayAppContext`：`ApplicationContext` 子类，承载托盘与主窗口生命周期。
- `MainForm`：任务列表 UI，只读快照 + 下发控制命令。

**关键决策：UI 用"拉"而非"推"。**
下载库进度事件频率极高。Manager 只更新内存快照，不向窗口逐事件 `BeginInvoke`；`MainForm` 用约 500ms 的 `Timer` 调 `GetSnapshots()` 增量刷新。窗口开关、托盘、多入口都简单，且无事件风暴。

**数据访问唯一入口**
- 持久化统一由 Manager 持有 `SqLiteDataProvider`，其它窗体不再各自 `new`（消除多 EF 上下文并发写同一 SQLite 的锁冲突）。
- **线程安全**：`SQLiteDataContext`（EF `DbContext`）非线程安全。下载完成回调在线程池线程写库、UI 定时器在 UI 线程读快照，所有 provider 读写必须**经 Manager 内部单一锁串行化**（或专用单线程队列封送），禁止多线程直接并发访问同一上下文。
- **两个数据库**：任务库 `DownloadMainDB` 与设置库 `DownloadSettingDB`（`ConstData.cs:11-12`）各用**独立 provider 实例**，不共用。
- `SqLiteDataProvider` 的静态缓存键需纳入数据库标识（当前 `KeyType` 仅含 `key`+`type`，`SqLiteDataProvider.cs:177-191`），避免不同库互相污染；`Get` 返回的 List 需深拷贝或视为只读，避免调用方原地修改污染缓存。
- DB 路径由相对 `./`（`ConstData.cs:11-12`）改为绝对路径（如 `%LocalAppData%\MyDownloader\`），避免托盘/快捷方式启动时工作目录不确定。

**事件推送边界**
- `TaskUpdated` 仅在**状态变化**或**进度节流后**（约 2 秒）触发，绝不逐进度事件触发；`TaskCompleted` 在终态触发。
- 托盘 Tooltip 与 `MainForm` **共用 `TrayAppContext` 持有的同一个 500ms 定时器**（不是各自一个），统一读 `GetSnapshots()`，不走事件推送。

## 4. 下载引擎：DownloadManager

**职责**：添加任务、启动/暂停/继续/取消/删除、并发调度、事件聚合、状态持久化、统一构建下载配置。

**结构**
- 单例，持有 `ConcurrentDictionary<string, TaskHandle>`，每个 `TaskHandle` 包一个 `DownloadService`。
- 并发上限常量（默认 3），超出排队；某任务结束即调度队列中的下一个。
- 统一在此构建 `DownloadConfiguration`（`ChunkCount`、系统代理等），保证配置一致。

**对外接口**
- `AddTask(string url, string path)`
- `Pause(id)` / `Resume(id)` / `Cancel(id)` / `Remove(id)`
- `IReadOnlyList<TaskSnapshot> GetSnapshots()`
- 事件：`TaskUpdated` / `TaskCompleted`

**数据模型**
- `TaskHandle`：运行时句柄（`DownloadService`、`CancellationTokenSource` 等）。
- `TaskSnapshot`：线程安全的只读快照（`Id`、文件名、大小、进度%、速度、ETA、状态、错误信息），供 UI/托盘读取。

**配置要点**
- `ChunkCount = 8`、`ParallelDownload = true`、`MaxTryAgainOnFailover = 5`。
- 确认 `CheckDiskSizeBeforeDownload`（库默认已为 true，`DownloadConfiguration.cs:17`）。
- 代理：`var p = WebRequest.GetSystemWebProxy(); p.Credentials = CredentialCache.DefaultCredentials; RequestConfiguration.Proxy = p;`。
  - **注意**：代理认证凭据必须设在 `IWebProxy.Credentials` 上；`RequestConfiguration.Credentials` 映射到 `HttpWebRequest.Credentials`（**服务器**认证，`Request.cs:93-96`），不得用于代理认证，也不得向任意服务器发送默认凭据。

## 5. 托盘常驻：TrayAppContext

- `NotifyIcon` + 内嵌图标资源。
- Tooltip 动态更新：`总进度 45% · 3 个任务 · ↓ 2.3MB/s`；空闲显示 `MyDownloader · 空闲`。
- 右键菜单（最小集）：显示主窗口、暂停全部 / 继续全部（动态启用）、退出。
- 双击托盘 → 显示/激活主窗口。
- 主窗口点 X → 隐藏到托盘（**仅 Hide，不 Dispose**，保证剪贴板监听的窗口句柄与消息循环存活）；首次弹一次气泡"已最小化到托盘，下载继续在后台进行"（"是否已提示过"记为设置项，存于设置 DB）。
- 退出仅从托盘菜单触发，完整退出序列：停所有下载并等待 → 持久化落盘 → `NotifyIcon.Visible = false` → 释放 NotifyIcon → 调用 `ExitThreadCore()` 结束 `ApplicationContext`。
- 系统关机：处理 `SystemEvents.SessionEnding` / `FormClosing`，取消退出并走同一退出序列，避免关机中断导致丢数据。
- 完成气泡：`xxx 下载完成`，`BalloonTipClicked` → 打开所在文件夹；失败气泡：`xxx 下载失败`。
- 不做开机自启。

**托盘刷新**：与 `MainForm` 共用同一 500ms 定时器（由 `TrayAppContext` 持有），读 `GetSnapshots()` 更新 Tooltip，不走事件推送。

## 6. 主界面进度体验：MainForm

**表格列**

| 列 | 内容 |
|---|---|
| 文件名 | 现有 |
| 大小 | 现有（总大小） |
| 进度 | 进度条 + 百分比（新增） |
| 速度 | 实时速度，如 `2.3MB/s`（新增） |
| 剩余 | ETA，如 `00:01:23`（新增） |
| 状态 | 现有（Badge） |
| 操作 | 查看 / 删除，增加"暂停 / 继续"（按状态切换） |

**刷新**：由 `TrayAppContext` 持有的唯一 500ms `Timer` 调 `GetSnapshots()`，同时驱动托盘 Tooltip 与 `MainForm` 表格；`MainForm` 按 `Id` 增量更新对应行的进度/速度/剩余/状态；不整表重建（避免闪烁、丢失选中/滚动）。删除旧的 `RefreshMainTable` 整表刷新。
- AntdUI 无 `RefreshRow` API，Table 依赖行对象的 `INotifyPropertyChanged` 触发单元格刷新。因此 `TableItem` 的属性 setter 必须调用 `OnPropertyChanged`，且进度/状态等被替换的单元格对象需重建后赋回属性，才能实现增量更新。

**行操作联动**：运行中显示"暂停"；已暂停显示"继续"；已完成显示"打开文件夹"。删除运行中任务需先取消。

**ETA**：`剩余字节 / 当前速度`；速度为 0 或未知显示 `--`。

**详情窗口（DownloadTaskInfoForm）简化**：不再自建 `DownloadService`，改为只读展示某任务分块进度 + 控制按钮（暂停/继续/取消），数据来自 Manager；保留逐块 `Progress` 控件可视化 8 个分块。

**不加**：搜索/筛选/排序。

## 7. 启动下载：剪贴板 + 拖拽

**剪贴板监听**
- `AddClipboardFormatListener`（Win32）+ 重写 `WndProc` 处理 `WM_CLIPBOARDUPDATE`。
- 宿主窗口：因主窗口点 X 仅隐藏（不 Dispose），可将监听寄生于 `MainForm`；若主窗口可能被真正关闭，则在 `TrayAppContext` 用 `NativeWindow` 创建独立消息窗口承载，保证无主窗口时监听仍有效。
- 内容为 http/https URL 且与上次不同 → 触发。
- 主窗口可见：弹添加框预填 URL 与推导文件名；窗口隐藏/托盘：仅在托盘气泡提示，不主动弹窗。
- 去重：记录上次处理内容。
- 托盘菜单提供"剪贴板监听"开关（默认开）。

**拖拽添加**
- `MainForm.AllowDrop = true`，处理 `DragEnter` / `DragDrop`。
- 支持的 `DataFormat`：`DataFormats.UniformResourceLocator`（浏览器拖链接）、`DataFormats.Text`、`DataFormats.FileDrop`（`.txt`，每行一个链接）。
- 拖入 `.txt` 时读取每行链接批量添加。
- 不做种子文件等。

**添加对话框**
- 复用 `DownloadTaskSelectForm` 作为统一入口，接收初始 URL。
- 默认目录从 Manager 取（系统"下载"文件夹），不再写死"我的文档"。

**边界**：非 URL 文本忽略；剪贴板异常静默处理。

## 8. 断点续传与持久化

**修复现有缺陷**
- `MainForm.cs` 删除按精确 `Id`。
- `AddOrUpdateTaskInfoAsync` 更新分支写回列表。
- `SqLiteDataProvider.Set`：真正缺陷是"DB 已有行但缓存未命中时会插入重复行"（未命中分支 `SqLiteDataProvider.cs:107-117` 直接 `Add`），修复为统一"存在则更新、不存在则插入"。

**持久化内容（字段白名单，必须排除 Storage）**
- **禁止直接序列化整个 `DownloadPackage`**：其公有属性 `Storage`（`DownloadPackage.cs:71`）为 `ConcurrentStream`，`Path` setter 会立即打开 `FileStream`（`ConcurrentStream.cs:24-34`），构造时还会启动后台 Watcher 任务（`ConcurrentStream.cs:158-170`）；内存流的 `Data` getter 会把整文件读入 JSON（`ConcurrentStream.cs:39-61`）。直接序列化会导致**启动时对每个任务打开文件句柄 + 拉起后台线程 + 句柄泄漏**。
- 采用**持久化 DTO**（首选），仅持久化白名单字段：
  - 任务层：`ID`、`Url`、`FilePath`、`StartTime`、`EndTime`、`Status`。
  - package 层：`TotalFileSize`、`FileName`、`Urls`、`IsSupportDownloadInRange`，以及 `Chunks` 的 `Start` / `End` / `Position` / `MaxTryAgainOnFailover` / `Timeout`。
- **Chunk 重试参数必须一并持久化或回填**：续传走 `ChunkHub.SetFileChunks` 的 `package.Validate()` 复用分支（`ChunkHub.cs:42-45`），该分支**不会**回填重试参数；若反序列化的 `Chunk.MaxTryAgainOnFailover` 为默认 0（`Chunk.cs:33`），则 `CanTryAgainOnFailover()` 恒为 false（`Chunk.cs:90`），续传时瞬时错误重试被禁用、`Timeout` 退回 1000ms。因此 DTO 须保留这两个字段，或在恢复时按当前配置回填。
- 备选：给 `Storage`/`ReceivedBytesSize`/`InMemoryStream` 加 `[JsonIgnore]` 并直接序列化 `Chunk[]`——此路径会自动保留 `Chunk` 全字段（含重试参数），保真度更高但需改动本地维护的库。二者择一，推荐 DTO。
- 反序列化后**不得**创建任何文件句柄或后台任务；`Storage` 一律为 null，续传时由 `BuildStorage` 重新创建。
- 不持久化实时速度（库中速度来自 `Bandwidth`，不在 package 上）；进度显示可由 `Chunks` 的 `Position` 推导，如保留 `Progress` 亦仅作展示缓存。

**续传原理**
- `DownloadFileTaskAsync(package, urls)` 接受已有 package；库在 `ChunkHub.SetFileChunks` 检测到 `Chunks != null` 即 `package.Validate()` 复用已有位置（`ChunkHub.cs:42-45`），未完成分块从 `Position` 继续。
- 续传必须走 `DownloadFileTaskAsync(package, urls)` 重载，**不得**走 `DownloadFileTaskAsync(fileName)`（后者会 `File.Delete` 已存在文件，`AbstractDownloadService.cs:363-381`）。
- 续传前校验：若服务端 `TotalFileSize` 与持久化值不一致，或本地文件缺失，则判定不可续传并重下（避免错位拼接）。
  - 注意：`DownloadService.StartDownload` 会先用服务端值覆盖 `Package.TotalFileSize`（`DownloadService.cs:37`），故该比对必须在调用 `DownloadFileTaskAsync(package, urls)` **之前**自行发起 HEAD/`GetFileSize` 完成，不能依赖库内自动比对。

**恢复时机**
- 启动时加载任务列表，对未完成状态（`Created`/`None`/`Running`/`Stopped`/`Paused`）标记为"已暂停"，不自动开跑。
  - `None`/`Created` 且 `Chunks` 为 null 的任务本就无法续传（`DownloadPackage` 默认 `Chunks` 为 null），显示为"未开始"，点击从头下载。

**持久化时机**
- 状态变化（开始/暂停/完成/失败）写库。
- 进度写入节流（每约 2 秒或每变化 5%）。
- 退出前强制落盘。

## 9. 边界与错误处理

- 所有下载异常由 Manager 捕获 → 任务 `Status = Failed` + `ErrorMessage`，不冒泡崩溃。
- 网络错误由库 `MaxTryAgainOnFailover=5` 重试，Manager 只处理最终失败。
- 磁盘空间不足：确认 `CheckDiskSizeBeforeDownload`（库默认已 true）。
- 持久化异常：不阻断下载，忽略/记日志。
- 跨线程：UI 更新统一由 `MainForm` 的 Timer 在 UI 线程读快照，避免 `BeginInvoke` 风暴与竞态。
- 同名文件：自动加序号 `name(1).ext`，并**同步更新** DB 的 `FilePath` 与 package 的 `FileName`；并发同名生成需加锁避免竞态。
- 无法推导文件名：默认 `download_<时间戳>`。
- 应用退出：停所有下载 → 持久化 → 释放 NotifyIcon → 退出。

## 10. 分期实施

- **P1 引擎与托盘**：`DownloadManager` + `TrayAppContext` + 关窗到托盘 + 主表进度/速度/ETA 列（拉模式刷新）。
- **P2 启动与续传**：剪贴板监听 + 拖拽 + 系统代理 + 持久化修复 + 断点续传 + 完成/失败气泡。
- **P3 收尾**：详情窗口简化、同名处理、错误信息展示、删除/暂停交互完善。

## 11. 验证方式

以手动验证为主：
- 添加大文件任务，关主窗口 → 确认下载继续。
- 重启程序 → 确认任务在列表且可继续。
- 测试剪贴板监听与拖拽添加。
- 断网 → 确认失败气泡与状态。
