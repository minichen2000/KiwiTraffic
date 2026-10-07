# KiwiVM API 数据契约核实结果

核实日期：2026-10-07
核实方式：公开资料联网检索 + 多源交叉验证。**未调用真实 API**（无密钥，且不应为此请求真实服务）。

> 本文件是 API 字段与错误语义的**唯一展开处**。其他文档只写摘要与指针。
>
> **可信度标注含义**
> - `官方`：能直接访问到的官方页面原文
> - `官方（经二手转录，多源一致）`：官方文档需登录才能看到，本结论来自多个独立第三方对官方原文的转录，且彼此一致
> - `社区实现（多源一致）`：多个独立开源实现的代码/文档一致
> - `单源未验证`：只有一个来源，不足以作为实现依据
> - `未找到`：没有证据。**不得据此推断**

## 0 取证条件（重要）

- 官方 API 文档位于 KiwiVM 面板登录后页面 `https://kiwivm.64clouds.com/main-exec.php?mode=api`。
  直接请求返回 HTTP 200，但内容是 `KiwiVM Session Timeout / Session Expired`（`官方`，2026-10-07）。
  **官方原文目前不可公开访问**，因此下表中大量字段依赖第三方转录。
- 主域名 `bandwagonhost.com` 从本机网络不可达；镜像域 `bwh81.net` 可达且确认为官方镜像
  （官方 KB 原文："It is possible to use affiliate links with our mirror domain in the same way as
  with our primary domain (bandwagonhost.com)"，来源 <https://bwh81.net/knowledgebase.php>，`官方`）。
- 社区代码中抄录的官方英文注释，是中转来源里可信度最高的一类，下文标为
  `官方文档（经二手转录）`。

## 1 已核实：流量相关字段

| 字段 | 结论 | 可信度 |
| --- | --- | --- |
| `plan_monthly_data` | 本计费周期总额度，**单位：字节** | 官方（经二手转录，多源一致） |
| `data_counter` | 本计费周期已用流量，**单位：字节** | 官方（经二手转录，多源一致） |
| `monthly_data_multiplier` | 存在。机房带宽核算系数。**必须是浮点数**（存在 0.33 这类值） | 官方（经二手转录）+ 社区实现（多源一致） |
| `data_next_reset` | 计数器重置时刻，**Unix 秒级时间戳**（UTC 基准，无时区字段） | 官方（经二手转录，多源一致） |
| 套餐名字段 | 是 `plan`，**不是** `plan_name` | 官方（经二手转录） |
| `plan_max_data` | **不存在**。任何来源均未出现；BWH 侧只有 `plan_monthly_data`/`plan_disk`/`plan_ram`/`plan_swap`/`plan_max_ipv6s` | 未找到 |

原文字摘录（官方英文注释，经社区转录）：

> "Allowed monthly data transfer (bytes). Needs to be multiplied by monthly_data_multiplier - see below."
> "Data transfer used in the current billing month. Needs to be multiplied by monthly_data_multiplier - see below."
> "Some locations offer more expensive bandwidth; this variable contains the bandwidth accounting coefficient."

单位佐证（`社区实现（多源一致）`）：公开的 mock 夹具中 `plan_monthly_data: 2147483648000`
对应套餐名 `...-2000g-...`，而 2000 × 1024³ = 2147483648000，证明是**字节**。

### 1.1 `getServiceInfo` 完整字段列表

`vm_type, hostname, node_alias, node_location_id, node_location, node_datacenter,
location_ipv6_ready, plan, plan_disk, plan_ram, plan_swap, plan_max_ipv6s, os, email,
plan_monthly_data, monthly_data_multiplier, data_counter, data_next_reset, ip_addresses,
ipv6_sit_tunnel_endpoint, private_ip_addresses, ip_nullroutes, iso1, iso2, available_isos,
rdns_api_available, plan_private_network_available, location_private_network_available, ptr,
free_ip_replacement_interval, suspended, policy_violation, suspension_count,
total_abuse_points, max_abuse_points, error`

