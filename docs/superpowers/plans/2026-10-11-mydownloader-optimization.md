# MyDownloader 下载体验优化 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让下载任务能后台常驻跑完、进度一目了然、启动下载更省事、重启可续传，同时保持个人项目轻量。

**Architecture:** 将下载引擎从窗口剥离为单例 `DownloadManager`，用 `TrayAppContext`（`ApplicationContext`）承载托盘与主窗口生命周期；UI 通过 500ms 定时器"拉取"内存快照增量刷新，不走高频事件推送。持久化改用 DTO 白名单，序列化分块位置以支持断点续传。

**Tech Stack:** C# / .NET 8 (`net8.0-windows`) / WinForms / AntdUI 1.6.2 / 本地 `Downloader` 库 / QDX.Service SQLite / xUnit（新增测试项目）

**规格来源:** `docs/superpowers/specs/2026-10-11-mydownloader-optimization-design.md`

**关键约定（实现时务必遵守）:**
- 注释、文本一律使用中文。
- 不得提交（commit）除非任务步骤明确要求。
- 持久化**禁止**直接序列化 `DownloadPackage`（其 `Storage` 会打开文件句柄并拉起后台线程）。
- 续传**必须**走 `DownloadFileTaskAsync(DownloadPackage, string[])` 重载，不得走 `DownloadFileTaskAsync(string, string)`（后者会 `File.Delete`）。
- 代理认证凭据设在 `IWebProxy.Credentials`，不得设 `RequestConfiguration.Credentials`。

---

## 文件结构总览

**主项目 `MyDownloader/`（现有，将修改/新增）**
- 新增 `Classes/TaskSnapshot.cs` — 只读快照模型（UI/托盘读取）。
- 新增 `Classes/TaskHandle.cs` — 运行时任务句柄。
- 新增 `Classes/DownloadTaskDto.cs` — 持久化 DTO（白名单字段）。
- 新增 `Classes/DownloadManager.cs` — 单例下载引擎（核心）。
- 新增 `Classes/DownloadConfigFactory.cs` — 统一构建 `DownloadConfiguration`（含系统代理）。
- 新增 `Classes/PathTools.cs` — 文件名推导、同名处理、默认目录、字节/ETA 格式化。
- 新增 `TrayAppContext.cs` — 托盘 + 应用生命周期 + 定时器。
- 修改 `Program.cs` — 入口改 `Application.Run(new TrayAppContext())`。
- 修改 `MainForm.cs` / `MainForm.Designer.cs` — 进度列、增量刷新、行操作、拖拽。
- 修改 `Forms/DownloadTaskInfoForm.cs` — 改为读 Manager 快照 + 控制命令。
- 修改 `Forms/DownloadTaskSelectForm.cs` — 默认目录来自 Manager。
- 修改 `Classes/ConstData.cs` — DB 绝对路径、默认目录常量。
- 修改 `Classes/DownloadTools.cs` — 保留字节格式化，新增 ETA/速度格式化。

**存储 `qdx/QDX.Service/`（现有，将修改）**
- 修改 `StorageServices/SQLiteDataProvider/SqLiteDataProvider.cs` — 修复重复插入、缓存键含库标识、Get 返回深拷贝。

**测试项目（新增）**
- 新增 `tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
- 新增 `tests/MyDownloader.Tests/DownloadTaskDtoTests.cs`
- 新增 `tests/MyDownloader.Tests/PathToolsTests.cs`
- 新增 `tests/MyDownloader.Tests/DownloadConfigFactoryTests.cs`
- 新增 `tests/MyDownloader.Tests/DownloadManagerTests.cs`

---

## Task 0: 搭建测试项目与解决方案

**Files:**
- Create: `tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
- Modify: `MyDownloader.sln`
- Create: `tests/MyDownloader.Tests/SmokeTests.cs`

- [ ] **Step 1: 创建测试项目文件**

创建 `tests/MyDownloader.Tests/MyDownloader.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\MyDownloader\MyDownloader.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建冒烟测试**

创建 `tests/MyDownloader.Tests/SmokeTests.cs`：

```csharp
namespace MyDownloader.Tests;

