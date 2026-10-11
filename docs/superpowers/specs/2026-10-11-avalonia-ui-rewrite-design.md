# MyDownloader UI 重写为 Avalonia 设计

日期：2026-10-11
状态：待审阅（已按审查修订）

## 1. 目标与范围

在既有"下载体验优化"基础上，将 UI 从 WinForms + AntdUI **全量重写为 Avalonia（.NET 10）**，同时修复两处回归并改善界面：

- **修复子进度丢失**：原详情窗口的逐分块进度在重写时被删，引擎也未把分块数据放进快照。本次恢复分块进度。
- **补上"取消"操作**：引擎已有 `Cancel(id)` 但未接到 UI。
- **重新设计界面与交互**：现代浅色 + 深色可切换。

**明确不做（YAGNI）**：设置窗口、搜索/筛选/排序、浏览器扩展、独立下载进程、桌面悬浮窗、开机自启、跨平台发布（仅 Windows 运行）。

## 2. 现状与问题

- 引擎类（`Classes/` 下 10 个）**无任何 UI 依赖**，可整体迁移复用。
- 详情窗口重写时删除了分块进度控件与 `ChunkDownloadProgressChanged` 处理。
- `DownloadManager.Cancel(id)` 已实现，但主列表行操作与详情窗口均未接线。
- 旧项目 `MyDownloader`（WinForms + AntdUI 1.6.2），`net8.0-windows`。
- 测试项目 `tests/MyDownloader.Tests` 为 `net8.0-windows` + `UseWindowsForms=true`，`ProjectReference` 指向旧 WinForms 项目——迁移必须同步处理。

## 3. 目标结构

```
MyDownloader.sln
├─ src/MyDownloader.Core/      # 类库 net10.0（引擎，无 UI 依赖）
│    └─ 迁移 Classes/ 下 10 个类（命名空间改为 MyDownloader.Core）
├─ src/MyDownloader.App/       # Avalonia 桌面应用 net10.0
│    ├─ App.axaml / Program.cs
│    ├─ Views/    MainWindow / TaskDetailWindow / AddTaskWindow
│    ├─ ViewModels/
│    └─ Services/  TrayService / ThemeService / ClipboardMonitor / NotificationService
└─ tests/MyDownloader.Tests/   # net10.0，引用 Core（移除 UseWindowsForms）
```

**迁移动作**
- 10 个引擎类迁入 `MyDownloader.Core`，命名空间 `MyDownloader.Classes` → `MyDownloader.Core`（同步改所有 `using`）。
- `MyDownloader.Core` 目标 `net10.0`。
- **`Downloader` 与 `QDX.Service` 保持 `net8.0` 不升级**（`net10.0` 可直接引用 `net8.0` 库；`QDX.Service` 属独立仓库且有他人未提交改动，升级会污染）。
- 修正 sln 与 `ProjectReference` 相对路径（迁到 `src/` 后层级变化）。
- 删除旧 `MyDownloader/`（WinForms）项目与 AntdUI 依赖——**在 P2 达到功能对等后再删**（见 §9）。
- `ConstData.DataDir` 保持 `%LocalAppData%\MyDownloader`（数据兼容；存储为无 `$type` 的 JSON，仅属性名决定兼容）。

## 4. 技术选型

- Avalonia 12.x：`Avalonia`、`Avalonia.Desktop`、`Avalonia.Themes.Fluent`、`Avalonia.Controls.DataGrid`（均支持 `net10.0`）。
- MVVM：`CommunityToolkit.Mvvm`（`[ObservableProperty]`/`[RelayCommand]`）。
- 目标框架：`net10.0`。

## 5. 应用骨架

- `Program.cs`：`BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`。
- `App.axaml`：`FluentTheme`；`RequestedThemeVariant` 绑定主题服务；`DataTemplates` 映射 ViewModel→View。
- 生命周期：`ShutdownMode = OnExplicitShutdown`（关窗不退出，托盘常驻）。
- **拉模式**：`DispatcherTimer`(500ms) 调 `GetSnapshots()`，按 Id 增量更新 `ObservableCollection`（不重建），避免闪烁。
- 引擎事件经 `Dispatcher.UIThread.Post` 回 UI 线程。

## 6. 服务

- **ThemeService**：切换 `Application.Current.RequestedThemeVariant`（Light/Dark），持久化到 `AppSettings`（追加字段，JSON 兼容）。
- **TrayService**：Avalonia 内置 `TrayIcon` + `NativeMenu`。
  - 菜单项 `IsEnabled` 由命令 `CanExecute` 驱动（暂停全部/继续全部按状态动态启用）。
  - **交互为单击显示主窗口**（Avalonia `TrayIcon` 仅有 `Clicked`，无双击事件）。
  - Tooltip 显示总进度（Windows 限制 63 字符，超长截断）。
  - 关窗拦截为隐藏 + 首次提示。
