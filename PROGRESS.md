# 进度

> 计划图纸见 `docs/kiwivm-traffic-widget-plan.md`；API 契约核实结果见 `docs/api-contract.md`。
> 本文件记录进度、待办与一次性排查过程，不重复展开需求。

## 当前状态

**阶段：M1 基本完成，M0 调研进行中**

| 里程碑 | 状态 |
| --- | --- |
| M0 核实 API 与运行环境 | **完成**：契约落 `docs/api-contract.md`；单位口径已用真实账号定案；倍率口径对当前账号不适用 |
| M1 骨架与纯计算模块 | 完成 |
| M2 凭据与真实 API 适配 | **真实账号已连通并读到数据**；仅剩「记录采样时间差」一项待补 |
| M3 悬浮窗与托盘 | 代码完成，待用户人工验收（拖动/置顶/托盘菜单/多屏） |
| M4 刷新与提醒 | 未开始 |
| M5 验收与绿色产物 | 未开始 |

> M2 的完成条件包含「真实账号数据与面板完成首次对照」。2026-10-07 用 VEID 2213202
> 完成了首次对照，并据此修正了单位口径；还剩记录采样时间差一项。

## M1 已完成内容

- 解决方案 `KiwiTraffic.slnx`（.NET 10 SDK 的 `dotnet new sln` 默认产出 `.slnx`）与 5 个项目：
  `KiwiTraffic.App`（WPF+WinForms，`net10.0-windows`）、`KiwiTraffic.Core`（纯领域，`net10.0`）、
  `KiwiTraffic.Infrastructure`（`net10.0-windows`）、两个 xUnit 测试项目。
- Core：`UsageCalculator`（已用/额度 → 百分比、剩余、进度条值）、`TrafficUsage`、
  `TrafficSnapshot`、`ByteSizeFormatter`（SI/IEC 前缀，不混标）。
- App：M1 预览窗口，显示**明确标识的模拟数据**（顶部黄色「模拟数据 · SIMULATED」横幅），
  文案与单位换算全部经 Core 计算与格式化，UI 不碰原始字段。
- 构建：`Directory.Build.props`（版本唯一来源 + Release 警告即错误）、`.editorconfig`、
  `scripts/build.ps1`（fast / release 两档）、`.github/workflows/release.yml`（tag 触发）。
- 测试：Core 36 个用例全部通过，覆盖计划第 11 节矩阵中属于纯计算的各行。

### 计划第 11 节矩阵的 M1 覆盖情况

| 矩阵行 | 覆盖 |
| --- | --- |
| 已用 250、总量 1000 → 25.0%、剩余 750 | ✅ `UsageCalculatorTests` |
| 已用超过总量 → 文字 >100%、进度条封顶、剩余 0 | ✅ 含刚好超过与大幅超过两种 |
| 额度为零／缺失／负值 → 未知，无除零、无假 0% | ✅ `TheoryData` 三个取值 |
| 计数负值或溢出 → 明确异常，不崩溃 | ✅ 负值抛异常；比值溢出返回失败而非崩溃 |
| 大计数器与倍率 → 精度与单位正确、不重复乘倍率 | 部分：精度与单位已测；**倍率待 M0 契约** |
| 其余各行（HTTP、限频、调度、提醒、DPI、托盘等） | 属 M2–M5，未开始 |

## M2 已完成内容

**存储层**（`KiwiTraffic.Infrastructure`）

- `AtomicFile`：同目录临时文件 → 落盘 → 一步替换；写 UTF-8 无 BOM，读时剥离 BOM。
- `JsonSettingsStore` / `JsonUsageCache`：schemaVersion + 显式迁移点；未知属性视为损坏
  （手改配置时拼错的键不会被静默丢弃）；损坏文件先备份成 `<名>.corrupt-<时间戳>` 再报错。
- `DpapiCredentialStore`：DPAPI `CurrentUser` + 应用级附加熵；解密失败单独返回
  `Undecryptable`（换机器/换用户），不伪装成「未配置」。

