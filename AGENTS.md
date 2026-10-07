# AGENTS.md — KiwiTraffic 项目约定

面向所有接手本仓库的 Coding Agent。**每次会话先读本文件。**

> 重大功能开发前先阅读 `docs/kiwivm-traffic-widget-plan.md`（开工图纸：需求、选型理由、里程碑 M0–M5、验收矩阵）。需求与验收细节一律不在本文件展开。

## 项目是什么

Windows 桌面常驻小工具：显示单台搬瓦工（KiwiVM）VPS 当前计费周期的流量已用百分比、
剩余量与下次重置时间。交付绿色单文件 EXE，**无安装器**。

## 技术栈（不要擅自更换）

| 项 | 选择 |
| --- | --- |
| 语言 / 界面 | C# + WPF，`net10.0-windows` |
| SDK | .NET SDK 10.0.4xx，由 `global.json` 锁定（`rollForward: latestFeature`） |
| 托盘 | Windows Forms 的 `NotifyIcon`（App 项目同时开 `UseWPF` + `UseWindowsForms`） |
| JSON | `System.Text.Json`，逐字段显式校验 |
| HTTP | `HttpClient`，异步 + 取消 + 可控代理 |
| 凭据 | Windows DPAPI，`CurrentUser` 范围 |
| 测试 | xUnit |
| 交付 | `win-x64` 自包含单文件绿色 EXE |

理由见计划文档第 2 节。不要换成 Electron / Tauri / 网页应用，不要引入 DI 框架、
MVVM 框架或数据库。

## 目录结构

```
KiwiTraffic.slnx                  解决方案（.NET 10 的 dotnet new sln 默认产出 .slnx）
Directory.Build.props             版本唯一来源；Release 档打开「警告即错误」
.editorconfig                     代码风格与按目录的规则关闭（见「踩坑」）
src/KiwiTraffic.App/              WPF 窗口、ViewModel、托盘、单实例、生命周期
src/KiwiTraffic.Core/             领域模型、计算、状态、调度与通知规则（net10.0，无平台依赖）
src/KiwiTraffic.Infrastructure/   HTTP、API 映射、DPAPI、配置、缓存、代理
tests/KiwiTraffic.Core.Tests/
tests/KiwiTraffic.Infrastructure.Tests/
docs/                             计划文档、API 契约
scripts/build.ps1                 唯一的构建入口
dist/<fast|release>/              产物，已 gitignore；目录里只应有一个 EXE（符号在 symbols/）
```

## 命令

```powershell
npm run build:exe        # fast 档 -> dist/fast/KiwiTraffic.exe（日常验证）
npm run build:release    # release 档：warnings-as-errors + 测试 + ReadyToRun
dotnet test              # 只跑测试
```

不装 Node.js 时用 `pwsh -File scripts/build.ps1 -Configuration fast|release`。
shell 用 **pwsh 7**，不要用 Windows PowerShell 5.1。

## 硬性禁令

- **不产出安装包**：不生成 MSI / MSIX / NSIS / dmg / deb / rpm，不写打包器配置。
- **不启用 WPF trimming 或 Native AOT**（未经验证，会破坏 WPF 反射路径）。
- **不改用户机器配置**（NUKE 全局工具、环境变量、镜像源）而不先告知用户。
- **不把国内镜像写进仓库**；境外 CI 用默认源。
- **密钥零容忍**：API Key / 代理密码不得出现在源码、测试样本、日志、异常消息、
  命令行参数、URL 查询串或提交历史里。日志不记录请求体与完整原始响应。
- **不猜测计费口径**：单位、倍率、周期语义必须来自 `docs/api-contract.md` 中已核实的
  官方说明。未核实的字段一律不做实现假设，标为「待核实」。
- **不得把倍率口径写死**：`monthly_data_multiplier` 的用法存在冲突（官方文档说两者都乘，
  社区实测现象是它作为按机房的配额系数）——见 `docs/api-contract.md` 第 2 节。
  必须收敛在 `UsageMapper` 一处并保持可切换，在真实账号对照前不得宣称已验证。
- **API 成功判定只看 `error == 0`**，不靠错误码白名单；响应字段类型不稳定
  （同一字段可能是数字或字符串数字），必须用宽松转换器，解析失败判为无效响应，
  **不得静默退化为 0**。
- **不对 VPS 执行任何管理操作**，不为测试错误密钥/限频/周期重置而高频请求真实 API。
- **不把未验证的东西写成已完成**：README、CHANGELOG、PROGRESS 不得把模拟数据通过
  说成真实接口验收通过。

## 编码约定

- 所有单位与倍率换算只发生在 `UsageMapper`；UI 不得直接读原始字段。
- **未知 ≠ 0**：缺失/零/负值用显式状态表达，不得用 `0` 兼任「未知」。
- 失败或无效的响应**绝不覆盖**最近一次有效快照。
- 百分比照实显示（可 > 100 %），只让进度条封顶；阈值判断用未舍入值。
- 文本文件按 UTF-8 无 BOM 原子写入（临时文件 + 替换）；读取时容忍 BOM。
- 配置 / 缓存 / 提醒记录都带 `schemaVersion`，升级走显式迁移。
- 时间内部用 `DateTimeOffset` UTC，展示用当前用户时区并标明偏移。
- 编辑文本文件用 agent 的 edit/write 工具，不要用 PowerShell `Set-Content` / `Out-File` / `>`。
- 长任务（构建、测试、依赖下载、大范围检索）后台执行，只把结论带回上下文。

## 踩坑（不读就会重犯）

