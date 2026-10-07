# 构建 KiwiTraffic

## 环境要求

| 工具 | 版本 | 说明 |
| --- | --- | --- |
| Windows | 10 / 11，x64 | WPF 仅限 Windows |
| .NET SDK | 10.0.4xx | 由 `global.json` 锁定 |
| PowerShell | 7（`pwsh`） | 构建脚本要求 PS 7 |
| Node.js | 18+ | **可选**，只是命令薄封装 |

`scripts/build.ps1` 不能用 Windows PowerShell 5.1 跑，请用 `pwsh`。

## 命令

```powershell
# 日常验证用的快速构建 -> dist/fast/KiwiTraffic.exe
npm run build:exe

# 完整质量门禁 + 正式 Release 构建 -> dist/release/KiwiTraffic.exe
npm run build:release

# 只跑测试
dotnet test

# 清理所有 bin/obj 与整个 dist/
npm run clean
```

不装 Node.js 时等价写法：

```powershell
pwsh -File scripts/build.ps1 -Configuration fast
pwsh -File scripts/build.ps1 -Configuration release
pwsh -File scripts/build.ps1 -Configuration fast -Clean
```

## 两档的区别

| | `fast` | `release` |
| --- | --- | --- |
| restore | 是 | 是 |
| 构建 | 由 `publish` 完成 | 整解决方案构建，**警告视为错误** |
| 测试 | 跳过 | 对整解决方案跑 `dotnet test` |
| ReadyToRun | 关 | **开**（构建更慢，启动更快） |
| 产物 | `dist/fast/KiwiTraffic.exe` | `dist/release/KiwiTraffic.exe` |

其余发布参数两档一致：`win-x64`、自包含、单文件。`SelfContained`、
`PublishSingleFile`、`RuntimeIdentifier`、`IncludeNativeLibrariesForSelfExtract`
都写在 `src/KiwiTraffic.App/*.csproj` 里而不是命令行上，这样任何调用方式都不会
意外产出依赖运行时或多文件的产物。

脚本结束时会打印产物路径、版本、体积与 SHA-256。

## 产物约定

- 交付物是**一个 EXE**，没有安装程序。永不产出 MSI / MSIX / NSIS —— 不要添加打包配置。
- 「单文件」不等于「完全不落盘」：.NET 单文件宿主可能解压内部原生组件，配置与凭据
  写在 `%LOCALAPPDATA%\KiwiTraffic\`。
- **不启用** WPF trimming 与 Native AOT。它们对本应用未经验证，且已知会破坏 WPF 的
  反射路径。不要为了减小体积而擅自打开后又不做完整人工验证。

## 依赖锁文件

每个项目都提交 `packages.lock.json`（由 `Directory.Build.props` 里的
`RestorePackagesWithLockFile` 生成）。改动任何 `PackageReference` 后重新生成：

```powershell
dotnet restore --force-evaluate
```

并把更新后的 `packages.lock.json` 与项目改动一起提交。

## 版本号

`Directory.Build.props` 中的 `<Version>` 是唯一版本来源。`scripts/build.ps1` 读取它
用于构建报告，release workflow 在 tag 与它不一致时直接失败。

## 发版流程

Release 一律云端构建，不手工上传：

1. 确认 `main` 已推送。
2. 更新 `CHANGELOG.md`（把 `Unreleased` 条目移到新版本下并写上日期），必要时同步
   `Directory.Build.props` 里的 `<Version>`。
3. 提交后打 tag 并推送：

   ```powershell
   git tag -a v0.1.0 -m "v0.1.0"
   git push github main
   git push github v0.1.0
   git push gitee main
   git push gitee v0.1.0
   ```

4. tag 推送触发 `.github/workflows/release.yml`，在 `windows-latest` 上校验 tag、
   跑 `scripts/build.ps1 -Configuration release`，把 `dist/release/KiwiTraffic.exe`
   作为附件发布到 GitHub Release 并自动生成 Release Notes。

GitHub 是唯一的 release 渠道；Gitee 只推代码和 tag，不在那边建 Release 页面。

## 常见问题

- **找不到 dotnet 或 SDK 版本不对** —— 用 `dotnet --list-sdks` 检查，必须满足
  `global.json`；到 <https://dotnet.microsoft.com/download/dotnet/10.0> 安装 .NET 10。
- **NuGet 超时** —— 先确认这台机器是否需要代理。这里**故意不配**国内 NuGet 镜像；
  检查 `dotnet nuget list source` 指向 `https://api.nuget.org/v3/index.json` 且代理可达。
- **`RestoreLockedMode` 失败** —— 改了 `PackageReference` 却没重新生成锁文件，见上文。
