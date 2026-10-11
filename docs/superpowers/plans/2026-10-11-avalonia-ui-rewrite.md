# MyDownloader Avalonia UI 重写 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 MyDownloader 的 UI 从 WinForms 全量重写为 Avalonia（.NET 10），恢复分块子进度、补上取消/重新下载，并重新设计界面与交互。

**Architecture:** 引擎类零 UI 依赖，迁入 `MyDownloader.Core` 类库（`net10.0`）；新建 Avalonia 应用 `MyDownloader.App`（`net10.0`，MVVM via CommunityToolkit.Mvvm）；下载引擎保持单例与"拉快照"模式，分块进度直接读 `package.Chunks`。

**Tech Stack:** .NET 10 / Avalonia 12.1.4 / Avalonia.Controls.DataGrid 12.1.2 / Avalonia.Themes.Fluent 12.1.4 / CommunityToolkit.Mvvm 8.4.2 / xUnit / 本地 Downloader(net8.0) + QDX.Service(net8.0)

**规格来源:** `docs/superpowers/specs/2026-10-11-avalonia-ui-rewrite-design.md`

**关键约定（务必遵守）:**
- 注释、文本一律使用中文。
- 不得提交（commit）除非步骤明确要求。
- `Downloader` 与 `QDX.Service` **保持 net8.0**，不升级。
- 分块进度**直接读 `package.Chunks`**，不订阅 `ChunkDownloadProgressChanged`。
- Avalonia `TrayIcon` **无气泡 API**，通知用自绘 Toast。
- 分块数**不固定为 8**，按 `ChunkSnapshots.Count` 动态渲染。
- P4 功能对等后才删旧 WinForms 项目。

---

## 文件结构总览

**新增 `src/MyDownloader.Core/`（类库 net10.0）**
- `MyDownloader.Core.csproj`
- 迁移 10 个引擎类（命名空间 `MyDownloader.Classes` → `MyDownloader.Core`）：
  `AppSettings.cs`、`ConstData.cs`、`DownloadConfigFactory.cs`、`DownloadManager.cs`、`DownloadTaskDto.cs`、`DownloadTaskInfo.cs`、`DownloadTools.cs`、`PathTools.cs`、`TaskHandle.cs`、`TaskSnapshot.cs`
- 新增 `ChunkSnapshot.cs`（分块快照模型）

**新增 `src/MyDownloader.App/`（Avalonia net10.0）**
- `MyDownloader.App.csproj`、`Program.cs`、`App.axaml`、`App.axaml.cs`
- `ViewModels/`：`MainViewModel.cs`、`TaskItemViewModel.cs`、`TaskDetailViewModel.cs`、`AddTaskViewModel.cs`
- `Views/`：`MainWindow.axaml(.cs)`、`TaskDetailWindow.axaml(.cs)`、`AddTaskWindow.axaml(.cs)`
- `Services/`：`TrayService.cs`、`ThemeService.cs`、`ClipboardMonitor.cs`、`NotificationService.cs`
- `Assets/`：图标资源

**修改 `tests/MyDownloader.Tests/`**
- `MyDownloader.Tests.csproj` 改 `net10.0`、去 `UseWindowsForms`、引用 Core
- 测试文件 `using MyDownloader.Classes;` → `using MyDownloader.Core;`

**修改 `MyDownloader.sln`**：加 Core/App，P4 删旧项目。

---

## Task 0: 创建 Core 类库并迁移引擎

**Files:**
- Create: `src/MyDownloader.Core/MyDownloader.Core.csproj`
- Create: `src/MyDownloader.Core/ChunkSnapshot.cs`
- Move: `MyDownloader/Classes/*.cs` → `src/MyDownloader.Core/`（10 个文件，改命名空间）
- Modify: `MyDownloader.sln`

- [ ] **Step 1: 创建 Core 项目文件**

创建 `src/MyDownloader.Core/MyDownloader.Core.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Downloader\src\Downloader\Downloader.csproj" />
    <ProjectReference Include="..\..\..\qdx\QDX.Service\QDX.Service.csproj" />
  </ItemGroup>

</Project>
```

> 路径说明：`src/MyDownloader.Core/` → 仓库根 `MyDownloader/` → 上一级 `CSharp/` 下的 `Downloader` 与 `qdx`，即 `..\..\..\`。

- [ ] **Step 2: 复制 10 个引擎类到 Core 并改命名空间**

将 `MyDownloader/Classes/` 下 10 个文件（`AppSettings.cs`、`ConstData.cs`、`DownloadConfigFactory.cs`、`DownloadManager.cs`、`DownloadTaskDto.cs`、`DownloadTaskInfo.cs`、`DownloadTools.cs`、`PathTools.cs`、`TaskHandle.cs`、`TaskSnapshot.cs`）复制到 `src/MyDownloader.Core/`。

在每个文件中把 `namespace MyDownloader.Classes` 改为 `namespace MyDownloader.Core`（同时把类内对同命名空间类型的引用无需改；若有 `using MyDownloader.Classes;` 则删除，因为同命名空间）。

用命令复制：
```powershell
Copy-Item "MyDownloader\Classes\*.cs" "src\MyDownloader.Core\" -Force
```
然后逐个把 `namespace MyDownloader.Classes` 替换为 `namespace MyDownloader.Core`。

- [ ] **Step 3: 新增 ChunkSnapshot 模型**

创建 `src/MyDownloader.Core/ChunkSnapshot.cs`：

```csharp
namespace MyDownloader.Core
{
    /// <summary>
    /// 单个下载分块的只读快照。
    /// </summary>
    public class ChunkSnapshot
    {
        public int Index { get; init; }
        public long Start { get; init; }
        public long End { get; init; }
        public long Position { get; init; }