**凭据隔离**（本轮的额外加固）

`StoredCredentials` 增加了 `ApiKeyProfileId`：密钥标记它属于哪台 VPS。切换 VPS 后旧密钥
**不再可用**，而是引导重新输入 —— 否则新 VEID 配旧密钥会得到一条令人困惑的认证失败，
而不是「你换了机器」这个明显结论。代理密码是机器级的，特意不绑定 VPS。

**API 层**

- 宽松 JSON 转换器：兼容已核实的「同一字段可能是数字或数字字符串」，非法值抛异常而**不退化为 0**。
  注意两处的未知字段策略**故意相反**：API 响应用 `Skip`（真实响应有约四十个字段），
  本地配置文件用 `Disallow`（拼错的键应当报错）。
- `UsageMapper`：唯一处理单位与倍率的地方。任何无法从契约确认的情况一律返回失败，
  失败**绝不产出快照**，因此旧数据不会被半成品覆盖。
- `MultiplierPolicy`：倍率口径未决，收敛为可切换策略，默认 `ScaleBoth`（官方文档口径）。
- `KiwiVmClient`：固定 HTTPS 端点（不接受自定义 Base URL）、POST 表单传参（密钥不进 URL）、
  不跟随重定向（否则密钥会被重放到跳转目标）、区分超时与用户取消、`error != 0` 不算成功。
- `KiwiVmHttpClientFactory`：保留证书校验；三种代理模式；代理密码不写进代理 URL。

**并发与切换**

- `UsageSession`：同一时刻最多一个请求（并发调用共享同一个在途请求，而不是排队发第二个）；
  切换配置会取消旧请求，且**即使旧响应仍然返回也会被丢弃**，不会写进缓存。

**界面**

- 设置窗口（首次配置与后续编辑同一窗口）：别名 / VEID / API Key（默认遮蔽，可临时显示）/
  记住密钥 / 代理模式与手动代理；「测试连接」用屏幕上未保存的值查询，失败不影响已生效配置；
  「取消」保留原状态。
- 主窗口改为显示**真实数据**；失败时保留上一次的数字并在旁边说明原因，绝不用 0 覆盖。
  M1 的模拟数据横幅随之移除。
- 启动流程：读配置 → 未配置则进入首次配置 → 配置会话 → 显示窗口 → 先显示缓存再请求。
- 保存顺序：**先存凭据、再存配置**。若第二步失败，磁盘上的配置仍然描述着密钥所属的那台 VPS，
  于是程序会要求重新输入，而不是把新 VEID 与旧密钥配在一起。

## M3 已完成内容

**悬浮窗外观**

- 无边框圆角卡片 + 柔和投影；`AllowsTransparency` + 自绘控件，不引入任何 UI 库。
- 两种指示样式（设置里可选，默认**圆环**）：`RingProgress` 与 `BarProgress` 都是自绘
  `FrameworkElement`，各约 80/60 行。不引入图表库是计划明确要求的。
- 颜色只是装饰：三档（<80 / 80–<90 / ≥90）来自 Core 的 `UsageLevelClassifier`，
  但百分比数字与文字始终在，颜色不是唯一的信息载体。
- 大百分比在圆环中心，环本身按百分比着色，读起来比默认 `ProgressBar` 干净得多。

**窗口行为**

- 拖动任意位置移动窗口（无边框窗口没有标题栏，整个卡片就是标题栏）。
- 位置按 DIP 保存；恢复时把**右下角**对齐后再夹取，所以内容高度变化时窗口不会跳动。
- 计划第 4.2 节要求的所有情形都有对应处置，逻辑在 Core 的
  `WindowPlacementPolicy` 里，纯函数、有测试：
  - 显示器被拔掉 → 夹回可见区
  - 分辨率变化 → 同上
  - 窗口跑到桌面顶边之外 → 推回来（无边框窗口没有标题栏可抓，跑上去就再也拿不回来）
