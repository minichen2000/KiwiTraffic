# 进度

> 计划图纸见 `docs/kiwivm-traffic-widget-plan.md`；API 契约核实结果见 `docs/api-contract.md`。
> 本文件记录进度、待办与一次性排查过程，不重复展开需求。

## 当前状态

**阶段：M1 基本完成，M0 调研进行中**

| 里程碑 | 状态 |
| --- | --- |
| M0 核实 API 与运行环境 | 环境完成；API 契约已核实并落到 `docs/api-contract.md`，**倍率口径未决**（见下） |
| M1 骨架与纯计算模块 | 骨架、纯计算、模拟预览界面、构建脚本均已完成并验证 |
| M2 凭据与真实 API 适配 | 未开始（契约已足够开工，倍率按可切换策略实现） |
| M3 悬浮窗与托盘 | 未开始 |
| M4 刷新与提醒 | 未开始 |
| M5 验收与绿色产物 | 未开始 |

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

## 实测数据（2026-10-07，本机）

| 项 | 数值 |
| --- | --- |
| fast 产物 | `dist/fast/KiwiTraffic.exe`，165 MB（无 ReadyToRun） |
| release 产物 | `dist/release/KiwiTraffic.exe`，181.7 MB（ReadyToRun 开启） |
| 启动到窗口出现 | fast 851 ms；release 1509 ms（均为首次运行，含单文件原生组件解压） |
| 工作集 | fast 150.3 MB；release 147 MB（空闲预览窗口） |
| 测试 | Core 36/36 通过；Release 构建 0 警告 0 错误 |
| 产物目录 | `dist/<档>/` 下只有一个 EXE，调试符号在 `symbols/` 子目录 |

> 注：`KiwiTraffic.Infrastructure.Tests` 目前没有测试，`dotnet test` 会提示
> 「没有可用测试」。这是预期的，M2 会往里加。不影响退出码。

体积偏大属自包含单文件 WPF 的正常水平（未启用 trimming/AOT，见 `BUILD.md` 的说明）。

## 待办

- [x] M0：API 契约核实 → `docs/api-contract.md`
- [ ] M0 收尾：由用户实测四项待定事项（倍率口径、SI/IEC、面板一致性、重置时区）
- [ ] M2：`IKiwiVmClient`、`UsageMapper`（含可切换的倍率策略）、宽松 JSON 转换器、
  DPAPI 凭据、配置/缓存存储、连接测试
- [ ] M3：真正的悬浮窗（拖动/置顶/DPI 位置记忆）、托盘、单实例
- [ ] M4：刷新协调器、退避、限频冷却、提醒去重、开机启动
- [ ] M5：验收、文档、末次实测
- [ ] 决定并记录 `BytePrefixStyle` 的最终取值（需与面板核对）
- [ ] 依赖计划：`Microsoft.Extensions.*` 仅在有实际需要时再引入，当前无外部依赖

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
改用 `TheoryData<decimal?>` + `[MemberData]`。

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