        public long Length => End - Start + 1;

        /// <summary>分块进度百分比（0-100）。</summary>
        public double Progress => Length <= 0 ? 0 : Math.Min(100, Position * 100.0 / Length);

        /// <summary>分块状态：完成 / 下载中 / 等待。</summary>
        public string State
        {
            get
            {
                if (Length <= 0) return "完成";
                if (Position >= Length) return "完成";
                if (Position > 0) return "下载中";
                return "等待";
            }
        }
    }
}
```

- [ ] **Step 4: 加入解决方案**

```powershell
dotnet sln MyDownloader.sln add src/MyDownloader.Core/MyDownloader.Core.csproj
```

- [ ] **Step 5: 编译 Core**

Run: `dotnet build src/MyDownloader.Core/MyDownloader.Core.csproj`
Expected: `Build succeeded`，0 Error。

- [ ] **Step 6: Commit**

```bash
git add src/MyDownloader.Core MyDownloader.sln
git commit -m "refactor: 新建 MyDownloader.Core 类库并迁入引擎类（net10.0）"
```

---

## Task 1: 测试项目改指 Core

**Files:**
- Modify: `tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
- Modify: 4 个测试文件（改 using）

- [ ] **Step 1: 改测试项目文件**

将 `tests/MyDownloader.Tests/MyDownloader.Tests.csproj` 替换为：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MyDownloader.Core\MyDownloader.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 改测试文件命名空间引用**

将 4 个测试文件中的 `using MyDownloader.Classes;` 改为 `using MyDownloader.Core;`：
- `DownloadConfigFactoryTests.cs`
- `DownloadManagerTests.cs`
- `DownloadTaskDtoTests.cs`
- `PathToolsTests.cs`

- [ ] **Step 3: 运行测试**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 19`

- [ ] **Step 4: Commit**

```bash
git add tests/MyDownloader.Tests
git commit -m "test: 测试项目改指 MyDownloader.Core 并升级 net10.0"
```

---

## Task 2: 引擎改动（分块快照 + 状态机 + 重新下载）

**Files:**
- Modify: `src/MyDownloader.Core/TaskSnapshot.cs`
- Modify: `src/MyDownloader.Core/DownloadManager.cs`

- [ ] **Step 1: TaskSnapshot 增加 ChunkSnapshots**

在 `src/MyDownloader.Core/TaskSnapshot.cs` 中，类内增加属性：

```csharp
        /// <summary>分块进度快照（按 Index 排序）。</summary>
        public IReadOnlyList<ChunkSnapshot> ChunkSnapshots { get; init; } = Array.Empty<ChunkSnapshot>();
```

- [ ] **Step 2: BuildSnapshot 填充 ChunkSnapshots**

在 `DownloadManager.cs` 的 `BuildSnapshot` 中，读取 `package.Chunks` 填充分块快照。将 `BuildSnapshot` 替换为：

```csharp
        private static TaskSnapshot BuildSnapshot(TaskHandle h)
        {
            var package = h.Info.DownloadPackage;
            var chunks = package?.Chunks;
            var chunkSnapshots = chunks == null
                ? Array.Empty<ChunkSnapshot>()
                : chunks.Select((c, i) => new ChunkSnapshot
                {
                    Index = i,
                    Start = c.Start,
                    End = c.End,
                    Position = c.Position
                }).ToArray();

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
                ErrorMessage = h.ErrorMessage,
                ChunkSnapshots = chunkSnapshots
            };
        }
```

- [ ] **Step 3: 修订 LoadPersistedTasks 的 Stopped 语义**

将 `LoadPersistedTasks` 中的状态映射由"一律 Paused"改为"Stopped 保持 Stopped"。将：

```csharp
                if (package.Status is DownloadStatus.Running
                    or DownloadStatus.Paused or DownloadStatus.Stopped or DownloadStatus.Created)
                {
                    package.Status = DownloadStatus.Paused;
                }
```

改为：

```csharp
                // Running/Created 视为暂停；Stopped（取消）保持 Stopped，不参与 ResumeAll 自动恢复。
                if (package.Status is DownloadStatus.Running or DownloadStatus.Created)
                {
                    package.Status = DownloadStatus.Paused;
                }
```

- [ ] **Step 4: Resume 支持 Stopped 重新入队**

将 `Resume` 方法替换为：

```csharp
        public void Resume(string id)
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
            else if (status == DownloadStatus.Stopped)
            {
                // 已取消：释放旧 Service 后重新入队续传。
                try { h.Service.Dispose(); } catch { /* 忽略 */ }
                h.Service = null!;
                EnqueueAndPump(id);
            }
        }