public class SmokeTests
{
    [Fact]
    public void 测试框架可用()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 3: 加入解决方案**

Run:
```powershell
dotnet sln MyDownloader.sln add tests/MyDownloader.Tests/MyDownloader.Tests.csproj
```
Expected: 输出 `Project ... added to the solution.`

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Commit**

```bash
git add tests/ MyDownloader.sln
git commit -m "test: 新增 xUnit 测试项目并接入解决方案"
```

---

## Task 1: 持久化 DTO（TDD）

**Files:**
- Create: `MyDownloader/Classes/DownloadTaskDto.cs`
- Test: `tests/MyDownloader.Tests/DownloadTaskDtoTests.cs`

- [ ] **Step 1: 写失败测试**

创建 `tests/MyDownloader.Tests/DownloadTaskDtoTests.cs`：

```csharp
using System.Text.Json;
using Downloader;
using MyDownloader.Classes;

namespace MyDownloader.Tests;

public class DownloadTaskDtoTests
{
    [Fact]
    public void 序列化往返_保留分块位置与重试参数()
    {
        var dto = new DownloadTaskDto
        {
            Id = "abc",
            Url = "https://example.com/a.zip",
            FilePath = @"C:\tmp\a.zip",
            Status = DownloadStatus.Paused,
            TotalFileSize = 1000,
            FileName = @"C:\tmp\a.zip",
            Urls = new[] { "https://example.com/a.zip" },
            IsSupportDownloadInRange = true,
            Chunks = new[]
            {
                new ChunkDto { Start = 0, End = 499, Position = 250, MaxTryAgainOnFailover = 5, Timeout = 1000 },
                new ChunkDto { Start = 500, End = 999, Position = 0, MaxTryAgainOnFailover = 5, Timeout = 1000 },
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<DownloadTaskDto>(json)!;

        Assert.Equal("abc", back.Id);
        Assert.Equal(2, back.Chunks!.Length);
        Assert.Equal(250, back.Chunks[0].Position);
        Assert.Equal(5, back.Chunks[0].MaxTryAgainOnFailover);
        Assert.Equal(1000, back.Chunks[0].Timeout);
        Assert.True(back.IsSupportDownloadInRange);
    }

    [Fact]
    public void 序列化_不包含Storage字段()
    {
        var dto = new DownloadTaskDto { Id = "x" };
        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("Storage", json);
        Assert.DoesNotContain("ConcurrentStream", json);
    }

    [Fact]
    public void 转DownloadPackage_分块已还原且Storage为空()
    {
        var dto = new DownloadTaskDto
        {
            TotalFileSize = 100,
            FileName = @"C:\tmp\b.bin",
            Chunks = new[] { new ChunkDto { Start = 0, End = 99, Position = 40, MaxTryAgainOnFailover = 5, Timeout = 1000 } }
        };

        var package = dto.ToPackage();

        Assert.NotNull(package.Chunks);
        Assert.Equal(40, package.Chunks![0].Position);
        Assert.Null(package.Storage);
        Assert.Equal(100, package.TotalFileSize);
    }

    [Fact]
    public void FromTaskInfo_空分块时Chunks为空()
    {
        var info = new DownloadTaskInfo
        {
            ID = "id1",
            Url = "https://e.com/f",
            FilePath = @"C:\tmp\f",
            DownloadPackage = new DownloadPackage { Status = DownloadStatus.None, TotalFileSize = 0 }
        };

        var dto = DownloadTaskDto.FromTaskInfo(info);

        Assert.Equal("id1", dto.Id);
        Assert.Null(dto.Chunks);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter DownloadTaskDtoTests`
Expected: 编译失败，`DownloadTaskDto` / `ChunkDto` 未定义。

- [ ] **Step 3: 实现 DTO**

创建 `MyDownloader/Classes/DownloadTaskDto.cs`：

```csharp
using Downloader;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 分块持久化数据（仅白名单字段，避免序列化 ConcurrentStream）。
    /// </summary>
    public class ChunkDto
    {
        public long Start { get; set; }
        public long End { get; set; }
        public long Position { get; set; }
        public int MaxTryAgainOnFailover { get; set; }
        public int Timeout { get; set; }
    }

    /// <summary>
    /// 下载任务的持久化 DTO。禁止直接序列化 DownloadPackage（其 Storage 会打开文件句柄）。
    /// </summary>
    public class DownloadTaskDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? FilePath { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DownloadStatus Status { get; set; }
        public string? ErrorMessage { get; set; }

        // package 白名单
        public long TotalFileSize { get; set; }
        public string? FileName { get; set; }
        public string[]? Urls { get; set; }
        public bool IsSupportDownloadInRange { get; set; }
        public ChunkDto[]? Chunks { get; set; }

        public static DownloadTaskDto FromTaskInfo(DownloadTaskInfo info)
        {
            var package = info.DownloadPackage;
            return new DownloadTaskDto
            {
                Id = info.ID,
                Url = info.Url,
                FilePath = info.FilePath,
                StartTime = info.StartTime,
                EndTime = info.EndTime,
                Status = package?.Status ?? DownloadStatus.None,
                TotalFileSize = package?.TotalFileSize ?? 0,
                FileName = package?.FileName ?? info.FilePath,
                Urls = package?.Urls,
                IsSupportDownloadInRange = package?.IsSupportDownloadInRange ?? true,
                Chunks = package?.Chunks == null
                    ? null
                    : package.Chunks.Select(c => new ChunkDto
                    {
                        Start = c.Start,
                        End = c.End,
                        Position = c.Position,
                        MaxTryAgainOnFailover = c.MaxTryAgainOnFailover,
                        Timeout = c.Timeout
                    }).ToArray()
            };
        }

        /// <summary>
        /// 还原为 DownloadPackage。不创建任何文件句柄或后台任务，Storage 保持为 null。
        /// </summary>
        public DownloadPackage ToPackage()
        {
            var package = new DownloadPackage
            {
                Status = Status,
                TotalFileSize = TotalFileSize,
                FileName = FileName,
                Urls = Urls,
                IsSupportDownloadInRange = IsSupportDownloadInRange
            };
            if (Chunks != null)
            {
                package.Chunks = Chunks.Select(c => new Chunk(c.Start, c.End)
                {
                    Position = c.Position,
                    MaxTryAgainOnFailover = c.MaxTryAgainOnFailover,
                    Timeout = c.Timeout
                }).ToArray();
            }
            return package;
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter DownloadTaskDtoTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Commit**

```bash
git add MyDownloader/Classes/DownloadTaskDto.cs tests/MyDownloader.Tests/DownloadTaskDtoTests.cs
git commit -m "feat: 新增下载任务持久化 DTO（排除 Storage，保留分块重试参数）"
```

---

## Task 2: 路径与格式化工具（TDD）

**Files:**
- Create: `MyDownloader/Classes/PathTools.cs`
- Test: `tests/MyDownloader.Tests/PathToolsTests.cs`

- [ ] **Step 1: 写失败测试**

创建 `tests/MyDownloader.Tests/PathToolsTests.cs`：

```csharp
using MyDownloader.Classes;

namespace MyDownloader.Tests;

public class PathToolsTests
{
    [Theory]
    [InlineData("https://a.com/dir/file.zip", "file.zip")]
    [InlineData("https://a.com/dir/file.zip?token=1", "file.zip")]
    [InlineData("https://a.com/", "")]
    public void 从URL推导文件名(string url, string expected)
    {
        Assert.Equal(expected, PathTools.ExtractFileName(url));
    }

    [Fact]
    public void 无文件名时返回默认名()
    {
        var name = PathTools.BuildFileName("https://a.com/", DateTime.Parse("2026-01-02 03:04:05"));
        Assert.Equal("download_20260102030405", name);
    }

