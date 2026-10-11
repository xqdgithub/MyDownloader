# MyDownloader UI 重写为 Avalonia 设计

日期：2026-10-11
状态：待审阅

## 1. 目标与范围

在既有"下载体验优化"基础上，将 UI 从 WinForms + AntdUI **全量重写为 Avalonia（.NET 10）**，同时修复两处回归并改善界面：

- **修复子进度丢失**：原详情窗口的逐分块进度在重写时被删；引擎层也曾移除 `ChunkDownloadProgressChanged` 订阅，导致分块数据未进快照。本次恢复分块进度。
- **补上"取消"操作**：引擎已有 `Cancel(id)` 但未接到 UI。
- **重新设计界面与交互**：现代浅色 + 深色可切换。

**明确不做（YAGNI）**：设置窗口、搜索/筛选/排序、浏览器扩展、独立下载进程、桌面悬浮窗、开机自启、跨平台发布（仅 Windows 运行）。

## 2. 现状与问题

- 引擎类（`Classes/` 下 10 个）**无任何 UI 依赖**，可整体迁移复用。
- 详情窗口重写时删除了分块进度控件与 `ChunkDownloadProgressChanged` 处理。
- `DownloadManager.Cancel(id)` 已实现，但主列表行操作与详情窗口均未接线。
- 旧项目 `MyDownloader`（WinForms + AntdUI 1.6.2），`net8.0-windows`。

## 3. 目标结构

```
MyDownloader.sln
├─ src/MyDownloader.Core/      # 类库 net10.0（引擎，无 UI 依赖）
│    └─ 迁移 Classes/ 下 10 个类；引用 Downloader + QDX.Service
├─ src/MyDownloader.App/       # Avalonia 桌面应用 net10.0
│    ├─ App.axaml / Program.cs
│    ├─ Views/    MainWindow / TaskDetailWindow / AddTaskWindow
│    ├─ ViewModels/
│    └─ Services/  TrayService / ThemeService / ClipboardMonitor
└─ tests/MyDownloader.Tests/   # 引用 Core
```

**迁移动作**
- 10 个引擎类迁入 `MyDownloader.Core`（零改动）。
- 命名空间由 `MyDownloader.Classes` 改为 `MyDownloader.Core`。
- `Downloader` 与 `QDX.Service` 升级/兼容 `net10.0`。
- 删除旧 `MyDownloader/`（WinForms）项目与 AntdUI 依赖。
- `ConstData.DataDir` 保持 `%LocalAppData%\MyDownloader`（数据兼容）。

## 4. 技术选型

- Avalonia 12.x：`Avalonia`、`Avalonia.Desktop`、`Avalonia.Themes.Fluent`、`Avalonia.Controls.DataGrid`。
- MVVM：`CommunityToolkit.Mvvm`（`[ObservableProperty]`/`[RelayCommand]`）。
- 目标框架：`net10.0`。

## 5. 应用骨架

- `Program.cs`：`BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`。
- `App.axaml`：`FluentTheme`；`RequestedThemeVariant` 绑定主题服务；`DataTemplates` 映射 ViewModel→View。
- 生命周期：`ShutdownMode = OnExplicitShutdown`（关窗不退出，托盘常驻）。
- **拉模式**：`DispatcherTimer`(500ms) 调 `GetSnapshots()`，按 Id 增量更新 `ObservableCollection`（不重建），避免闪烁。
- 引擎事件经 `Dispatcher.UIThread.Post` 回 UI 线程。

## 6. 服务

- **ThemeService**：切换 `RequestedThemeVariant`（Light/Dark），持久化到 `AppSettings`。
- **TrayService**：Avalonia 内置 `TrayIcon` + `NativeMenu`（显示主窗口/暂停全部/继续全部/退出）；双击显示主窗口；Tooltip 显示总进度；关窗拦截为隐藏 + 首次气泡提示。
- **ClipboardMonitor**：Windows 专属，Win32 `AddClipboardFormatListener` + 隐藏消息窗口处理 `WM_CLIPBOARDUPDATE`；去重后回调；窗口隐藏时用托盘气泡提示。

## 7. 窗口 UI 与交互

### 主窗口（单窗口列表 + 状态栏）
- 顶部工具栏：`+ 添加任务`、`暂停全部`/`继续全部`、主题切换。
- 中部 `DataGrid`：复选框 / 文件名 / 大小 / 进度（内联 `ProgressBar`）/ 速度 / 剩余 / 状态（彩色徽标）/ 操作（按状态动态：暂停·继续·取消·查看·打开·删除）。
- 底部状态栏：总速度、活动任务数、完成数。
- 双击行 → 详情窗口；多选批量删除/暂停；拖拽 URL 与 `.txt` 添加。

### 详情窗口（分块进度）
- 顶部：文件名（大）、URL（可复制）、保存路径（可打开文件夹）。
- 中部：总进度条 + 速度/已下载/总量/ETA/状态。
- 下方 `ItemsControl`：**8 条分块进度条**，显示块序号、百分比、状态色（下载中/完成/等待）。
- 按钮：暂停 / 继续 / 取消 / 重新下载。

### 添加任务对话框
- URL 输入（自动推导文件名）、保存路径（默认目录 + 浏览）、确定/取消。
- 校验 URL 合法、路径非空。

### 操作语义
- `取消`：停止下载但保留任务记录（状态 Stopped）。
- `删除`：停止并移除记录。

## 8. 引擎改动

- `TaskSnapshot` 增加 `ChunkSnapshots`（每块 `Start`/`End`/`Position`/`Progress`）。
- `DownloadManager` 重新订阅 `ChunkDownloadProgressChanged`，节流（每块 ≥200ms 或 ≥1%）写入 `TaskHandle` 分块进度；`BuildSnapshot` 填充 `ChunkSnapshots`。
- `Cancel(id)` 接线到 UI（主列表与详情窗口）。
- 复用现有 `DownloadTaskInfo`/`DownloadTaskDto` 的 `Chunks`，续传快照可反映分块位置。

## 9. 分期实施（快速实现优先）

- **P1 迁移**：建 `MyDownloader.Core`，搬 10 个引擎类，改引用，升 `net10.0`，删旧 WinForms，测试通过。
- **P2 Avalonia 骨架**：App/窗口/托盘/主题/剪贴板 + 主窗口列表跑通。
- **P3 详情与添加对话框** + 引擎分块快照 + 取消接线。
- **P4 收尾**：拖拽、状态栏、视觉打磨。

## 10. 验证

- `dotnet build` 0 错误；`dotnet test` 全绿。
- 手动：关窗到托盘下载继续；详情窗口显示 8 条分块进度；取消/删除语义正确；浅深色切换；剪贴板/拖拽添加。