```

- [ ] **Step 5: 新增 Redownload（覆盖式重下）**

在 `DownloadManager.cs` 的 `Remove` 方法之后新增：

```csharp
        /// <summary>覆盖式重新下载：清空分块与目标文件后从头下载，不生成 (1) 副本。</summary>
        public void Redownload(string id)
        {
            if (!_tasks.TryGetValue(id, out var h)) return;

            try { h.Service?.CancelAsync(); } catch { /* 忽略 */ }
            try { h.Service?.Dispose(); } catch { /* 忽略 */ }
            h.Service = null!;

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
```

- [ ] **Step 6: 编译**

Run: `dotnet build src/MyDownloader.Core/MyDownloader.Core.csproj`
Expected: `Build succeeded`。

- [ ] **Step 7: Commit**

```bash
git add src/MyDownloader.Core
git commit -m "feat: 快照带分块进度、Stopped 可续传、新增覆盖式重新下载"
```

---

## Task 3: 搭建 Avalonia 应用骨架

**Files:**
- Create: `src/MyDownloader.App/MyDownloader.App.csproj`
- Create: `src/MyDownloader.App/Program.cs`
- Create: `src/MyDownloader.App/App.axaml`
- Create: `src/MyDownloader.App/App.axaml.cs`
- Modify: `MyDownloader.sln`

- [ ] **Step 1: 创建 App 项目文件**

创建 `src/MyDownloader.App/MyDownloader.App.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.4" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.4" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.4" />
    <PackageReference Include="Avalonia.Controls.DataGrid" Version="12.1.2" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyDownloader.Core\MyDownloader.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建 app.manifest**

创建 `src/MyDownloader.App/app.manifest`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="MyDownloader.App"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">permonitorv2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 3: 创建 Program.cs**

创建 `src/MyDownloader.App/Program.cs`：

```csharp
using Avalonia;

namespace MyDownloader.App
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
            => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
```

- [ ] **Step 4: 创建 App.axaml**

创建 `src/MyDownloader.App/App.axaml`：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyDownloader.App.App"
             RequestedThemeVariant="Light">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml" />
  </Application.Styles>
</Application>
```

- [ ] **Step 5: 创建 App.axaml.cs（骨架，先启动空窗口）**

创建 `src/MyDownloader.App/App.axaml.cs`：

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyDownloader.App.Views;

namespace MyDownloader.App
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow = new MainWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
```

- [ ] **Step 6: 创建占位 MainWindow**

创建 `src/MyDownloader.App/Views/MainWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="MyDownloader.App.Views.MainWindow"
        Title="MyDownloader" Width="900" Height="560">
  <TextBlock Text="MyDownloader" HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="24"/>
</Window>
```

创建 `src/MyDownloader.App/Views/MainWindow.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace MyDownloader.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();
    }
}
```

- [ ] **Step 7: 加入解决方案并编译运行**

```powershell
dotnet sln MyDownloader.sln add src/MyDownloader.App/MyDownloader.App.csproj
dotnet build src/MyDownloader.App/MyDownloader.App.csproj
```
Expected: `Build succeeded`。

- [ ] **Step 8: Commit**

```bash
git add src/MyDownloader.App MyDownloader.sln
git commit -m "feat: 新建 Avalonia 应用骨架（App/Program/MainWindow 占位）"
```

---

## Task 4: 主窗口 ViewModel 与列表 UI

**Files:**
- Create: `src/MyDownloader.App/ViewModels/TaskItemViewModel.cs`
- Create: `src/MyDownloader.App/ViewModels/MainViewModel.cs`
- Modify: `src/MyDownloader.App/Views/MainWindow.axaml(.cs)`

- [ ] **Step 1: TaskItemViewModel**

创建 `src/MyDownloader.App/ViewModels/TaskItemViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Downloader;
using MyDownloader.Core;

namespace MyDownloader.App.ViewModels
{
    /// <summary>列表行 ViewModel，按 Id 增量更新。</summary>
    public partial class TaskItemViewModel : ObservableObject
    {
        public string Id { get; }

        public TaskItemViewModel(string id) => Id = id;

        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _sizeText = string.Empty;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private string _speedText = string.Empty;
        [ObservableProperty] private string _etaText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private DownloadStatus _status;

        public bool IsRunning => Status == DownloadStatus.Running;
        public bool IsPaused => Status == DownloadStatus.Paused;
        public bool IsStopped => Status == DownloadStatus.Stopped;
        public bool IsCompleted => Status == DownloadStatus.Completed;
        public bool CanPause => IsRunning;
        public bool CanResume => IsPaused || IsStopped;
        public bool CanOpen => IsCompleted;

        public void Apply(TaskSnapshot snap)
        {
            FileName = snap.FileName;
            var size = DownloadTools.FitByteUnit(snap.TotalBytes, out string unit);
            SizeText = $"{size:0.00}{unit}";
            Progress = snap.Progress;
            SpeedText = snap.SpeedText;
            EtaText = snap.Eta;
            Status = snap.Status;
            StatusText = snap.Status switch
            {
                DownloadStatus.Completed => "已完成",
                DownloadStatus.Failed => "失败",
                DownloadStatus.Paused => "已暂停",
                DownloadStatus.Stopped => "已取消",
                DownloadStatus.Running => "下载中",
                _ => "未开始"
            };
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsStopped));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanResume));
            OnPropertyChanged(nameof(CanOpen));
        }
    }
}
```

- [ ] **Step 2: MainViewModel**

创建 `src/MyDownloader.App/ViewModels/MainViewModel.cs`：

```csharp
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

            // 新增
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

            // 移除
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
```

- [ ] **Step 3: 主窗口 XAML（列表 + 状态栏）**

将 `src/MyDownloader.App/Views/MainWindow.axaml` 替换为：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:MyDownloader.App.ViewModels"
        xmlns:dg="using:Avalonia.Controls"
        x:Class="MyDownloader.App.Views.MainWindow"
        x:DataType="vm:MainViewModel"
        Title="MyDownloader" Width="980" Height="600" MinWidth="720" MinHeight="420">
  <DockPanel>
    <!-- 顶部工具栏 -->
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="12">
      <Button Content="+ 添加任务" Classes="accent" Command="{Binding }" Name="AddButton"/>
      <Button Content="暂停全部" Command="{Binding PauseAllCommand}"/>
      <Button Content="继续全部" Command="{Binding ResumeAllCommand}"/>
    </StackPanel>

    <!-- 底部状态栏 -->
    <Border DockPanel.Dock="Bottom" Background="#F5F5F5" Padding="12,6">
      <StackPanel Orientation="Horizontal" Spacing="24">
        <TextBlock Text="{Binding TotalSpeedText, StringFormat='总速度: {0}'}"/>
        <TextBlock Text="{Binding ActiveCount, StringFormat='活动任务: {0}'}"/>
        <TextBlock Text="{Binding CompletedCount, StringFormat='已完成: {0}'}"/>
      </StackPanel>
    </Border>

    <!-- 任务列表 -->
    <DataGrid ItemsSource="{Binding Items}"
              SelectedItem="{Binding SelectedItem}"
              AutoGenerateColumns="False"
              IsReadOnly="True"
              GridLinesVisibility="Horizontal"
              CanUserResizeColumns="True"
              Name="TaskGrid">
      <DataGrid.Columns>
        <DataGridTemplateColumn Header="文件名" Width="*">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="vm:TaskItemViewModel">
              <TextBlock Text="{Binding FileName}" VerticalAlignment="Center"/>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
        <DataGridTextColumn Header="大小" Binding="{Binding SizeText}" Width="110"/>
        <DataGridTemplateColumn Header="进度" Width="180">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="vm:TaskItemViewModel">
              <ProgressBar Value="{Binding Progress}" Minimum="0" Maximum="100"
                           Height="14" VerticalAlignment="Center"/>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
        <DataGridTextColumn Header="速度" Binding="{Binding SpeedText}" Width="100"/>
        <DataGridTextColumn Header="剩余" Binding="{Binding EtaText}" Width="90"/>
        <DataGridTextColumn Header="状态" Binding="{Binding StatusText}" Width="90"/>
      </DataGrid.Columns>
    </DataGrid>
  </DockPanel>
</Window>
```

- [ ] **Step 4: 主窗口代码绑定 ViewModel + 行操作**

将 `src/MyDownloader.App/Views/MainWindow.axaml.cs` 替换为：

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
```

> 行操作（暂停/继续/取消/查看/打开/删除）在 Task 8 补上右键菜单/按钮；本任务先跑通列表与状态栏。

- [ ] **Step 5: 编译运行**

Run: `dotnet build src/MyDownloader.App/MyDownloader.App.csproj`
Expected: `Build succeeded`。

- [ ] **Step 6: Commit**

```bash
git add src/MyDownloader.App
git commit -m "feat: 主窗口任务列表与状态栏（MVVM 拉快照增量刷新）"
```

---

## Task 5: 托盘、主题、通知服务

**Files:**
- Create: `src/MyDownloader.App/Services/TrayService.cs`
- Create: `src/MyDownloader.App/Services/ThemeService.cs`
- Create: `src/MyDownloader.App/Services/NotificationService.cs`
- Modify: `src/MyDownloader.App/App.axaml`、`App.axaml.cs`

- [ ] **Step 1: ThemeService**

创建 `src/MyDownloader.App/Services/ThemeService.cs`：

```csharp
using Avalonia;
using Avalonia.Styling;
using MyDownloader.Core;

namespace MyDownloader.App.Services
{
    /// <summary>浅/深色主题切换，持久化到设置。</summary>
    public static class ThemeService
    {
        public static void Apply(AppSettings settings)
        {
            if (Application.Current == null) return;
            Application.Current.RequestedThemeVariant =
                settings.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public static void Toggle()
        {
            var s = DownloadManager.Instance.Settings;
            s.DarkTheme = !s.DarkTheme;
            DownloadManager.Instance.SaveSettings();
            Apply(s);
        }
    }
}
```

- [ ] **Step 2: AppSettings 增加 DarkTheme**

在 `src/MyDownloader.Core/AppSettings.cs` 中增加属性：

```csharp
        /// <summary>是否使用深色主题。</summary>
        public bool DarkTheme { get; set; }
```

- [ ] **Step 3: NotificationService（自绘 Toast）**

创建 `src/MyDownloader.App/Services/NotificationService.cs`：

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace MyDownloader.App.Services
{
    /// <summary>
    /// 轻量 Toast 通知：屏幕右下角、无边框、自动淡出。
    /// Avalonia TrayIcon 无气泡 API，故自绘。
    /// </summary>
    public static class NotificationService
    {
        public static void Show(string title, string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var text = new StackPanel { Spacing = 4 };
                text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold });
                text.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });

                var win = new Window
                {
                    SystemDecorations = SystemDecorations.None,
                    ShowInTaskbar = false,
                    CanResize = false,
                    Topmost = true,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Background = Brushes.Transparent,
                    Content = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(240, 32, 32, 32)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 12),
                        MaxWidth = 320,
                        Child = text
                    }
                };

                win.Opened += (_, _) =>
                {
                    var screen = win.Screens.Primary;
                    if (screen != null)
                    {
                        var wa = screen.WorkingArea;
                        win.Position = new PixelPoint(
                            wa.X + wa.Width - (int)win.Bounds.Width - 20,
                            wa.Y + wa.Height - (int)win.Bounds.Height - 20);
                    }
                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    t.Tick += (_, _) => { t.Stop(); win.Close(); };
                    t.Start();
                };
                win.Show();
            });
        }
    }
}
```

- [ ] **Step 4: TrayService**

创建 `src/MyDownloader.App/Services/TrayService.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Threading;
using MyDownloader.Core;