    [Fact]
    public void 同名文件自动加序号()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mdtest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, "a.zip");
            File.WriteAllText(target, "x");
            var result = PathTools.EnsureUniquePath(target);
            Assert.Equal(Path.Combine(dir, "a(1).zip"), result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void 格式化速度()
    {
        Assert.Equal("1.00MB/s", PathTools.FormatSpeed(1024 * 1024));
        Assert.Equal("--", PathTools.FormatSpeed(0));
    }

    [Fact]
    public void 格式化剩余时间()
    {
        Assert.Equal("--", PathTools.FormatEta(1000, 0));
        Assert.Equal("00:00:10", PathTools.FormatEta(1024 * 1024, 1024 * 1024 / 10));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter PathToolsTests`
Expected: 编译失败，`PathTools` 未定义。

- [ ] **Step 3: 实现工具类**

创建 `MyDownloader/Classes/PathTools.cs`：

```csharp
using System.Text.RegularExpressions;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 路径、文件名与显示格式化工具。
    /// </summary>
    public static class PathTools
    {
        /// <summary>
        /// 从 URL 推导文件名；去查询串；无法推导时返回空串。
        /// </summary>
        public static string ExtractFileName(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            var withoutQuery = url.Split('?')[0].Split('#')[0];
            var last = withoutQuery.Split('/').LastOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(last)) return string.Empty;
            return Uri.UnescapeDataString(last);
        }

        /// <summary>
        /// 无法推导文件名时，生成默认文件名。
        /// </summary>
        public static string BuildFileName(string url, DateTime now)
        {
            var name = ExtractFileName(url);
            return string.IsNullOrEmpty(name) ? $"download_{now:yyyyMMddHHmmss}" : name;
        }

        /// <summary>
        /// 若目标路径已存在，追加 (1)、(2)… 序号。
        /// </summary>
        public static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (int i = 1; ; i++)
            {
                var candidate = Path.Combine(dir, $"{name}({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
        }

        /// <summary>
        /// 格式化为可读速度；速度为 0 或负时返回 "--"。
        /// </summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "--";
            var v = DownloadTools.FitByteUnit((long)bytesPerSecond, out string unit);
            return $"{v.ToString("#0.00")}{unit}/s";
        }

        /// <summary>
        /// 根据剩余字节与速度计算 ETA；速度为 0 或负时返回 "--"。
        /// </summary>
        public static string FormatEta(long remainingBytes, double bytesPerSecond)
        {
            if (bytesPerSecond <= 0 || remainingBytes <= 0) return "--";
            var seconds = (long)(remainingBytes / bytesPerSecond);
            if (seconds > 359999) return "--";
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter PathToolsTests`
Expected: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 5: Commit**

```bash
git add MyDownloader/Classes/PathTools.cs tests/MyDownloader.Tests/PathToolsTests.cs
git commit -m "feat: 新增路径/文件名/速度/ETA 工具类"
```

---

## Task 3: 下载配置工厂与系统代理（TDD）

**Files:**
- Create: `MyDownloader/Classes/DownloadConfigFactory.cs`
- Test: `tests/MyDownloader.Tests/DownloadConfigFactoryTests.cs`

- [ ] **Step 1: 写失败测试**

创建 `tests/MyDownloader.Tests/DownloadConfigFactoryTests.cs`：

```csharp
using MyDownloader.Classes;

namespace MyDownloader.Tests;

public class DownloadConfigFactoryTests
{
    [Fact]
    public void 默认配置_分块与并行符合约定()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: false);
        Assert.Equal(8, config.ChunkCount);
        Assert.True(config.ParallelDownload);
        Assert.Equal(5, config.MaxTryAgainOnFailover);
        Assert.True(config.CheckDiskSizeBeforeDownload);
    }

    [Fact]
    public void 启用系统代理_Proxy非空且凭据为默认凭据()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: true);
        Assert.NotNull(config.RequestConfiguration.Proxy);
        Assert.Same(System.Net.CredentialCache.DefaultCredentials, config.RequestConfiguration.Proxy.Credentials);
    }