- 右侧托盘菜单：显示/隐藏、刷新、置顶、设置、恢复窗口位置、退出。
- 关闭窗口 = 隐藏到托盘（首次会气泡提示），真正退出在托盘菜单里。
- 单实例：第二次启动会唤醒已在运行的窗口后自行退出。

## 首次真实账号联调（2026-10-07）

配置：VEID 2213202，套餐 `KVMV5-20G-1G-1T-CA-CN2GIA`，代理「系统默认」。
结果：**连接成功**，`getServiceInfo` 一次通过，程序显示 `已用 16.0 GB / 1.1 TB（1.5%）`。

这一条数据同时定案了两件事：

### 1 倍率口径对本账号不存在

该账号的倍率是 **1**，两种口径（`ScaleBoth` / `ScaleQuotaOnly`）算出的结果完全一致。
计划里那条「真实账号数据与面板完成首次对照」中的倍率部分因此对本账号不适用；
通用结论仍需一台倍率 ≠ 1 的机器，见 `docs/api-contract.md` 第 8 节。

### 2 单位口径定案：KiwiVM 用 1024 进制除以却标 TB/GB

**程序显示 1.1 TB，面板显示 1 TB。** 同一个字节数在两种除数下分别得到 1.1 与 1.0，
说明 KiwiVM 按 **1024** 除，却把结果标成 TB/GB。

处置：程序也按 1024 除（数字与面板一致），但标签用 **TiB / GiB** ——
把二进制倍数标成 GB 正是计划明令禁止的误标。改完显示为 `1.0 TiB`。
实现收敛到 `KiwiTraffic.App.DisplayFormat.BytePrefix` 一处常量（此前散在两处，已合并）。
百分比是比值，不受影响。

> 该结论对 `plan_monthly_data` 究竟是 `2^40` 还是十进制 `1.1e12` 两种可能都成立
> （两者按 1024 除后都是 1.0 TiB），所以不需要再区分。

### 顺带确认的事实

- API Key 前缀与"全权限凭证"的描述一致；「系统默认代理」在本机（`127.0.0.1:10808`）
  能正常访问 `api.64clouds.com`，无需手动配置代理。
- 设置窗口的「测试连接」按预期工作（用未保存的值查询）。
- 输入框里的别名被填成了 `snappy-baud-1 [KVMV5-20G-1G-1T-CA-CN2GIA]`（用户自取）。

## 实测数据（2026-10-07，本机）