namespace MyDownloader.App.Services
{
    /// <summary>托盘图标与菜单（单击显示主窗口，Tooltip 显示总进度）。</summary>
    public class TrayService : IDisposable
    {
        private readonly TrayIcon _tray;
        private readonly NativeMenu _menu;
        private readonly NativeMenuItem _pauseAll;
        private readonly NativeMenuItem _resumeAll;
        private readonly DispatcherTimer _timer;
        private readonly Action _showMainWindow;
        private readonly Action _exit;

        public TrayService(Action showMainWindow, Action exit)
        {
            _showMainWindow = showMainWindow;
            _exit = exit;

            _menu = new NativeMenu();
            var show = new NativeMenuItem("显示主窗口");
            show.Click += (_, _) => _showMainWindow();
            _menu.Add(show);

            _pauseAll = new NativeMenuItem("暂停全部");
            _pauseAll.Click += (_, _) => DownloadManager.Instance.PauseAll();
            _menu.Add(_pauseAll);

            _resumeAll = new NativeMenuItem("继续全部");
            _resumeAll.Click += (_, _) => DownloadManager.Instance.ResumeAll();
            _menu.Add(_resumeAll);

            _menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) => _exit();
            _menu.Add(exitItem);

            _tray = new TrayIcon
            {
                Icon = TrayIconHelper.LoadIcon(),
                ToolTipText = "MyDownloader · 空闲",
                Menu = _menu,
                IsVisible = true
            };
            _tray.Clicked += (_, _) => _showMainWindow();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => RefreshTooltip();
            _timer.Start();
        }