可信度：`官方（经二手转录）/ 社区实现`。

与本项目相关的附加状态字段：`suspended`、`policy_violation`、`suspension_count`
（流量耗尽被停机时可用于给出更准确的提示）。

## 2 ⚠️ 未解决：`monthly_data_multiplier` 到底怎么用

**这是当前唯一会实质影响计费正确性的未决项，实现时必须保持可切换，不得写死。**

存在两个互相冲突的口径：

记号：`c` = `data_counter`，`pm` = `plan_monthly_data`，`m` = `monthly_data_multiplier`。

**口径 A（官方文档原文）**：两者**都要乘**倍率。

```
已用 = c × m        额度 = pm × m        百分比 = c / pm
```

**口径 B（社区实测现象推断）**：倍率是"按机房的配额系数"，普通机房 1x，CN2 GT（DC3）为
**0.33x**（月配额缩到 1/3，迁回即恢复）。此时只有额度被缩放，已用量不再乘：

```
已用 = c            额度 = pm × m        百分比 = c / (pm × m)
```

**关键：两者对"面板显示的额度"预测一致，对"百分比"预测不同。**

- 面板配额缩到 1/3 这一点，**A 和 B 都能解释**（A 中 `pm × m` 同样是 1/3），所以它
  **不能**用来区分两个口径。
- 真正的分歧点在**已用量**：面板显示的 used 是 `c` 还是 `c × m`。
  对 `m < 1`（如 0.33），B 的百分比是 A 的 `1/m` 倍（约 3 倍），差别巨大。
- 注意：口径 A 与"完全不乘倍率"在百分比上完全等价（同乘则约掉），仅在**显示的绝对值**上不同。
- 社区实现两种做法并存（"两者都乘" 与 "只乘 data_counter"），说明社区也未定论。
- **未找到**任何 `monthly_data_multiplier != 1` 的真实 API 响应样本，公开资料无法定案。

与本计划第 5 节的对应关系：计划已预先规定"倍率处理只做一次；只有确认分子与分母使用同一倍率，
才允许利用比例抵消；剩余量仍需正确换算"。因此实现上：

- 倍率策略收敛在 `UsageMapper` 一处（`MultiplierPolicy`），作为**显式可切换策略**而非散落的乘法；
- 默认按**口径 A**（有官方文档原文支撑），保留切换到口径 B 的路径；
- 在真实账号完成下方第 8 节的对照前，**不得宣称倍率处理已验证**
  （M2 完成条件中的"真实账号数据与面板完成首次对照"因此保持未达成）。

## 3 计费方向（入站/出站）

- **未找到官方原文**。多个第三方站点一致转述"入站+出站双向 1:1 计入"，
  但均为二手转述客服答复。
- 可信度：`单源未验证`。官方 TOS（<https://bwh81.net/terms-of-service.php>，`官方`）只提到
  "The monthly data transfer usage on your VPS is under 10% of your monthly quota"，
  **未说明计费方向**。

对本项目的影响：**不需要**在客户端做方向拆分——`data_counter` 已经是服务端算好的合计数。
不要自行按接口方向加总。

## 4 错误表示

- **成功判定**：每个响应都含 `error` 字段，**`0` 表示成功**；非 0 时附 `message`。
  可信度：`官方（经二手转录，多源一致）`。
- 已知码值：

| 码 | 含义 | 可信度 |
| --- | --- | --- |
| `700005` | `Authentication failure`（veid/api_key 错） | 官方（经二手转录）+ 社区实现（多源一致） |
| `788888` | `VE is currently locked, try again in a few minutes`，可附 `additionalErrorInfo`、`additionalLockingInfo{last_status_update_s_ago, completed_percent, friendly_progress_message}` | 单源未验证 |
| `700000` | `VPS is not running` | 单源未验证 |
| `700003` | `already running` | 单源未验证 |
| — | 参数写错时消息形如 `API:invalidrequest` | 官方（经二手转录） |