    [Fact]
    public void 禁用系统代理_Proxy为空()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: false);
        Assert.Null(config.RequestConfiguration.Proxy);
    }

    [Fact]
    public void 不设置服务器凭据()
    {
        var config = DownloadConfigFactory.Create(useSystemProxy: true);
        Assert.Null(config.RequestConfiguration.Credentials);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter DownloadConfigFactoryTests`
Expected: 编译失败，`DownloadConfigFactory` 未定义。

- [ ] **Step 3: 实现配置工厂**

创建 `MyDownloader/Classes/DownloadConfigFactory.cs`：

```csharp
using Downloader;
using System.Net;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 统一构建下载配置，保证所有任务配置一致。
    /// </summary>
    public static class DownloadConfigFactory
    {
        /// <summary>
        /// 创建下载配置。
        /// </summary>
        /// <param name="useSystemProxy">是否跟随系统代理（默认开启）。</param>
        public static DownloadConfiguration Create(bool useSystemProxy = true)
        {
            var option = DownloadTools.GetDefaultDowmloadOptions(null);
            option.ChunkCount = 8;
            option.ParallelDownload = true;
            option.MaxTryAgainOnFailover = 5;
            option.CheckDiskSizeBeforeDownload = true;

            if (useSystemProxy)
            {
                var proxy = WebRequest.GetSystemWebProxy();
                proxy.Credentials = CredentialCache.DefaultCredentials;
                option.RequestConfiguration.Proxy = proxy;
                // 注意：不设置 RequestConfiguration.Credentials（那是服务器认证，不是代理认证）
            }

            return option;
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter DownloadConfigFactoryTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Commit**

```bash
git add MyDownloader/Classes/DownloadConfigFactory.cs tests/MyDownloader.Tests/DownloadConfigFactoryTests.cs
git commit -m "feat: 新增下载配置工厂，统一分块参数与系统代理"
```

---

## Task 4: 修复存储层（重复插入 / 缓存键 / 深拷贝）

**Files:**
- Modify: `qdx/QDX.Service/StorageServices/SQLiteDataProvider/SqLiteDataProvider.cs`

- [ ] **Step 1: 修改缓存键，纳入数据库标识**

在 `SqLiteDataProvider.cs` 中，将 `KeyType` 结构体改为包含数据库标识：

```csharp
    public struct KeyType : IEqualityComparer<KeyType>
    {
        public string db;
        public string key;
        public Type type;

        public bool Equals(KeyType x, KeyType y)
        {
            return x.db == y.db && x.key == y.key && x.type == y.type;
        }

        public int GetHashCode([DisallowNull] KeyType obj)
        {
            return HashCode.Combine(obj.db, obj.key, obj.type);
        }
    }
```

- [ ] **Step 2: 更新所有 KeyType 构造处，传入数据库标识**

在 `Get<T>`、`Set<T>`、`Delete<T>` 中，将 `new KeyType(){ key = key, type = typeof(T) }` 改为：

```csharp
            KeyType keyType = new KeyType()
            {
                db = SqliteDataBase,
                key = key,
                type = typeof(T)
            };
```

（三处均需修改：`Get`、`Set`、`Delete`。）

- [ ] **Step 3: 修复 Set 的重复插入**

将 `Set<T>` 方法整体替换为"存在则更新、不存在则插入"：

```csharp
        public void Set<T>(string key = "", T? value = default, TimeSpan expireTime = default)
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            KeyType keyType = new KeyType()
            {
                db = SqliteDataBase,
                key = key,
                type = typeof(T)
            };

            var json = JsonSerializer.Serialize(value);
            var item = dbContext?.DataItems.FirstOrDefault(x => x.Key == key);
            if (item == null)
            {
                dbContext?.DataItems.Add(new DataItem()
                {
                    Key = key,
                    Value = json,
                    Type = 0
                });
            }
            else
            {
                item.Value = json;
            }
            dbContext?.SaveChanges();
            cachedData[keyType] = value;
        }
```

- [ ] **Step 4: Get 返回深拷贝，避免污染缓存**

将 `Get<T>` 中读取缓存与写入缓存的部分改为深拷贝：

```csharp
        public T? Get<T>(string key = "")
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            KeyType keyType = new KeyType()
            {
                db = SqliteDataBase,
                key = key,
                type = typeof(T)
            };
            if (cachedData.ContainsKey(keyType) && cachedData[keyType] != null)
            {
                return DeepCopy<T>(cachedData[keyType]);
            }
            T? result = default;
            var data = dbContext.DataItems.FirstOrDefault(x => x.Key == key);
            if (data != null)
            {
                var json = data.Value;
                result = JsonSerializer.Deserialize<T>(json);
            }
            cachedData.TryAdd(keyType, result);
            return DeepCopy<T>(result);
        }

        private static T? DeepCopy<T>(object? value)
        {
            if (value == null) return default;
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(json);
        }
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build qdx/QDX.Service/QDX.Service.csproj`
Expected: `Build succeeded`，0 Error。

- [ ] **Step 6: Commit**

```bash
git add qdx/QDX.Service/StorageServices/SQLiteDataProvider/SqLiteDataProvider.cs
git commit -m "fix: 修复存储层重复插入、缓存键跨库污染与缓存引用外泄"
```

---

## Task 5: 常量与默认目录调整

**Files:**
- Modify: `MyDownloader/Classes/ConstData.cs`

- [ ] **Step 1: 改为绝对路径常量**

将 `ConstData.cs` 替换为：

```csharp
namespace MyDownloader.Classes
{
    public static class ConstData
    {
        /// <summary>数据目录（%LocalAppData%\MyDownloader）。</summary>
        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyDownloader");

        public static string DownloadMainDB => Path.Combine(DataDir, "DownloadMainDB.db");
        public static string DownloadSettingDB => Path.Combine(DataDir, "DownloadSettingDB.db");

        public const string DownloadTaskInfoList = "DownloadTaskInfoList";
        public const string AppSettings = "AppSettings";

        /// <summary>并发下载上限。</summary>
        public const int MaxConcurrentDownloads = 3;
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: `Build succeeded`。

- [ ] **Step 3: Commit**

```bash
git add MyDownloader/Classes/ConstData.cs
git commit -m "refactor: 数据目录改为绝对路径并新增并发上限常量"
```

---

## Task 6: 快照与句柄模型

**Files:**
- Create: `MyDownloader/Classes/TaskSnapshot.cs`
- Create: `MyDownloader/Classes/TaskHandle.cs`
- Create: `MyDownloader/Classes/AppSettings.cs`

- [ ] **Step 1: 创建快照模型**

创建 `MyDownloader/Classes/TaskSnapshot.cs`：

```csharp
using Downloader;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 任务只读快照，供 UI 与托盘读取（线程安全：每次刷新重建）。
    /// </summary>
    public class TaskSnapshot
    {
        public string Id { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string? Url { get; init; }
        public long TotalBytes { get; init; }
        public long ReceivedBytes { get; init; }
        public double SpeedBytesPerSecond { get; init; }
        public DownloadStatus Status { get; init; }
        public string? ErrorMessage { get; init; }

        /// <summary>进度百分比（0-100）。</summary>
        public double Progress => TotalBytes <= 0 ? 0 : Math.Min(100, ReceivedBytes * 100.0 / TotalBytes);

        /// <summary>剩余时间显示。</summary>
        public string Eta => PathTools.FormatEta(Math.Max(0, TotalBytes - ReceivedBytes), SpeedBytesPerSecond);

        /// <summary>速度显示。</summary>
        public string SpeedText => PathTools.FormatSpeed(SpeedBytesPerSecond);

        public bool IsActive => Status == DownloadStatus.Running || Status == DownloadStatus.Paused || Status == DownloadStatus.Created;
    }
}
```

- [ ] **Step 2: 创建句柄模型**

创建 `MyDownloader/Classes/TaskHandle.cs`：

```csharp
using Downloader;

namespace MyDownloader.Classes
{
    /// <summary>
    /// 单个下载任务的运行时句柄（仅 DownloadManager 内部使用）。
    /// </summary>
    internal class TaskHandle
    {
        public string Id { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DownloadService Service { get; set; } = null!;
        public DownloadTaskInfo Info { get; set; } = null!;

        // 实时进度（由库事件更新）
        public long ReceivedBytes;
        public long TotalBytes;
        public double SpeedBytesPerSecond;
        public string? ErrorMessage;

        /// <summary>是否已入队等待调度。</summary>
        public bool IsQueued;
    }
}
```

- [ ] **Step 3: 创建设置模型**

创建 `MyDownloader/Classes/AppSettings.cs`：

```csharp
namespace MyDownloader.Classes
{
    /// <summary>
    /// 应用设置（持久化于设置库）。
    /// </summary>
    public class AppSettings
    {
        /// <summary>默认下载目录。</summary>
        public string? DefaultDownloadDir { get; set; }

        /// <summary>是否启用剪贴板监听。</summary>
        public bool ClipboardMonitorEnabled { get; set; } = true;

        /// <summary>是否已提示过"最小化到托盘"。</summary>
        public bool TrayTipShown { get; set; }

        /// <summary>是否跟随系统代理。</summary>
        public bool UseSystemProxy { get; set; } = true;

        /// <summary>取默认下载目录（系统"下载"文件夹）。</summary>
        public string GetDefaultDir()
        {
            if (!string.IsNullOrWhiteSpace(DefaultDownloadDir) && Directory.Exists(DefaultDownloadDir))
                return DefaultDownloadDir!;
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            return Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: `Build succeeded`。

- [ ] **Step 5: Commit**

```bash
git add MyDownloader/Classes/TaskSnapshot.cs MyDownloader/Classes/TaskHandle.cs MyDownloader/Classes/AppSettings.cs
git commit -m "feat: 新增任务快照/句柄/设置模型"
```

---

## Task 7: DownloadManager 核心（TDD）

**Files:**
- Create: `MyDownloader/Classes/DownloadManager.cs`
- Test: `tests/MyDownloader.Tests/DownloadManagerTests.cs`

- [ ] **Step 1: 写失败测试（并发调度与快照）**

创建 `tests/MyDownloader.Tests/DownloadManagerTests.cs`：

```csharp
using MyDownloader.Classes;

namespace MyDownloader.Tests;

public class DownloadManagerTests
{
    [Fact]
    public void 单例唯一()
    {
        Assert.Same(DownloadManager.Instance, DownloadManager.Instance);
    }

    [Fact]
    public void 空管理器_快照为空()
    {
        var snapshots = DownloadManager.Instance.GetSnapshots();
        Assert.NotNull(snapshots);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter DownloadManagerTests`
Expected: 编译失败，`DownloadManager` 未定义。

- [ ] **Step 3: 实现 DownloadManager**

创建 `MyDownloader/Classes/DownloadManager.cs`：

```csharp
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
            List<DownloadTaskInfo> list;
            lock (_dbLock)
            {
                list = _mainProvider.Get<List<DownloadTaskInfo>>(ConstData.DownloadTaskInfoList) ?? new List<DownloadTaskInfo>();
            }
            foreach (var info in list)
            {
                if (info.DownloadPackage == null) continue;
                if (info.DownloadPackage.Status is DownloadStatus.Running
                    or DownloadStatus.Paused or DownloadStatus.Stopped or DownloadStatus.Created)
                {
                    info.DownloadPackage.Status = DownloadStatus.Paused;
                }
                _tasks[info.ID] = new TaskHandle
                {
                    Id = info.ID,
                    Url = info.Url ?? string.Empty,
                    FilePath = info.FilePath ?? string.Empty,
                    Info = info,
                    ReceivedBytes = info.DownloadPackage.Chunks?.Sum(c => c.Position) ?? 0,
                    TotalBytes = info.DownloadPackage.TotalFileSize
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
            // 状态已由库设置；此处仅触发 UI 刷新
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
                var list = _tasks.Values.Select(h => h.Info).ToList();
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
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj --filter DownloadManagerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

> 注意：`DownloadManager` 构造会创建数据目录与 DB，测试环境下会写入 `%LocalAppData%\MyDownloader`，属预期行为。

- [ ] **Step 5: Commit**

```bash
git add MyDownloader/Classes/DownloadManager.cs tests/MyDownloader.Tests/DownloadManagerTests.cs
git commit -m "feat: 新增单例下载引擎 DownloadManager（并发调度/快照/续传/持久化）"
```

---

## Task 8: TrayAppContext 托盘与生命周期

**Files:**
- Create: `MyDownloader/TrayAppContext.cs`
- Modify: `MyDownloader/Program.cs`

- [ ] **Step 1: 实现 TrayAppContext**

创建 `MyDownloader/TrayAppContext.cs`：

```csharp
using MyDownloader.Classes;
using System.Windows.Forms;

namespace MyDownloader
{
    /// <summary>
    /// 应用上下文：承载托盘图标、主窗口生命周期与统一刷新定时器。
    /// </summary>
    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private MainForm? _mainForm;
        private readonly ToolStripMenuItem _pauseAllItem;
        private readonly ToolStripMenuItem _resumeAllItem;
        private readonly ToolStripMenuItem _clipboardItem;
        private bool _exiting;

        public TrayAppContext()
        {
            DownloadManager.Instance.LoadPersistedTasks();

            _tray = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "MyDownloader · 空闲",
                Visible = true
            };
            _tray.DoubleClick += (_, _) => ShowMainForm();

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, (_, _) => ShowMainForm());
            _pauseAllItem = new ToolStripMenuItem("暂停全部", null, (_, _) => DownloadManager.Instance.PauseAll());
            _resumeAllItem = new ToolStripMenuItem("继续全部", null, (_, _) => DownloadManager.Instance.ResumeAll());
            menu.Items.Add(_pauseAllItem);
            menu.Items.Add(_resumeAllItem);
            _clipboardItem = new ToolStripMenuItem("剪贴板监听", null, (_, _) =>
            {
                var s = DownloadManager.Instance.Settings;
                s.ClipboardMonitorEnabled = !s.ClipboardMonitorEnabled;
                _clipboardItem.Checked = s.ClipboardMonitorEnabled;
                DownloadManager.Instance.SaveSettings();
                _mainForm?.ApplyClipboardSetting(s.ClipboardMonitorEnabled);
            })
            { CheckOnClick = false, Checked = DownloadManager.Instance.Settings.ClipboardMonitorEnabled };
            menu.Items.Add(_clipboardItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => ExitApp());
            _tray.ContextMenuStrip = menu;

            DownloadManager.Instance.TaskCompleted += OnTaskCompleted;

            _timer = new System.Windows.Forms.Timer { Interval = 500 };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();

            ShowMainForm();
        }

        private void ShowMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm();
            }
            _mainForm.Show();
            if (_mainForm.WindowState == FormWindowState.Minimized)
                _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        }

        /// <summary>由 MainForm 在关闭时调用：隐藏而非退出。</summary>
        public void HideMainForm()
        {
            if (_mainForm == null) return;
            _mainForm.Hide();
            var s = DownloadManager.Instance.Settings;
            if (!s.TrayTipShown)
            {
                s.TrayTipShown = true;
                DownloadManager.Instance.SaveSettings();
                _tray.ShowBalloonTip(3000, "MyDownloader", "已最小化到托盘，下载继续在后台进行", ToolTipIcon.Info);
            }
        }

        private void Refresh()
        {
            var snapshots = DownloadManager.Instance.GetSnapshots();
            var active = snapshots.Where(x => x.IsActive).ToList();
            var hasRunning = snapshots.Any(x => x.Status == Downloader.DownloadStatus.Running);
            var hasPaused = snapshots.Any(x => x.Status == Downloader.DownloadStatus.Paused);

            _pauseAllItem.Enabled = hasRunning;
            _resumeAllItem.Enabled = hasPaused;

            if (active.Count == 0)
            {
                _tray.Text = "MyDownloader · 空闲";
            }
            else
            {
                var total = active.Sum(x => x.TotalBytes);
                var recv = active.Sum(x => x.ReceivedBytes);
                var pct = total > 0 ? (int)(recv * 100 / total) : 0;
                var speed = active.Sum(x => x.SpeedBytesPerSecond);
                _tray.Text = $"总进度 {pct}% · {active.Count} 个任务 · ↓ {PathTools.FormatSpeed(speed)}";
            }
            if (_tray.Text.Length > 63) _tray.Text = _tray.Text.Substring(0, 63);

            _mainForm?.RefreshFromSnapshots(snapshots);
        }

        private void OnTaskCompleted(object? sender, string id)
        {
            var snap = DownloadManager.Instance.GetSnapshot(id);
            if (snap == null) return;
            if (snap.Status == Downloader.DownloadStatus.Completed)
                _tray.ShowBalloonTip(3000, "下载完成", snap.FileName, ToolTipIcon.Info);
            else if (snap.Status == Downloader.DownloadStatus.Failed)
                _tray.ShowBalloonTip(3000, "下载失败", snap.FileName, ToolTipIcon.Error);
        }

        private void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            _timer.Stop();
            DownloadManager.Instance.Shutdown();
            _tray.Visible = false;
            _tray.Dispose();
            _mainForm?.ForceClose();
            ExitThreadCore();
        }
    }
}
```

- [ ] **Step 2: 修改入口**

将 `MyDownloader/Program.cs` 的 `Main` 替换为：

```csharp
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayAppContext());
        }