- **WPF 绑定错误只在运行期暴露。** 改完界面必须真正启动一次程序，不能只看编译通过。
  `ProgressBar.Value` 之类的依赖属性默认 `BindsTwoWayByDefault`，绑到只读属性上会在
  运行时抛 `InvalidOperationException`；WinExe 没有控制台，表现为静默退出。
  绑定只读属性时显式写 `Mode=OneWay`。
- **不要删 App csproj 里的 `<Using Remove="System.Windows.Forms" />` 与 `System.Drawing`。**
  `UseWindowsForms=true` 会注入 `global using System.Windows.Forms;`，与 WPF 的
  `System.Windows.Application` 撞名。M3 用托盘时显式限定 `System.Windows.Forms.NotifyIcon`
  或加别名。
- **Release 档是「警告即错误」**，分析器为 `latest-recommended`。本地日常用 `fast` 档跑得快，
  但提交前应至少跑过一次 `build:release`。`.editorconfig` 中对 `tests/**/*.cs` 关闭了
  CA1707（测试方法名下划线属惯例），生产代码仍受约束。
- **xUnit 的 `[InlineData]` 不能传 decimal 字面量**（会被当成 int），用 `TheoryData<T>` +
  `[MemberData]`。
- **`Set-StrictMode` 下不要用属性链读 XML**（`.Project.PropertyGroup.Version` 会在缺少该子元素
  的节点上抛错），用 `SelectSingleNode` / XPath。
- **自包含单文件 WPF 产物约 165 MB 属正常**，不是构建配置出错。不要为此擅自打开
  trimming 或 Native AOT。
- **XAML 就是 XML：注释里不能出现 `--`。** 用横线做分隔的注释会以 `MC3000` 编译失败。
- **XAML 生成的窗口类是 public**，所以 `public MainWindow(AppServices …)` 这类构造函数的
  参数类型也必须是 public，否则 `CS0051`。
- **未知字段策略在两种文件上故意相反**：KiwiVM API 响应用 `JsonUnmappedMemberHandling.Skip`
  （真实响应有约四十个字段），本地 settings/cache 用 `Disallow`（手改配置拼错的键必须报错）。
  改任何一处前先想清楚是哪一类。
- **`ProtectedData` 需要 NuGet 包 `System.Security.Cryptography.ProtectedData`**，
  它在 WindowsDesktop 共享框架里没有。这是目前唯一的外部依赖，不要顺手加别的。
- **凭据必须绑定 VPS**：`StoredCredentials.ApiKeyProfileId` 是防止「新 VEID 配旧密钥」的
  关键，任何新增的密钥类字段都要想清楚它的作用域（机器级还是 VPS 级）。
- **写进配置文件的枚举，数值就是格式**：`IndicatorStyle` / `ProxyMode` 的值会序列化进
  settings.json，还被当作设置界面下拉项的索引，**不能重新排序**。新增这类枚举时显式写数值
  并加一条钉死数值的测试。
- **App 项目保持零 P/Invoke、零 unsafe。** `[LibraryImport]` 会要求整个程序集开
  `AllowUnsafeBlocks`，不值得。单实例用命名 `EventWaitHandle`，托盘图标在内存里拼
   PNG-in-ICO。加新的原生调用前先找托管替代方案。
- **托盘图标由 `App` 持有**，不是 `MainWindow`：隐藏窗口时图标必须还在，它的生命周期属于
  整个进程。（窗口持有可释放字段会触发 CA1001，而 `Window` 不该实现 `IDisposable`。）
- **颜色一律走 `DynamicResource`**，键定义在 `Themes/Light.xaml` 与 `Themes/Dark.xaml` 里，
  两份文件的键必须完全一致（整份字典被替换，缺键就是一片空白）。控件样式在
  `Themes/Controls.xaml`，由 `ThemeManager.Apply` 换字典，不重建窗口。
- **应用图标是生成的**：改图标外观要改 `scripts/make-app-icon.ps1` 再跑一次，
  不要直接手改 `Assets/app.ico`。它同时是 EXE 图标和两个窗口的图标来源。
- **流量单位是 1024 进制、在 1000 处进位、标签用 TiB/GiB**：KiwiVM 面板把 1000 GiB
  写作 "1 TB"，我们按同样方式除但用诚实的单位名。不要改回十进制，也不要为了
  "和面板一模一样"而把 TiB 标成 TB —— 见 `docs/api-contract.md` 第 8 节。
- **位图/图标不手绘进仓库**：托盘图标在运行时画（还按用量变色），
  应用图标由脚本生成，两者都不依赖设计稿。
- **默认窗口位置用主显示器工作区**（`SystemParameters.WorkArea`），虚拟桌面只用于
  「别跑到所有屏幕外」的夹取。双屏（副屏在右）时按虚拟桌面右下角放置会落到副屏。

## 记录文件分工

| 文件 | 内容 | 语言 |
| --- | --- | --- |
| `AGENTS.md` | 本文件，长期有效的规则 | 中文 |
| `PROGRESS.md` | 进度、待办、一次性排查过程与实测数据 | 中文 |
| `CHANGELOG.md` | 影响用户的改动，记入 Unreleased | 英文 |
| `docs/api-contract.md` | API 字段与错误契约的**唯一**展开处 | 中文 |
| `README*.md` / `BUILD*.md` | 功能用法 / 构建发版流程 | 英文 + 中文 |

同一事实只在一处展开，其他处写一行指针。

## 交接阅读顺序

1. `AGENTS.md`（本文件）
2. `PROGRESS.md` — 当前状态与待办
3. `docs/kiwivm-traffic-widget-plan.md` — 开工图纸
4. `docs/api-contract.md` — API 契约核实结果（若存在）
5. `README.md` / `BUILD.md`