        private void RefreshTooltip()
        {
            var snaps = DownloadManager.Instance.GetSnapshots();
            var active = snaps.Where(s => s.IsActive).ToList();
            var hasRunning = snaps.Any(s => s.Status == Downloader.DownloadStatus.Running);
            var hasPaused = snaps.Any(s => s.Status == Downloader.DownloadStatus.Paused);

            _pauseAll.IsEnabled = hasRunning;
            _resumeAll.IsEnabled = hasPaused;

            string text;
            if (active.Count == 0)
            {
                text = "MyDownloader · 空闲";
            }
            else
            {
                var total = active.Sum(s => s.TotalBytes);
                var recv = active.Sum(s => s.ReceivedBytes);
                var pct = total > 0 ? (int)(recv * 100 / total) : 0;
                var speed = active.Sum(s => s.SpeedBytesPerSecond);
                text = $"总进度 {pct}% · {active.Count} 个任务 · ↓ {PathTools.FormatSpeed(speed)}";
            }
            _tray.ToolTipText = text.Length > 63 ? text[..63] : text;
        }

        public void Dispose()
        {
            _timer.Stop();
            _tray.IsVisible = false;
            _tray.Dispose();
        }
    }

    internal static class TrayIconHelper
    {
        /// <summary>加载托盘图标（内嵌资源）。</summary>
        public static WindowIcon? LoadIcon()
        {
            try
            {
                var uri = new Uri("avares://MyDownloader.App/Assets/app.ico");
                return new WindowIcon(uri);
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 5: 接线 App.axaml.cs（主题 + 托盘 + 关窗隐藏）**

将 `src/MyDownloader.App/App.axaml.cs` 替换为：

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyDownloader.App.Services;
using MyDownloader.App.Views;
using MyDownloader.Core;

namespace MyDownloader.App
{
    public partial class App : Application
    {
        private TrayService? _tray;
        private MainWindow? _mainWindow;

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                DownloadManager.Instance.LoadPersistedTasks();
                ThemeService.Apply(DownloadManager.Instance.Settings);

                _mainWindow = new MainWindow();
                _mainWindow.Closing += (_, e) =>
                {
                    // 关窗即隐藏到托盘，仅显式退出才真正关闭。
                    if (!_exiting)
                    {
                        e.Cancel = true;
                        _mainWindow.Hide();
                    }
                };
                desktop.MainWindow = _mainWindow;

                _tray = new TrayService(
                    showMainWindow: () =>
                    {
                        _mainWindow.Show();
                        _mainWindow.WindowState = WindowState.Normal;
                        _mainWindow.Activate();
                    },
                    exit: () => { _exiting = true; DownloadManager.Instance.Shutdown(); _tray?.Dispose(); desktop.Shutdown(); });
            }
            base.OnFrameworkInitializationCompleted();
        }

        private bool _exiting;
    }
}
```

- [ ] **Step 6: 编译**

Run: `dotnet build src/MyDownloader.App/MyDownloader.App.csproj`
Expected: `Build succeeded`。

- [ ] **Step 7: Commit**

```bash
git add src/MyDownloader.App src/MyDownloader.Core
git commit -m "feat: 托盘/主题/自绘Toast通知服务与关窗隐藏"
```

---

## Task 6: 详情窗口（分块进度）

**Files:**
- Create: `src/MyDownloader.App/ViewModels/TaskDetailViewModel.cs`
- Create: `src/MyDownloader.App/Views/TaskDetailWindow.axaml(.cs)`

- [ ] **Step 1: TaskDetailViewModel**

创建 `src/MyDownloader.App/ViewModels/TaskDetailViewModel.cs`：

```csharp
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

            // 按实际块数动态同步（数量变化才重建）。
            if (Chunks.Count != snap.ChunkSnapshots.Count)
            {
                Chunks.Clear();
                foreach (var c in snap.ChunkSnapshots) Chunks.Add(c);
            }
            else
            {
                for (int i = 0; i < Chunks.Count; i++) Chunks[i] = snap.ChunkSnapshots[i];
            }
        }

        [RelayCommand] private void Pause() => _manager.Pause(TaskId);
        [RelayCommand] private void Resume() => _manager.Resume(TaskId);
        [RelayCommand] private void Cancel() => _manager.Cancel(TaskId);
        [RelayCommand] private void Redownload() => _manager.Redownload(TaskId);

        public void Stop() => _timer.Stop();
    }
}
```

- [ ] **Step 2: 详情窗口 XAML**

创建 `src/MyDownloader.App/Views/TaskDetailWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:MyDownloader.App.ViewModels"
        xmlns:core="using:MyDownloader.Core"
        x:Class="MyDownloader.App.Views.TaskDetailWindow"
        x:DataType="vm:TaskDetailViewModel"
        Title="下载任务" Width="640" Height="520" MinWidth="520" MinHeight="420">
  <DockPanel Margin="16">
    <!-- 顶部信息 -->
    <StackPanel DockPanel.Dock="Top" Spacing="6">
      <TextBlock Text="{Binding FileName}" FontSize="18" FontWeight="Bold" TextTrimming="CharacterEllipsis"/>
      <SelectableTextBlock Text="{Binding Url}" Foreground="#666" TextTrimming="CharacterEllipsis"/>
      <SelectableTextBlock Text="{Binding FilePath}" Foreground="#666" TextTrimming="CharacterEllipsis"/>
    </StackPanel>

    <!-- 控制按钮 -->
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,12">
      <Button Content="暂停" Command="{Binding PauseCommand}"/>
      <Button Content="继续" Command="{Binding ResumeCommand}"/>
      <Button Content="取消" Command="{Binding CancelCommand}"/>
      <Button Content="重新下载" Command="{Binding RedownloadCommand}"/>
    </StackPanel>

    <!-- 总进度 -->
    <StackPanel DockPanel.Dock="Top" Spacing="6">
      <ProgressBar Value="{Binding Progress}" Minimum="0" Maximum="100" Height="18"/>
      <StackPanel Orientation="Horizontal" Spacing="20">
        <TextBlock Text="{Binding SpeedText, StringFormat='速度: {0}'}"/>
        <TextBlock Text="{Binding OverviewText, StringFormat='总览: {0}'}"/>
        <TextBlock Text="{Binding EtaText, StringFormat='剩余: {0}'}"/>
        <TextBlock Text="{Binding StatusText}"/>
      </StackPanel>
    </StackPanel>

    <!-- 分块进度 -->
    <ScrollViewer Margin="0,12,0,0">
      <ItemsControl ItemsSource="{Binding Chunks}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="core:ChunkSnapshot">
            <Grid ColumnDefinitions="60,*,70" Margin="0,3">
              <TextBlock Text="{Binding Index, StringFormat='块 {0}'}" VerticalAlignment="Center"/>
              <ProgressBar Grid.Column="1" Value="{Binding Progress}" Minimum="0" Maximum="100" Height="12" VerticalAlignment="Center"/>
              <TextBlock Grid.Column="2" Text="{Binding State}" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="8,0,0,0"/>
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </DockPanel>
</Window>
```

- [ ] **Step 3: 详情窗口代码**

创建 `src/MyDownloader.App/Views/TaskDetailWindow.axaml.cs`：

```csharp
using Avalonia.Controls;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class TaskDetailWindow : Window
    {
        private readonly TaskDetailViewModel _vm;

        public TaskDetailWindow() : this(string.Empty) { }

        public TaskDetailWindow(string taskId)
        {
            InitializeComponent();
            _vm = new TaskDetailViewModel(taskId);
            DataContext = _vm;
            Closed += (_, _) => _vm.Stop();
        }
    }
}
```

- [ ] **Step 4: 编译**

Run: `dotnet build src/MyDownloader.App/MyDownloader.App.csproj`
Expected: `Build succeeded`。

- [ ] **Step 5: Commit**

```bash
git add src/MyDownloader.App
git commit -m "feat: 任务详情窗口（总进度 + 动态分块进度 + 控制按钮）"
```

---

## Task 7: 添加任务对话框

**Files:**
- Create: `src/MyDownloader.App/ViewModels/AddTaskViewModel.cs`
- Create: `src/MyDownloader.App/Views/AddTaskWindow.axaml(.cs)`

- [ ] **Step 1: AddTaskViewModel**

创建 `src/MyDownloader.App/ViewModels/AddTaskViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDownloader.Core;