- **NotificationService**：Avalonia `TrayIcon` **无气泡 API**，改用**自绘轻量 Toast 窗口**（无边框、屏幕右下角、自动淡出），用于完成/失败通知与"检测到链接"提示。不引入额外 Toast 依赖。
- **ClipboardMonitor**：Windows 专属，通过 `Win32Properties.AddWndProcHook(TopLevel, WndProc)` 挂消息钩子，用 `TopLevel.TryGetPlatformHandle().Handle` 取 HWND 后 `AddClipboardFormatListener`；处理 `WM_CLIPBOARDUPDATE`，去重后回调。窗口隐藏时用 `NotificationService` 提示。

## 7. 窗口 UI 与交互

### 主窗口（单窗口列表 + 状态栏）
- 顶部工具栏：`+ 添加任务`、`暂停全部`/`继续全部`、主题切换。
- 中部 `DataGrid`：复选框 / 文件名 / 大小 / 进度（内联 `ProgressBar`）/ 速度 / 剩余 / 状态（彩色徽标）/ 操作（按状态动态）。
- 底部状态栏：总速度、活动任务数、完成数。
- 双击行 → 详情窗口；多选批量删除/暂停；拖拽 URL 与 `.txt` 添加。

### 详情窗口（分块进度）
- 顶部：文件名（大）、URL（可复制）、保存路径（可打开文件夹）。
- 中部：总进度条 + 速度/已下载/总量/ETA/状态。
- 下方 `ItemsControl`：**按实际分块数动态渲染**（`ChunkSnapshots.Count`，非固定 8——服务器不支持 range 或文件过小时库会缩减为 1 块）。每条显示块序号、百分比、状态色（下载中/完成/等待）。
- 按钮：暂停 / 继续 / 取消 / 重新下载。

### 添加任务对话框
- URL 输入（自动推导文件名）、保存路径（默认目录 + 浏览）、确定/取消。
- 校验 URL 合法、路径非空。

### 操作语义（状态机）
- `暂停`：库 `Pause()`，状态 `Paused`，可 `Resume()`。
- `继续`：仅对 `Paused` 有效，库 `Resume()`。
- `取消`：库 `CancelAsync()`，状态 `Stopped`，**保留任务记录**。
- `重新下载`：**覆盖式重下**——清空分块与目标文件后从头下载（避免 `AddTask` 的 `EnsureUniquePath` 产生 `(1)` 副本）；实现为引擎新增 `Redownload(id)`（或明确用 `Remove`+内部覆盖式 `AddTask`）。
- `删除`：停止并移除记录。
- **重启语义**：`LoadPersistedTasks` 目前把 `Running/Paused/Stopped/Created` 一律映射为 `Paused`。修订为：`Stopped`（取消）**保持 `Stopped`**，不参与 `ResumeAll` 自动恢复；`Running/Paused/Created` 映射为 `Paused`（可继续）。

## 8. 引擎改动

- `TaskSnapshot` 增加 `ChunkSnapshots`：每块 `Index`/`Start`/`End`/`Position`/`Progress`/`State`（State 由 `Position/Length` 推导：完成/下载中/等待）。
- **分块进度采集：直接读 `package.Chunks`，不订阅高频事件。** `DownloadManager` 已把 `h.Info.DownloadPackage` 指向库的 `Package`（`StartHandle`），分块 `Position` 由库在共享 `Chunk` 对象上实时更新，故 `BuildSnapshot` 读取 `package.Chunks` 即可得到实时分块位置，天然与 500ms 拉模式一致，无事件风暴。
- `LoadPersistedTasks` 按 §7 修订 Stopped 映射。
- 新增 `Redownload(id)`（覆盖式）。
- `Cancel(id)` 接线到 UI（主列表与详情窗口）。

## 9. 分期实施（快速实现优先）

- **P1 迁移（不删旧项目）**：建 `MyDownloader.Core`，搬 10 个引擎类并改命名空间/引用；测试项目改 `net10.0`、去 `UseWindowsForms`、指向 Core；修正 sln 路径。此阶段旧 WinForms 项目仍保留，保证有可用应用。
- **P2 Avalonia 骨架**：App/窗口/托盘/主题/通知/剪贴板 + 主窗口列表跑通。
- **P3 详情与添加对话框** + 引擎分块快照 + 取消/重新下载接线。
- **P4 收尾**：拖拽、状态栏、视觉打磨；**功能对等后**删除旧 WinForms 项目与 AntdUI 依赖。

## 10. 验证

- `dotnet build` 0 错误；`dotnet test` 全绿。
- 手动：关窗到托盘下载继续；详情窗口按实际块数显示分块进度；暂停/继续/取消/重新下载/删除语义正确；浅深色切换；剪贴板/拖拽添加；完成/失败 Toast 通知。