| 项 | 数值 |
| --- | --- |
| fast 产物 | `dist/fast/KiwiTraffic.exe`，165 MB（无 ReadyToRun） |
| release 产物 | `dist/release/KiwiTraffic.exe`，181.7 MB（ReadyToRun 开启） |
| 启动到窗口出现 | fast 851 ms；release 1509 ms（均为首次运行，含单文件原生组件解压） |
| 工作集 | fast 150.3 MB；release 147 MB（空闲预览窗口） |
| 测试 | Core 75 + Infrastructure 81 = **156 个，全部通过**；Release 构建 0 警告 0 错误 |
| 产物目录 | `dist/<档>/` 下只有一个 EXE，调试符号在 `symbols/` 子目录 |
| 首次启动行为 | 无配置时显示设置窗口，关闭后以 0 码退出；数据目录 `%LOCALAPPDATA%\KiwiTraffic\` 被建立 |

体积偏大属自包含单文件 WPF 的正常水平（未启用 trimming/AOT，见 `BUILD.md` 的说明）。

## 待办

- [x] M0：API 契约核实 → `docs/api-contract.md`
- [x] M2：`IKiwiVmClient`、`UsageMapper`（含可切换的倍率策略）、宽松 JSON 转换器、
  DPAPI 凭据、配置/缓存存储、连接测试、配置切换取消
- [x] **M2 联调：真实账号连通**（2026-10-07，VEID 2213202）—— 连接成功并读到真实数据，
  据此定案了单位口径，详见下方「首次真实账号联调」
- [ ] 联调收尾：把程序显示的百分比与重置时间同面板再对一次（记录采样时间差），
  这是计划第 11 节矩阵最后一行；倍率一项对该账号已不适用（系数为 1）
- [x] M3：悬浮窗（拖动/置顶/位置记忆）、托盘、单实例、环形/进度条两种样式
- [ ] **M3 人工验收（用户）**：拖动、置顶切换、托盘菜单各一项、关闭后从托盘恢复、
  位置在重启后保持、多显示器下窗口不丢
- [ ] M4：刷新协调器（自动刷新/节流/退避/限频冷却）、提醒去重、开机启动
- [ ] M5：验收、文档、末次实测
- [ ] 代理密码与开机启动目前只有存储与模型，尚无界面（代理密码已在设置窗口，
  开机启动待 M4）
- [ ] 依赖计划：`Microsoft.Extensions.*` 仅在有实际需要时再引入；当前唯一外部依赖是
  `System.Security.Cryptography.ProtectedData`（DPAPI 不在 WindowsDesktop 共享框架里）

## 关键决策与理由

### 目标框架：net10.0-windows（而非 net8.0-windows）

计划要求「实施时仍受官方支持的 .NET LTS」。2026-10-07 查官方 release metadata：

| 通道 | SDK | 类型 | 支持阶段 | EOL |
| --- | --- | --- | --- | --- |
| 10.0 | 10.0.401 | LTS | active | 2028-11-14 |
| 9.0 | 9.0.318 | STS | maintenance | 2026-11-10 |
| 8.0 | 8.0.425 | LTS | maintenance | 2026-11-10 |

本机原本只装了 8.0.425，其 EOL 距今约一个月，故装 .NET 10 SDK 并以其为目标。

### 其余已定参数

- 应用与仓库名 `KiwiTraffic`；数据目录 `%LOCALAPPDATA%\KiwiTraffic\`
- 远端仓库：**暂不创建**，先本地推进；日后创建时 Gitee + GitHub 双远端
  （`gitee` / `github`），可见性 public
- 发 release：默认不发
- `KiwiTraffic.Core` 保持 `net10.0`（无平台依赖），便于测试且防止 Windows API 渗入领域层

## 环境实测记录（2026-10-07）

| 项 | 结果 |
| --- | --- |
| .NET SDK | 原 8.0.425 → winget 安装 10.0.401（需 UAC 授权） |
| PowerShell | pwsh 7.6.6 (Core) |
| Node / npm | v24.21.0 / 11.19.0 |
| git / gh | 2.55.0.windows.5 / 2.101.0 |
| Gitee token | `~/.gitee_token` 存在 |
| NuGet 源 | 仅 nuget.org；直连 0.37s（**无需国内镜像**，符合计划第 9 节） |
| 系统代理 | `127.0.0.1:10808`，ProxyEnable=1（.NET 默认代理解析会走它） |
| 当前 shell | **非管理员**（机器级安装会弹 UAC） |

## 已知坑（排查过程）

### 1. `ProgressBar.Value` 默认 TwoWay，绑只读属性会在运行期崩溃

预览窗口首次启动即退出（exit code `-532462766` / `0xE0434352`）。WinExe 没有控制台，
异常只出现在事件日志里：

```
System.InvalidOperationException: 无法对「WidgetPreviewViewModel」类型的只读属性
「ProgressValue」进行 TwoWay 或 OneWayToSource 绑定。
```

`ProgressBar.Value` 的依赖属性声明为 `BindsTwoWayByDefault`，而 VM 属性只有 getter。
**编译期完全无感，只有运行才炸。** 处置：绑定上显式写 `Mode=OneWay`；并给 `App`
加了 `DispatcherUnhandledException` 处理器，弹框提示后以非零码退出，避免以后再靠翻事件日志排查。

> 教训：WPF 绑定错误是运行期的。今后新增绑定后要真正启动一次程序，不能只看编译通过。

### 2. Release 档「警告即错误」拦下的规范问题

- **CA1720**：枚举成员 `BytePrefixStyle.Decimal` 与类型名 `decimal` 冲突 → 改名
  `Si` / `Iec`（也更准确：1000 进制是 SI，1024 进制是 IEC）。
- **CA1707**：测试方法名里的下划线 → 在 `.editorconfig` 中仅对 `tests/**/*.cs` 关闭，
  生产代码仍受约束。

### 3. WinForms 与 WPF 的隐式 using 冲突

`UseWindowsForms=true` 会注入 `global using System.Windows.Forms;`，与 WPF 的
`System.Windows.Application` 撞名（`error CS0104`）。处置：在 App 的 csproj 中
`<Using Remove="System.Windows.Forms" />`，M3 用到托盘时再显式限定或加别名。

### 4. 构建脚本读 XML 的方式

`Set-StrictMode -Version Latest` 下用 `$xml.Project.PropertyGroup.Version` 会抛
「找不到属性 Version」——`Directory.Build.props` 里有多个 `PropertyGroup`，其中几个没有该子元素。
改用 XPath：`$xml.SelectSingleNode('/Project/PropertyGroup/Version')`。

### 5. xUnit 的 `[InlineData]` 不能传 decimal 字面量

`[InlineData(0)]` 会被当成 `int`，无法转成 `decimal?`（`ArgumentException`）。
改用 `TheoryData<decimal?>` + `[MemberData]`。同理，`double?` 参数要写 `0.0` 而不是 `0`。

### 6. XAML 注释里不能出现 `--`

用 `<!-- 连接 --... -->` 这类横线分隔的注释会直接编译失败：
`MC3000: An XML comment cannot contain '--'`。XAML 就是 XML。

### 7. 公开的窗口构造函数不能接收 internal 参数类型

XAML 生成的窗口类是 public，`public MainWindow(AppServices ...)` 里若 `AppServices` 是
internal 会报 `CS0051`。处置：把 `AppServices` 也设为 public（本程序集里视图模型本来就是 public）。

### 8. `ProtectedData` 不在 WindowsDesktop 共享框架里

即使目标框架是 `net10.0-windows`，也必须显式引用 NuGet 包
`System.Security.Cryptography.ProtectedData`（当前 10.0.12）。这是本项目目前唯一的外部依赖。

### 9. CA1001 对 `Application` 是误报

`App` 持有可释放字段就要求类型本身可释放，而 `Application` 无法实现 `IDisposable`。
已就地 `[SuppressMessage]` 并写明理由（字段在 `OnExit` 中释放）。

### 10. 枚举值会被持久化，不能随手调序

`IndicatorStyle` 第一版把 `Bar` 声明在 `Ring` 前面（Bar=0），而设置界面的下拉项顺序是
「圆环、进度条」（索引 0 = 圆环）。结果读回配置时圆环会被显示成进度条。
`ProxyMode` 恰好顺序一致所以没暴露。

处置：枚举显式写出数值（`Ring = 0, Bar = 1`），顺序与下拉项一致，并加测试把数值钉死
——它们既进配置文件又被当作下拉索引。**新增持久化枚举时先想清楚这一点。**

### 11. LibraryImport 要求整个程序集开 unsafe

`[LibraryImport]` 生成的代码需要 `AllowUnsafeBlocks`，为两个 P/Invoke 放开整个程序集不值得。
改成不需要 P/Invoke 的实现：

- 单实例通知：命名 `EventWaitHandle` + `RegisterWaitForSingleObject`，比广播窗口消息更可靠
  （不会被消息过滤吞掉），也不用手工传窗口句柄。
- 托盘图标：在内存里拼一个 PNG-in-ICO 容器交给 `new Icon(stream)`，绕开了
  `Bitmap.GetHicon` + `DestroyIcon` 的 GDI 句柄管理。

于是 App 项目现在**零 P/Invoke、零 unsafe**。

### 12. CA1001 与 tray 的归属

托盘图标一开始放在 `MainWindow` 里，分析器要求窗口实现 `IDisposable`（它不能）。
把托盘改为由 `App` 持有并在 `OnExit` 释放 —— 语义上也更对：隐藏窗口时图标必须还在，
它的生命周期属于整个进程而不是某个窗口。顺带少了一处抑制。

### 13. 默认位置用了虚拟桌面而不是主显示器

首次显示时若按虚拟桌面右下角放置，在双屏（副屏在右侧）布局下会落到**副屏**上，
与计划「主显示器工作区右下角」不符。实测确认：窗口出现在 x=3504 DIP（约物理 8760，
属于 DISPLAY2）。处置：默认位置用 `SystemParameters.WorkArea`（主显示器工作区），
虚拟桌面只用于「别跑到所有屏幕之外」的夹取。

> 已知近似：混合 DPI 下，用单一的 DIP 虚拟矩形做夹取并不精确地等于「某个屏幕的工作区」。
> 托盘里的「恢复窗口位置」是计划规定的补救手段。真实的多显示器逐屏处理留给后续需要时再做。

### 14. 我自己写错过一次倍率推导（已修正）

初版 `docs/api-contract.md` 第 2 节写成「两个倍率口径在百分比上等价、只有剩余量不同」。
实际上：口径 A（两者都乘）与「完全不乘」在百分比上等价，而口径 B（只乘额度）会得到
`c/(pm×m)`，与 A 相差 `1/m` 倍。**配额在 A、B 下都是 `pm×m`，所以不能用配额来区分二者**，
只能看面板显示的「已用」是 `c` 还是 `c×m`。文档第 2、8 节已改正。

> 教训：这类"看起来显然"的代数结论要落到纸面上验算，尤其当它会决定实现方式时。

## M0：API 契约核实结果（2026-10-07）

完整结果见 **`docs/api-contract.md`**（唯一展开处，含可信度标注与来源）。摘要：

**已核实**（官方文档经多源转录一致，或官方原文）：

- `plan_monthly_data`、`data_counter` 单位都是**字节**；`data_next_reset` 是 **Unix 秒级时间戳**
- 套餐名字段是 `plan`，**不存在 `plan_max_data`**
- 成功判定：响应含 `error` 字段，**`0` 为成功**；非 0 看 `message`
  （`700005` = 认证失败，多源确认；完整错误码表**未找到**）
- `getRateLimitStatus` 存在，返回 `remaining_points_15min` / `remaining_points_24h`；
  **点数上限未找到**
- base URL 只有 `https://api.64clouds.com/v1/`；`v1.1` **无任何证据**
- API Key 是**每台 VPS 一把的全权限凭证**，**无只读密钥**（否定性结论）
- `getLiveServiceInfo` 与 `getServiceInfo` 同在 `/v1/`，前者多返回 VM 运行时状态且**最长耗时 15 秒**

**关键未决：`monthly_data_multiplier` 的口径**（`docs/api-contract.md` 第 2 节）。
官方文档说 `data_counter` 与 `plan_monthly_data` **两者都乘**；但社区实测现象是倍率作为
**按机房的配额系数**（CN2 GT 为 0.33x，配额缩到 1/3），此时已用量不应再乘。两者在"百分比"上
等价、在"剩余量"上不同。**未找到任何倍率 ≠ 1 的真实响应样本**，公开资料无法定案。
处置：默认按官方文档口径实现，但把倍率策略收敛在 `UsageMapper` 一处、保持可切换，
**在真实账号对照前不得宣称倍率已验证**。

**实现风险提醒**：KiwiVM 响应的字段类型不稳定（同一字段可能是 JSON number 或字符串数字），
M2 必须用宽松的自定义 `JsonConverter`，解析失败判为无效响应，**不得静默退化为 0**。

## 需要用户本机实测才能定案的事项

见 `docs/api-contract.md` 第 8 节。共四项：倍率口径、面板单位是 SI 还是 IEC、
百分比/重置时间与面板的一致性（含采样时间差）、重置时刻的时区。均需用户在程序里
自行输入 VEID/API Key 完成，**密钥不需要发到聊天中**。
