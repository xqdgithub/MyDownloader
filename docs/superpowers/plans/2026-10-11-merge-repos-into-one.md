# 仓库合并为单一自包含仓库 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 让 MyDownloader 成为单一自包含仓库：Downloader 改用 NuGet 包，QDX.Service 源码复制进本仓库，移除所有跨目录项目引用。

**Architecture:** `src/MyDownloader.Core` 引 `Downloader`(NuGet) + 本地 `src/QDX.Service`；`src/MyDownloader.App` 与 `tests` 只引 Core。外部 `E:\Projects\CSharp\Downloader`、`E:\Projects\CSharp\qdx` 保持原样不动。

**Tech Stack:** .NET 10 / NuGet `Downloader 3.1.2` / 本地 `QDX.Service`(net8.0) / Avalonia。

---

## 关键事实
- NuGet 上 `Downloader` 有 `3.1.2`（与本地版本一致）。
- `QDX.Service` 源码文件（复制这些，排除 bin/obj/*.user/*.db/PublishProfiles）：
  - `QDX.Service.csproj`
  - `DbTemoleteResource.Designer.cs`、`DbTemoleteResource.resx`
  - `HttpService/BaseResponse.cs`、`CommonResponse.cs`、`HttpService.cs`、`RestResponse.cs`
  - `StorageServices/IDataProvider.cs`
  - `StorageServices/SQLiteDataProvider/SQLiteDataContext.cs`、`SqLiteDataProvider.cs`、`Entities/DataItem.cs`
- 命名空间保持 `QDX.Serivices.*`（含原拼写），Core 的 `using` 无需改。
- `.gitignore` 已忽略 `[Bb]in/`、`[Oo]bj/`、`*.db`。

---

## Task 1: 复制 QDX.Service 进仓库并改引用

**Files:**
- Create: `src/QDX.Service/**`（复制）
- Modify: `src/MyDownloader.Core/MyDownloader.Core.csproj`
- Modify: `MyDownloader.sln`

- [ ] **Step 1: 复制 QDX.Service 源码**

```powershell
$src = "E:\Projects\CSharp\qdx\QDX.Service"
$dst = "E:\Projects\CSharp\MyDownloader\src\QDX.Service"
robocopy $src $dst /E /XD bin obj .vs Properties /XF *.user *.db
```
（`Properties` 仅含 PublishProfiles，排除；`*.user`、`*.db` 排除。）

- [ ] **Step 2: 确认复制结果**

Run: `Get-ChildItem "src\QDX.Service" -Recurse -File | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }`
Expected: 含 `QDX.Service.csproj`、`DbTemoleteResource.*`、`HttpService\*.cs`、`StorageServices\**\*.cs`。

- [ ] **Step 3: 改 Core 的项目引用**

将 `src/MyDownloader.Core/MyDownloader.Core.csproj` 的 `<ItemGroup>` 替换为：

```xml
  <ItemGroup>
    <PackageReference Include="Downloader" Version="3.1.2" />
    <ProjectReference Include="..\QDX.Service\QDX.Service.csproj" />
  </ItemGroup>
```

- [ ] **Step 4: 从 sln 移除外部项目并加入本地 QDX.Service**

```powershell
dotnet sln MyDownloader.sln remove ..\Downloader\src\Downloader\Downloader.csproj
dotnet sln MyDownloader.sln remove ..\qdx\QDX.Service\QDX.Service.csproj
dotnet sln MyDownloader.sln add src\QDX.Service\QDX.Service.csproj
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build MyDownloader.sln`
Expected: `Build succeeded`，0 Error。

- [ ] **Step 6: Commit**

```bash
git add src/QDX.Service src/MyDownloader.Core/MyDownloader.Core.csproj MyDownloader.sln
git commit -m "refactor: QDX.Service 源码并入仓库，Downloader 改用 NuGet 包，移除跨目录引用"
```

---

## Task 2: 验证与收尾

- [ ] **Step 1: 全量构建 + 测试**

Run: `dotnet build MyDownloader.sln` → 0 Error
Run: `dotnet test tests/MyDownloader.Tests/MyDownloader.Tests.csproj` → 19 passed

- [ ] **Step 2: 确认无残留跨目录引用**

Run: `Select-String -Path "*.sln","src\**\*.csproj","tests\**\*.csproj" -Pattern "\.\.\\\.\.\\Downloader|\.\.\\\.\.\\qdx"`
Expected: 无匹配。

- [ ] **Step 3: 应用启动冒烟**

Run: `dotnet run --project src/MyDownloader.App/MyDownloader.App.csproj`
Expected: 窗口正常出现。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: 验证单一仓库自包含构建通过"
```

---

## Self-Review
- **覆盖**：Downloader→NuGet（Task1 Step3）、QDX.Service 内联（Task1 Step1/3）、sln 清理（Task1 Step4）、验证（Task2）——齐。
- **类型/命名**：命名空间不变，调用方零改动。
- **风险**：`QDX.Service` 的 `HttpService` 若引用本仓库外类型会导致编译失败——Core 只用到 `StorageServices`；若 HttpService 编译报错，按需移除 HttpService 目录（保留 StorageServices）。构建会暴露该点。