- **未找到**官方公开的完整错误码对照表。

实现要求：**不能靠码值白名单判断成功**。以 `error == 0` 为成功判定；非 0 时按
`700005` 单独归类为认证失败（停止自动重试），其余归为业务错误并保留 `message` 供展示，
未知码不得当作成功。

## 5 限频

- 官方文档原文（经二手转录）：
  > "When you perform too many API calls in a short amount of time, KiwiVM API may start dropping
  > your requests for a few minutes. This call allows monitoring this matter."

- 查询接口：`getRateLimitStatus`，返回 `remaining_points_15min`（15 分钟区间剩余点数）
  与 `remaining_points_24h`（24 小时区间）。可信度：`官方（经二手转录）+ 社区实现（多源一致）`。
- 具体点数上限：**未找到**。
- 响应头（如 `X-RateLimit-*`）：**未找到**证据；社区 SDK 均只检查 HTTP 200，不解析限频头。
- "调用间隔不低于 1 秒"之类说法**无出处**，`单源未验证`。

对本项目的影响：计划第 6.2 节的退避与冷却数值**仍属自定策略**，不是官方限频值。
不额外轮询 `getRateLimitStatus`（它本身也消耗配额）；仅在收到疑似限频响应时用于诊断。

## 6 API 版本与主机名

- 所有来源中出现的 base URL 只有 **`https://api.64clouds.com/v1/`**。
  `官方（经二手转录）` + 全部社区库一致。
- **`v1.1` 无任何证据**：代码检索与网络检索命中 0 条。
  **不能断言"不存在"，只能说无证据**。计划中"确认 `getLiveServiceInfo` 在哪个版本"的问题：
  它与 `getServiceInfo` **同在 `/v1/`**。
- `api.64clouds.com` 是全部来源中唯一的 API 主机名；面板在 `kiwivm.64clouds.com`。
  **未找到**官方声明"这是唯一主机名"。

实现要求：按计划第 6.1 节，主机与路径用**固定允许列表**，不开放任意 Base URL。

## 7 密钥权限

- API Key 是**每台 VPS 一把、全权限**凭证（可关机/重装/执行命令/迁移）。形如 `private_` 前缀。
  可信度：`官方（经二手转录）+ 社区（多源一致）`。
- **未找到**只读密钥或权限范围（scope）支持的证据。面板只有 `Reset API Key`，
  重置后旧 key 立即失效，新 key 仍是全权限。社区工具的"read-only"是**客户端侧**行为，不是服务端权限。
  可信度：`未找到`（否定性结论，仅有社区侧旁证）。

对本项目的影响：**必须按最高权限凭证对待**——本地加密保存、不入日志、不进仓库、
不明的重试与自动化都要保守。这一点计划第 7 节已经要求，此处给出依据。

## 8 需要用户本机实测才能定案的事项

> 这些是**唯一**无法靠公开资料解决、且影响正确性的项。程序具备配置界面后，
> 由用户自行输入 VEID/API Key 完成，密钥不需要发到聊天中。

1. **倍率口径**（第 2 节）。一次即可定案：对一台倍率 ≠ 1 的机器（如迁到 CN2 GT 机房），
   同时记录三样东西——面板 Bandwidth usage 显示的 used、total，以及响应里的
   `data_counter`、`plan_monthly_data`、`monthly_data_multiplier`。
   判据是**面板 used 是否等于 `c × m`**：
   - 面板 used ≈ `c × m` → 口径 A（官方文档：两者都乘）
   - 面板 used ≈ `c` → 口径 B（只有额度乘倍率）

   配额那一项**不能**用来判断（A 和 B 都预测 `pm × m`）。
