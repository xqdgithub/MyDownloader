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
- `SqLiteDataProvider.Set` 缓存未命中时的写入分支可能重复/未覆盖。
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
- 开启 `CheckDiskSizeBeforeDownload`。
- 代理：`RequestConfiguration.Proxy = WebRequest.GetSystemWebProxy()`，`Credentials = CredentialCache.DefaultCredentials`，跟随系统代理。

## 5. 托盘常驻：TrayAppContext

- `NotifyIcon` + 内嵌图标资源。
- Tooltip 动态更新：`总进度 45% · 3 个任务 · ↓ 2.3MB/s`；空闲显示 `MyDownloader · 空闲`。
- 右键菜单（最小集）：显示主窗口、暂停全部 / 继续全部（动态启用）、退出。
- 双击托盘 → 显示/激活主窗口。
- 主窗口点 X → 隐藏到托盘；首次弹一次气泡"已最小化到托盘，下载继续在后台进行"（配置项记一次）。
- 退出仅从托盘菜单触发：停所有下载 → 持久化 → 释放 NotifyIcon。
- 完成气泡：`xxx 下载完成`，点击打开所在文件夹；失败气泡：`xxx 下载失败`。
- 不做开机自启。

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

**刷新**：500ms `Timer` 调 `GetSnapshots()`，按 `Id` 增量更新对应行的进度/速度/剩余/状态；不整表重建（避免闪烁、丢失选中/滚动）。删除旧的 `RefreshMainTable` 整表刷新。

**行操作联动**：运行中显示"暂停"；已暂停显示"继续"；已完成显示"打开文件夹"。删除运行中任务需先取消。

**ETA**：`剩余字节 / 当前速度`；速度为 0 或未知显示 `--`。

**详情窗口（DownloadTaskInfoForm）简化**：不再自建 `DownloadService`，改为只读展示某任务分块进度 + 控制按钮（暂停/继续/取消），数据来自 Manager；保留逐块 `Progress` 控件可视化 8 个分块。

**不加**：搜索/筛选/排序。

## 7. 启动下载：剪贴板 + 拖拽

**剪贴板监听**
- `AddClipboardFormatListener`（Win32）+ 重写 `WndProc` 处理 `WM_CLIPBOARDUPDATE`。
- 内容为 http/https URL 且与上次不同 → 触发。
- 主窗口可见：弹添加框预填 URL 与推导文件名；窗口隐藏/托盘：仅在托盘气泡提示，不主动弹窗。
- 去重：记录上次处理内容。
- 托盘菜单提供"剪贴板监听"开关（默认开）。

**拖拽添加**
- `MainForm.AllowDrop = true`，处理 `DragEnter` / `DragDrop`。
- 支持拖入 URL 文本；支持拖入 `.txt`（每行一个链接）批量添加。
- 不做种子文件等。

**添加对话框**
- 复用 `DownloadTaskSelectForm` 作为统一入口，接收初始 URL。
- 默认目录从 Manager 取（系统"下载"文件夹），不再写死"我的文档"。

**边界**：非 URL 文本忽略；剪贴板异常静默处理。

## 8. 断点续传与持久化

**修复现有缺陷**
- `MainForm.cs` 删除按精确 `Id`。
- `AddOrUpdateTaskInfoAsync` 更新分支写回列表。
- `SqLiteDataProvider.Set` 统一为"存在则更新、不存在则插入"。

**持久化内容**
- `DownloadTaskInfo` 增加运行时字段：`Progress`、`ReceivedBytes`、`Speed`（用于列表恢复显示）。
- 序列化 `DownloadPackage.Chunks`（含每块 `Start` / `End` / `Position`）、`TotalFileSize`、`FileName`、`Urls`、`Status`。

**续传原理**
- `DownloadFileTaskAsync(package, urls)` 接受已有 package；库在 `ChunkHub.SetFileChunks` 检测到 `Chunks != null` 即 `package.Validate()` 复用已有位置（`ChunkHub.cs:42-45`），未完成分块从 `Position` 继续。

**恢复时机**
- 启动时加载任务列表，对未完成状态（Running/Paused/Stopped/Created）标记为"已暂停"，不自动开跑。
- 用户点"继续" → 用持久化 package 恢复下载。

**持久化时机**
- 状态变化（开始/暂停/完成/失败）写库。
- 进度写入节流（每约 2 秒或每变化 5%）。
- 退出前强制落盘。

## 9. 边界与错误处理

- 所有下载异常由 Manager 捕获 → 任务 `Status = Failed` + `ErrorMessage`，不冒泡崩溃。
- 网络错误由库 `MaxTryAgainOnFailover=5` 重试，Manager 只处理最终失败。
- 磁盘空间不足：开启库的 `CheckDiskSizeBeforeDownload`。
- 持久化异常：不阻断下载，忽略/记日志。
- 跨线程：UI 更新统一由 `MainForm` 的 Timer 在 UI 线程读快照，避免 `BeginInvoke` 风暴与竞态。
- 同名文件：自动加序号 `name(1).ext`。
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