namespace MyDownloader.App.ViewModels
{
    /// <summary>添加任务对话框 ViewModel。</summary>
    public partial class AddTaskViewModel : ObservableObject
    {
        [ObservableProperty] private string _url = string.Empty;
        [ObservableProperty] private string _filePath = string.Empty;
        [ObservableProperty] private string? _error;

        public bool Confirmed { get; private set; }

        public AddTaskViewModel() { }

        public AddTaskViewModel(string url)
        {
            Url = url;
            UpdatePath();
        }

        partial void OnUrlChanged(string value) => UpdatePath();

        private void UpdatePath()
        {
            if (string.IsNullOrWhiteSpace(Url)) return;
            var dir = DownloadManager.Instance.Settings.GetDefaultDir();
            var file = PathTools.BuildFileName(Url, DateTime.Now);
            FilePath = Path.Combine(dir, file);
        }

        [RelayCommand]
        private void Confirm()
        {
            Error = null;
            if (!IsHttpUrl(Url)) { Error = "请输入合法的 http/https 地址"; return; }
            if (string.IsNullOrWhiteSpace(FilePath)) { Error = "保存路径不能为空"; return; }
            Confirmed = true;
            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);

        public Action<bool>? RequestClose;

        private static bool IsHttpUrl(string s)
            => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: 添加对话框 XAML**

创建 `src/MyDownloader.App/Views/AddTaskWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:MyDownloader.App.ViewModels"
        x:Class="MyDownloader.App.Views.AddTaskWindow"
        x:DataType="vm:AddTaskViewModel"
        Title="新建任务" Width="620" Height="240"
        WindowStartupLocation="CenterOwner">
  <StackPanel Margin="16" Spacing="12">
    <StackPanel Spacing="4">
      <TextBlock Text="下载地址"/>
      <TextBox Text="{Binding Url}" Watermark="https://..."/>
    </StackPanel>
    <StackPanel Spacing="4">
      <TextBlock Text="保存路径"/>
      <Grid ColumnDefinitions="*,Auto">
        <TextBox Text="{Binding FilePath}"/>
        <Button Grid.Column="1" Content="浏览..." Name="BrowseButton" Margin="8,0,0,0"/>
      </Grid>
    </StackPanel>
    <TextBlock Text="{Binding Error}" Foreground="#D33" IsVisible="{Binding Error, Converter={x:Static ObjectConverters.IsNotNull}}"/>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8">
      <Button Content="取消" Command="{Binding CancelCommand}"/>
      <Button Content="确定" Classes="accent" Command="{Binding ConfirmCommand}"/>
    </StackPanel>
  </StackPanel>
</Window>
```

- [ ] **Step 3: 添加对话框代码（含浏览按钮）**

创建 `src/MyDownloader.App/Views/AddTaskWindow.axaml.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class AddTaskWindow : Window
    {
        private readonly AddTaskViewModel _vm;

        public AddTaskWindow() : this(new AddTaskViewModel()) { }

        public AddTaskWindow(AddTaskViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            _vm.RequestClose = ok => Close(ok);
            DataContext = _vm;

            var browse = this.FindControl<Button>("BrowseButton");
            if (browse != null) browse.Click += async (_, _) => await BrowseAsync();
        }

        private async Task BrowseAsync()
        {
            var files = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "选择保存位置",
                SuggestedFileName = System.IO.Path.GetFileName(_vm.FilePath),
                DefaultExtension = System.IO.Path.GetExtension(_vm.FilePath).TrimStart('.')
            });
            if (files != null)
            {
                _vm.FilePath = files.Path.LocalPath;
            }
        }
    }
}
```

- [ ] **Step 4: 编译**

Run: `dotnet build src/MyDownloader.App/MyDownloader.App.csproj`
Expected: `Build succeeded`。

- [ ] **Step 5: Commit**

```bash
git add src/MyDownloader.App
git commit -m "feat: 添加任务对话框（URL 推导文件名 + 路径浏览）"
```

---

## Task 8: 主窗口交互接线（行操作/详情/添加/拖拽）

**Files:**
- Modify: `src/MyDownloader.App/Views/MainWindow.axaml`
- Modify: `src/MyDownloader.App/Views/MainWindow.axaml.cs`
- Modify: `src/MyDownloader.App/ViewModels/MainViewModel.cs`

- [ ] **Step 1: MainViewModel 增加打开对话框/详情/打开文件夹的命令**

在 `MainViewModel.cs` 增加：

```csharp
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
```

- [ ] **Step 2: 主窗口 XAML 增加行操作列与双击详情**

在 `MainWindow.axaml` 的 `DataGrid.Columns` 末尾增加操作列（在状态列之后）：

```xml
        <DataGridTemplateColumn Header="操作" Width="240">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate x:DataType="vm:TaskItemViewModel">
              <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
                <Button Content="暂停" FontSize="12" Padding="6,2"
                        IsVisible="{Binding CanPause}"
                        Command="{Binding $parent[DataGrid].((vm:MainViewModel)DataContext).PauseItemCommand}"
                        CommandParameter="{Binding}"/>
                <Button Content="继续" FontSize="12" Padding="6,2"
                        IsVisible="{Binding CanResume}"
                        Command="{Binding $parent[DataGrid].((vm:MainViewModel)DataContext).ResumeItemCommand}"
                        CommandParameter="{Binding}"/>
                <Button Content="查看" FontSize="12" Padding="6,2"
                        Command="{Binding $parent[DataGrid].((vm:MainViewModel)DataContext).ViewDetailCommand}"
                        CommandParameter="{Binding}"/>
                <Button Content="打开" FontSize="12" Padding="6,2"
                        IsVisible="{Binding CanOpen}"
                        Command="{Binding $parent[DataGrid].((vm:MainViewModel)DataContext).OpenItemCommand}"
                        CommandParameter="{Binding}"/>
                <Button Content="删除" FontSize="12" Padding="6,2"
                        Command="{Binding $parent[DataGrid].((vm:MainViewModel)DataContext).RemoveItemCommand}"
                        CommandParameter="{Binding}"/>
              </StackPanel>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
```

同时给 `DataGrid` 增加双击详情：`DoubleTapped="OnRowDoubleTapped"`。

- [ ] **Step 3: 主窗口代码：注入对话框/详情/拖拽**

将 `MainWindow.axaml.cs` 替换为：

```csharp
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MyDownloader.App.ViewModels;

namespace MyDownloader.App.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            _vm.RequestAddTask = ShowAddTaskAsync;
            _vm.RequestShowDetail = ShowDetail;
            DataContext = _vm;

            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        private async Task<(string url, string path)?> ShowAddTaskAsync(string? url)
        {
            var dlg = new AddTaskWindow(new AddTaskViewModel(url ?? string.Empty));
            var ok = await dlg.ShowDialog<bool>(this);
            return ok ? (dlg.UrlResult, dlg.PathResult) : null;
        }

        private void ShowDetail(string id)
        {
            var win = new TaskDetailWindow(id);
            win.Show(this);
        }

        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_vm.SelectedItem != null) ShowDetail(_vm.SelectedItem.Id);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Text) || e.Data.Contains(DataFormats.Files)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            var text = e.Data.GetText();
            if (!string.IsNullOrWhiteSpace(text) && text.Trim().StartsWith("http"))
            {
                _ = ShowAddTaskAsync(text.Trim());
            }
        }
    }
}
```

- [ ] **Step 4: AddTaskWindow 暴露结果属性**

在 `AddTaskWindow.axaml.cs` 增加：

```csharp
        public string UrlResult => _vm.Url;
        public string PathResult => _vm.FilePath;
```

- [ ] **Step 5: 编译**

Run: `dotnet build src/MyDownloader.App/MyDownloader.App.csproj`
Expected: `Build succeeded`。

- [ ] **Step 6: 手动验证**

Run: `dotnet run --project src/MyDownloader.App/MyDownloader.App.csproj`
Expected：窗口出现；点"添加任务"弹对话框；添加后列表出现行；双击行弹详情；关窗后托盘常驻。

- [ ] **Step 7: Commit**

```bash
git add src/MyDownloader.App
git commit -m "feat: 主窗口行操作、详情/添加对话框接线与拖拽添加"
```

---

## Task 9: 剪贴板监听

**Files:**
- Create: `src/MyDownloader.App/Services/ClipboardMonitor.cs`
- Modify: `src/MyDownloader.App/App.axaml.cs`

- [ ] **Step 1: ClipboardMonitor（Win32 消息钩子）**

创建 `src/MyDownloader.App/Services/ClipboardMonitor.cs`：

```csharp
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Win32;

namespace MyDownloader.App.Services
{
    /// <summary>
    /// 剪贴板变更监听：通过 Win32 AddClipboardFormatListener 监听 WM_CLIPBOARDUPDATE。
    /// </summary>
    public class ClipboardMonitor : IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private IntPtr _hwnd;
        private readonly Action<string> _onUrl;
        private readonly Func<string?> _readClipboard;
        private string _last = string.Empty;

        public ClipboardMonitor(Action<string> onUrl, Func<string?> readClipboard)
        {
            _onUrl = onUrl;
            _readClipboard = readClipboard;
        }

        /// <summary>在主窗口句柄就绪后调用。</summary>
        public void Attach(TopLevel topLevel)
        {
            var handle = topLevel.TryGetPlatformHandle();
            if (handle == null) return;
            _hwnd = handle.Handle;
            AddClipboardFormatListener(_hwnd);

            Win32Properties.AddWndProcHook(topLevel, WndProc);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                var text = _readClipboard()?.Trim();
                if (!string.IsNullOrEmpty(text)
                    && text != _last
                    && (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    _last = text;
                    _onUrl(text);
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                try { RemoveClipboardFormatListener(_hwnd); } catch { /* 忽略 */ }
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
```

> 注意：`Win32Properties.AddWndProcHook` 的确切签名以实际 Avalonia 12 API 为准；若签名不同（如返回 `IntPtr` 的委托形式），按其签名调整 `WndProc` 参数。

- [ ] **Step 2: App.axaml.cs 接线剪贴板**

在 `App.axaml.cs` 中，创建主窗口后附加剪贴板监听。在 `_mainWindow = new MainWindow();` 之后加入：

```csharp
                var clip = new ClipboardMonitor(
                    onUrl: url =>
                    {
                        // 主窗口可见时直接弹添加框，否则用 Toast 提示。
                        if (_mainWindow.IsVisible)
                        {
                            _mainWindow.PromptAddUrl(url);
                        }
                        else
                        {
                            NotificationService.Show("检测到下载链接", url);
                        }
                    },
                    readClipboard: () => _mainWindow.Clipboard?.GetTextAsync().GetAwaiter().GetResult());
                _clipboardMonitor = clip;
                _mainWindow.Opened += (_, _) => clip.Attach(_mainWindow);
```

并增加字段 `private ClipboardMonitor? _clipboardMonitor;`，在退出时 `_clipboardMonitor?.Dispose();`。

- [ ] **Step 3: MainWindow 增加 PromptAddUrl**

在 `MainWindow.axaml.cs` 增加：

```csharp
        public void PromptAddUrl(string url) => _ = ShowAddTaskAsync(url);
```

- [ ] **Step 4: 编译**

Run: `dotnet build src/MyDownloader.App/MyDownloader.App.csproj`
Expected: `Build succeeded`。

- [ ] **Step 5: Commit**

```bash
git add src/MyDownloader.App
git commit -m "feat: 剪贴板监听（Win32 消息钩子）与隐藏时 Toast 提示"
```

---

## Task 10: 旧项目清理与终验

**Files:**
- Modify: `MyDownloader.sln`（删旧项目）
- Delete: `MyDownloader/`（WinForms 项目）

- [ ] **Step 1: 确认新应用功能对等**

手动核对：添加/暂停/继续/取消/重新下载/删除、详情分块进度、托盘常驻、主题切换、剪贴板、拖拽——均可用后，再删旧项目。

- [ ] **Step 2: 从解决方案移除并删除旧项目**

```powershell
dotnet sln MyDownloader.sln remove MyDownloader/MyDownloader.csproj
Remove-Item -Recurse -Force "MyDownloader"
```

- [ ] **Step 3: 全量构建**

Run: `dotnet build MyDownloader.sln`
Expected: `Build succeeded`，0 Error。

- [ ] **Step 4: 全量测试**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 19`

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: 删除旧 WinForms 项目，完成 Avalonia 重写"
```

---

## Self-Review 检查结论

- **规格覆盖**：项目结构/迁移（Task 0-1）、引擎改动分块快照+状态机+重下（Task 2）、Avalonia 骨架（Task 3）、主窗口列表+状态栏（Task 4）、托盘/主题/通知（Task 5）、详情分块（Task 6）、添加对话框（Task 7）、行操作/拖拽（Task 8）、剪贴板（Task 9）、删旧项目（Task 10）——全部有对应任务。
- **明确排除项**：未引入设置窗口、搜索/筛选/排序、浏览器扩展、独立进程、悬浮窗、开机自启。
- **类型一致性**：`ChunkSnapshot`、`TaskSnapshot.ChunkSnapshots`、`DownloadManager.Redownload/Resume/Cancel`、`MainViewModel`、`TaskDetailViewModel`、`AddTaskViewModel` 在任务间签名一致。
- **风险提示**：`Win32Properties.AddWndProcHook` 签名（Task 9）、`NativeMenuItem.IsEnabled`（Task 5）、`TrayIcon.Dispose`（Task 5）以实际 Avalonia 12.1.4 API 为准，编译报错时按 API 微调。