2. **百分比与重置时间的一致性**：记录采样时间差（计划第 11 节矩阵最后一行）。
3. **重置时刻的时区**：`data_next_reset` 是 Unix 时间戳（UTC 基准），但重置发生在什么本地时刻
   **未找到**官方说明。不要假设"每月 1 日"或固定 30 天。

### 已定案：单位口径（2026-10-07，真实账号）

对照：VEID 2213202，套餐 `KVMV5-20G-1G-1T-CA-CN2GIA`。

**事实**：`plan_monthly_data` = **1 073 741 824 000 字节 = 1000 × 1024³**，
面板把这一数量写作 **1 TB**。

**结论**：KiwiVM 的 "GB / TB" 是 **1024 进制**的量 —— 1000 个 1024³ 字节被称作 1 TB。

**处置**：

- 程序按 1024 除（数字与面板一致），标签用 **TiB / GiB**（把二进制倍数标成 GB 是本项目禁止的）。
- 并且在 **1000** 处进位而不是 1024：这样 1000 GiB 显示为 `1.0 TiB`，与面板的 `1 TB`
  读数一致，也避免出现 `1023.9 MiB` 这种写法。
  实现：`ByteSizeFormatter.SwitchThreshold` + `KiwiTraffic.App.DisplayFormat.BytePrefix`。

> **修正记录**：本条结论第一次是靠「面板写 1 TB、程序写 1.1 TB」推断的，当时**错误地假设**
> `plan_monthly_data` 是 2⁴⁰。后来在界面上看到程序实际显示 `1000.0 GiB`，才拿到真实原始值。
> 结论方向没变，依据换了 —— 记在这里，以免以后有人照着错误依据继续推理。
>
> 教训：从"两个显示值不同"反推原始数据，中间的假设必须写明并尽早用真实值验证。

### 补充：倍率问题对当前账号不存在

上述账号的倍率是 1，两种口径结果完全相同，因此**对该账号不存在倍率歧义**。
通用结论仍取决于一台倍率 ≠ 1 的机器（见第 1 项）。

## 9 实现层面的兼容性提醒

`社区实现（单源/双源，但风险直接相关）`：KiwiVM 响应的**字段类型不稳定**。
例如 `plan_disk` 可能以数字或字符串数字出现（`"plan_disk":"4294967296"`），
`location_ipv6_ready` 为 `1`，`location_private_network_available` 为 `"1"`。

M2 实现要求：为流量相关字段编写**宽松的自定义 `JsonConverter`**，同时接受 JSON number 与
可解析的字符串数字；解析失败一律判为无效响应，**不得静默退化为 0**（计划第 5 节）。

## 10 主要来源

- 官方可公开访问部分：<https://kiwivm.64clouds.com/main-exec.php?mode=api>（需登录）、
  <https://bwh81.net/knowledgebase.php>、<https://bwh81.net/terms-of-service.php>、<https://bwh81.net/>
- 官方文档的第三方完整转录：
  <https://bwgcn2gia.com/archives/325.html>、<https://md5.pw/>
- 社区实现（类型/字段注释，多处抄录官方英文原文）：
  <https://github.com/strahe/bwh>、<https://github.com/mushroom-cn/bwh81>、
  <https://pkg.go.dev/github.com/ZhangXavier/kiwivm-go@v0.0.1>、
  <https://github.com/icyleaf/bandwagon-exporter>
- 用于对照实现差异的社区监控工具：
  <https://github.com/ZainCheung/kiwivm-traffic-watch>、
  <https://github.com/weiqiang333/bandwagonhost_cloud_exporter>、
  <https://github.com/WangZhiYao/bwg-panel>、<https://github.com/kekemao00/vps_monitor>
- 低可信线索（仅作线索，未采信）：
  <https://github.com/muxg5950/bwh-traffic-migration>（0.33x 倍率）、
  <https://www.bwgyhw.cn/bandwagonhost-bandwidth-twice-than-actual/>（双向计费）

以上均为 2026-10-07 抓取。