```

- [ ] **Step 3: 编译验证（此时 MainForm 尚未加 RefreshFromSnapshots/ForceClose，会报错，属预期）**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: 报错 `MainForm` 缺少 `RefreshFromSnapshots` / `ForceClose`（将在 Task 9 补上）。记录错误信息。

- [ ] **Step 4: Commit（先提交 TrayAppContext，编译错误随后续任务修复）**

```bash
git add MyDownloader/TrayAppContext.cs MyDownloader/Program.cs
git commit -m "feat: 新增 TrayAppContext 托盘常驻与统一刷新定时器"
```

---

## Task 9: MainForm 改造（进度列 / 增量刷新 / 行操作 / 拖拽 / 剪贴板）

**Files:**
- Modify: `MyDownloader/MainForm.cs`
- Modify: `MyDownloader/MainForm.Designer.cs`

- [ ] **Step 1: 改造表格列定义与 TableItem**

将 `MainForm.cs` 整体替换为（保留命名空间与类名）：

```csharp
using AntdUI;
using MyDownloader.Classes;
using MyDownloader.Forms;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MyDownloader
{
    public partial class MainForm : Form
    {
        private readonly Dictionary<string, TableItem> _items = new();
        private bool _clipboardEnabled;
        private string _lastClipboard = string.Empty;

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public MainForm()
        {
            InitializeComponent();
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
            FormClosing += MainForm_FormClosing;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            InitMainTable();
            ApplyClipboardSetting(DownloadManager.Instance.Settings.ClipboardMonitorEnabled);
            RefreshFromSnapshots(DownloadManager.Instance.GetSnapshots());
        }

        private void InitMainTable()
        {
            tableMain.Columns = new AntdUI.ColumnCollection {
                new AntdUI.ColumnCheck("check"){ Fixed=false },
                new AntdUI.Column("fileName","文件名"){ Fixed=false,Width="auto"},
                new AntdUI.Column("size","大小"){ Fixed=false,Width="auto"},
                new AntdUI.Column("progress","进度"){ Fixed=false,Width="auto"},
                new AntdUI.Column("speed","速度",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("eta","剩余",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("status","状态",AntdUI.ColumnAlign.Center),
                new AntdUI.Column("oper","操作",AntdUI.ColumnAlign.Center){ Fixed=false,Width="auto"},
            };
        }

        /// <summary>由 TrayAppContext 每 500ms 调用，按 Id 增量更新。</summary>
        public void RefreshFromSnapshots(IReadOnlyList<TaskSnapshot> snapshots)
        {
            if (IsDisposed) return;
            bool changed = false;

            foreach (var snap in snapshots)
            {
                if (!_items.TryGetValue(snap.Id, out var item))
                {
                    item = new TableItem { Id = snap.Id };
                    _items[snap.Id] = item;
                    changed = true;
                }
                if (item.Apply(snap)) changed = true;
            }

            // 移除已删除的任务
            var ids = snapshots.Select(s => s.Id).ToHashSet();
            foreach (var gone in _items.Keys.Where(k => !ids.Contains(k)).ToList())
            {
                _items.Remove(gone);
                changed = true;
            }

            if (changed)
            {
                tableMain.DataSource = _items.Values.ToList();
            }
        }

        private void tableMain_CellButtonClick(object sender, TableButtonEventArgs e)
        {
            if (e.Record is TableItem data)
            {
                switch (e.Btn.Id)
                {
                    case "view":
                        var form = new DownloadTaskInfoForm(data.Id);
                        form.Show();
                        break;
                    case "pause":
                        DownloadManager.Instance.Pause(data.Id);
                        break;
                    case "resume":
                        DownloadManager.Instance.Resume(data.Id);
                        break;
                    case "open":
                        var snap = DownloadManager.Instance.GetSnapshot(data.Id);
                        if (snap != null && File.Exists(snap.FilePath))
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{snap.FilePath}\"");
                        break;
                    case "del":
                        DownloadManager.Instance.Remove(data.Id);
                        _items.Remove(data.Id);
                        tableMain.DataSource = _items.Values.ToList();
                        break;
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            using var downloadTaskSelectForm = new DownloadTaskSelectForm();
            if (downloadTaskSelectForm.ShowDialog() == DialogResult.OK)
            {
                var res = downloadTaskSelectForm.Result;
                DownloadManager.Instance.AddTask(res.url, res.path);
                RefreshFromSnapshots(DownloadManager.Instance.GetSnapshots());
            }
            await Task.CompletedTask;
        }

        // ---- 拖拽 ----

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (e.Data.GetDataPresent(DataFormats.UniformResourceLocator)
                || e.Data.GetDataPresent(DataFormats.Text)
                || e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop)
                && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var f in files.Where(x => x.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var line in File.ReadAllLines(f))
                    {
                        var url = line.Trim();
                        if (IsHttpUrl(url)) AddFromUrl(url);
                    }
                }
                return;
            }
            var text = (e.Data.GetData(DataFormats.UniformResourceLocator) as string)
                       ?? (e.Data.GetData(DataFormats.Text) as string);
            if (!string.IsNullOrWhiteSpace(text) && IsHttpUrl(text.Trim()))
                AddFromUrl(text.Trim());
        }

        private void AddFromUrl(string url)
        {
            using var selectForm = new DownloadTaskSelectForm((url, string.Empty));
            if (selectForm.ShowDialog() == DialogResult.OK)
            {
                var res = selectForm.Result;
                DownloadManager.Instance.AddTask(res.url, res.path);
            }
        }

        private static bool IsHttpUrl(string s)
            => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        // ---- 剪贴板监听 ----

        public void ApplyClipboardSetting(bool enabled)
        {
            if (_clipboardEnabled == enabled) return;
            _clipboardEnabled = enabled;
            if (!IsHandleCreated) return;
            if (enabled) AddClipboardFormatListener(Handle);
            else RemoveClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
            }
            base.WndProc(ref m);
        }

        private void OnClipboardChanged()
        {
            if (!_clipboardEnabled || !Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Trim();
            if (text == _lastClipboard || !IsHttpUrl(text)) return;
            _lastClipboard = text;

            if (Visible && WindowState != FormWindowState.Minimized)
            {
                AddFromUrl(text);
            }
        }

        // ---- 关闭即隐藏 ----

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !IsForcedClose)
            {
                e.Cancel = true;
                if (Context is TrayAppContext ctx) ctx.HideMainForm();
                else Hide();
            }
        }

        public bool IsForcedClose { get; private set; }

        /// <summary>退出时由 TrayAppContext 调用，真正关闭。</summary>
        public void ForceClose()
        {
            IsForcedClose = true;
            Close();
        }
    }

    public class TableItem : NotifyProperty
    {
        public bool check { get; set; } = true;
        public string? fileName { get; set; }
        public string Id { get; set; } = string.Empty;
        public string? size { get; set; }
        public CellProgress? progress { get; set; }
        public string? speed { get; set; }
        public string? eta { get; set; }
        public CellBadge? status { get; set; }
        public CellLink[]? oper { get; set; }

        /// <summary>把快照写回本行；有变化返回 true。</summary>
        public bool Apply(TaskSnapshot snap)
        {
            bool changed = false;
            var name = snap.FileName;
            if (fileName != name) { fileName = name; OnPropertyChanged(nameof(fileName)); changed = true; }

            var size = DownloadTools.FitByteUnit(snap.TotalBytes, out string unit);
            var sizeText = $"{size.ToString("#0.00")}{unit}";
            if (this.size != sizeText) { this.size = sizeText; OnPropertyChanged(nameof(this.size)); changed = true; }

            var pct = (float)(snap.Progress / 100.0);
            if (progress == null)
            {
                progress = new CellProgress(pct);
                changed = true;
            }
            else if (Math.Abs(progress.Value - pct) > 0.001f)
            {
                progress.Value = pct;
                progress = progress; // 重新赋值以触发通知
                OnPropertyChanged(nameof(progress));
                changed = true;
            }

            if (speed != snap.SpeedText) { speed = snap.SpeedText; OnPropertyChanged(nameof(speed)); changed = true; }
            if (eta != snap.Eta) { eta = snap.Eta; OnPropertyChanged(nameof(eta)); changed = true; }

            var state = snap.Status switch
            {
                Downloader.DownloadStatus.Completed => TState.Success,
                Downloader.DownloadStatus.Failed => TState.Error,
                Downloader.DownloadStatus.Running => TState.Processing,
                Downloader.DownloadStatus.Paused => TState.Warn,
                _ => TState.Default
            };
            status = new CellBadge(state);
            changed = true;

            oper = snap.Status switch
            {
                Downloader.DownloadStatus.Completed => new[] { new CellLink("view", "查看"), new CellLink("open", "打开"), new CellLink("del", "删除") },
                Downloader.DownloadStatus.Running => new[] { new CellLink("pause", "暂停"), new CellLink("view", "查看"), new CellLink("del", "删除") },
                Downloader.DownloadStatus.Paused => new[] { new CellLink("resume", "继续"), new CellLink("view", "查看"), new CellLink("del", "删除") },
                _ => new[] { new CellLink("resume", "开始"), new CellLink("view", "查看"), new CellLink("del", "删除") }
            };
            return changed;
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: `Build succeeded`，0 Error。

> `TState` 实际枚举值为 `Primary / Default / Success / Error / Processing / Warn`（已核实），无 `Warning`，故暂停态用 `TState.Warn`。

- [ ] **Step 3: 手动验证（启动 + 托盘 + 关窗）**

Run: `dotnet run --project MyDownloader/MyDownloader.csproj`
Expected:
1. 出现主窗口与托盘图标。
2. 点击窗口 X → 窗口隐藏，托盘弹一次气泡。
3. 双击托盘 → 主窗口重新显示。

- [ ] **Step 4: Commit**

```bash
git add MyDownloader/MainForm.cs MyDownloader/MainForm.Designer.cs
git commit -m "feat: 主界面进度/速度/剩余列、增量刷新、行操作、拖拽与剪贴板监听"
```

---

## Task 10: DownloadTaskSelectForm 使用默认目录

**Files:**
- Modify: `MyDownloader/Forms/DownloadTaskSelectForm.cs`

- [ ] **Step 1: 默认目录改为 Manager 设置**

将 `DownloadTaskSelectForm.cs` 中的 `inputUrl_TextChanged` 与 `SearchClick` 相关逻辑更新：

```csharp
        private void inputUrl_TextChanged(object sender, EventArgs e)
        {
            var dir = DownloadManager.Instance.Settings.GetDefaultDir();
            var file = PathTools.BuildFileName(inputUrl.Text, DateTime.Now);
            searchInput1.InputControl.Text = Path.Combine(dir, file);
        }

        private void SearchClick(object? sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                searchInput1.InputControl.Text = saveFileDialog.FileName;
            }
        }
```

并在文件顶部 `using` 增加：

```csharp
using MyDownloader.Classes;
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: `Build succeeded`。

- [ ] **Step 3: Commit**

```bash
git add MyDownloader/Forms/DownloadTaskSelectForm.cs
git commit -m "feat: 添加任务默认保存目录改为 Manager 设置（系统下载文件夹）"
```

---

## Task 11: DownloadTaskInfoForm 简化为读快照

**Files:**
- Modify: `MyDownloader/Forms/DownloadTaskInfoForm.cs`
- Modify: `MyDownloader/Forms/DownloadTaskInfoForm.Designer.cs`（若构造函数签名变化需同步）

- [ ] **Step 1: 构造函数改为接收任务 Id**

将 `DownloadTaskInfoForm.cs` 整体替换为：

```csharp
using AntdUI;
using Downloader;
using MyDownloader.Classes;

namespace MyDownloader
{
    public partial class DownloadTaskInfoForm : Form
    {
        private readonly string _taskId;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Dictionary<string, AntdUI.Progress> _chunkProgress = new();

        public DownloadTaskInfoForm(string taskId)
        {
            InitializeComponent();
            _taskId = taskId;
            _timer = new System.Windows.Forms.Timer { Interval = 500 };
            _timer.Tick += (_, _) => RefreshView();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var snap = DownloadManager.Instance.GetSnapshot(_taskId);
            if (snap != null)
            {
                input1.Text = snap.Url ?? string.Empty;
                input2.Text = snap.FilePath;
            }
            RefreshView();
            _timer.Start();
        }

        private void RefreshView()
        {
            var snap = DownloadManager.Instance.GetSnapshot(_taskId);
            if (snap == null) return;

            progressTotal.Value = (float)(snap.Progress / 100.0);
            label2.Text = $"速度:{snap.SpeedText}";
            label3.Text = $"总览:{snap.ReceivedBytes}/{snap.TotalBytes}";
            label4.Text = snap.Status switch
            {
                DownloadStatus.Completed => "已完成",
                DownloadStatus.Failed => "失败:" + snap.ErrorMessage,
                DownloadStatus.Paused => "已暂停",
                DownloadStatus.Running => "正在下载",
                _ => "未开始"
            };
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            using var selectForm = new DownloadTaskSelectForm((input1.Text, input2.Text));
            if (selectForm.ShowDialog() == DialogResult.OK)
            {
                var res = selectForm.Result;
                DownloadManager.Instance.Remove(_taskId);
                DownloadManager.Instance.AddTask(res.url, res.path);
                Close();
            }
        }

        private void button1_Click_1(object sender, EventArgs e) => DownloadManager.Instance.Pause(_taskId);

        private void button2_Click(object sender, EventArgs e) => DownloadManager.Instance.Resume(_taskId);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 2: 删除 Designer 中不再引用的控件事件绑定**

在 `DownloadTaskInfoForm.Designer.cs` 中删除 `button1_Click`、`flowLayoutPanel4_Paint`、`label5_Click` 等已不存在方法的绑定（若保留会编译报错）。保留 `button1.Click += button1_Click_1;`、`button2.Click += button2_Click;`、`btnStart.Click += btnStart_Click;`。

- [ ] **Step 3: 编译验证**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: `Build succeeded`，0 Error。

- [ ] **Step 4: 运行全部测试**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
Expected: 全部 `Passed`。

- [ ] **Step 5: Commit**

```bash
git add MyDownloader/Forms/DownloadTaskInfoForm.cs MyDownloader/Forms/DownloadTaskInfoForm.Designer.cs
git commit -m "refactor: 任务详情窗口改为读 Manager 快照并下发控制命令"
```

---

## Task 12: 续传恢复与大小校验

**Files:**
- Modify: `MyDownloader/Classes/DownloadManager.cs`

- [ ] **Step 1: 在续传前校验服务端大小**

在 `DownloadManager.cs` 的 `RunAsync` 方法中，续传分支前增加大小校验：

```csharp
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

                    var localExists = File.Exists(h.FilePath);
                    if (!localExists || (remoteSize > 0 && remoteSize != package.TotalFileSize))
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
```

> 注意：`Request.GetFileSize()` 为 public（`Request.cs`），直接用 `DownloadConfigFactory.Create(...).RequestConfiguration` 构造 `Request` 探测即可，不要访问 `DownloadService.Options`（它是 `protected`，会编译失败）。

- [ ] **Step 2: 编译验证**

Run: `dotnet build MyDownloader/MyDownloader.csproj`
Expected: `Build succeeded`。

- [ ] **Step 3: 手动验证续传**

Run: `dotnet run --project MyDownloader/MyDownloader.csproj`
Expected:
1. 添加一个大文件任务，下载到约一半。
2. 关闭程序（托盘 → 退出）。
3. 重新启动 → 任务显示为"已暂停"，进度为上次百分比。
4. 点"继续" → 从断点继续，而非从头开始。

- [ ] **Step 4: Commit**

```bash
git add MyDownloader/Classes/DownloadManager.cs
git commit -m "feat: 续传前校验服务端大小与本地文件，不匹配则重下"
```

---

## Task 13: 终验与清理

**Files:**
- Modify: `MyDownloader/Classes/DownloadTools.cs`（如存在无用方法可保留，不强制删除）

- [ ] **Step 1: 全量构建**

Run: `dotnet build MyDownloader.sln`
Expected: `Build succeeded`，0 Error。

- [ ] **Step 2: 全量测试**

Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj`
Expected: 全部 `Passed`。

- [ ] **Step 3: 手动验收清单**

逐项确认：
1. 关主窗口 → 下载继续（托盘 Tooltip 进度递增）。
2. 托盘右键"暂停全部/继续全部"生效。
3. 表格进度/速度/剩余列实时刷新。
4. 行操作（暂停/继续/查看/打开/删除）正确。
5. 复制一个 http 链接 → 弹出添加框（主窗口可见时）。
6. 拖拽链接到窗口 → 弹出添加框。
7. 断网下载 → 任务显示失败，托盘失败气泡。
8. 重启 → 未完成任务在列表且可继续。
9. 托盘"退出" → 程序完全退出，进程消失。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: 终验通过，清理与收尾"
```

---

## Self-Review 检查结论

- **规格覆盖**：架构（Task 7/8）、托盘常驻（Task 8）、主界面进度（Task 9）、剪贴板+拖拽（Task 9）、续传与持久化修复（Task 1/4/12）、系统代理（Task 3）、退出流程（Task 8）、同名/默认目录（Task 2/10）、详情窗口简化（Task 11）——全部有对应任务。
- **明确排除项**：未引入设置窗口、搜索/筛选/排序、浏览器扩展、独立进程、悬浮窗、开机自启。
- **类型一致性**：`TaskSnapshot`、`TaskHandle`、`DownloadTaskDto`/`ChunkDto`、`DownloadManager.Instance`、`RefreshFromSnapshots`、`ForceClose`、`ApplyClipboardSetting`、`PathTools.*`、`DownloadConfigFactory.Create` 在任务间签名一致。
- **风险提示**：`TState` 枚举值（Task 9 Step 2）、`DownloadService.Options` 可访问性（Task 12 Step 1）需以实际编译结果为准微调。
