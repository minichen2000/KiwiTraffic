# KiwiTraffic

一个常驻 Windows 桌面的小工具，让你不打开控制面板就能看见搬瓦工（KiwiVM）VPS
当前计费周期的流量用了多少。

> **状态：可用但未完成。** 已经能读真实数据：填好 VEID 和 API Key 就能看到本周期用量。
> 还缺的是悬浮窗的那层外壳 —— 托盘图标、置顶、位置记忆（M3）与自动刷新（M4），
> 所以目前需要手动点刷新。
>
> 在信任某个数字之前，有两点需要知道：官方 API 文档在面板登录之后，本项目的数据契约
> 只能从公开资料重建；而 `monthly_data_multiplier` 的用法**仍未定论、也未经真实账号验证**
> —— 见 [`docs/api-contract.md`](docs/api-contract.md)。当前进度见 [PROGRESS.md](PROGRESS.md)。

## 功能（第一版范围）

- 大字号显示本周期已用百分比，并显示已用量 / 总额度 / 剩余量和下次重置时间。
- 可置顶的桌面悬浮窗 + 系统托盘常驻；支持拖动，窗口位置按 DPI 正确记忆。
- 自动刷新与手动刷新，并显示最近一次成功更新的相对时间。
- 80 % / 90 % / 95 % 三档流量提醒，按「账号 + 周期」去重，不重复打扰。
- 断网 / 限频 / 密钥失效 / 数据过期等状态分别明确显示 —— 错误绝不渲染成 0 % 用量。
- API Key 在本机输入，用 Windows DPAPI（CurrentUser 范围）加密保存，不落明文、不进日志。

## 第一版不包含

VPS 启停 / 重装 / 快照 / 迁移等管理操作、多 VPS 聚合、账号同步、移动端、网页后台、
实时网速、CPU / 内存 / SSH 监控、历史流量曲线、用量预测、自动更新器、安装包、商业代码签名。

## 环境要求

- Windows 10 或 11（x64）
- 构建需要 [.NET SDK 10.0.4xx](https://dotnet.microsoft.com/download/dotnet/10.0)（版本由 `global.json` 锁定）
- 构建脚本需要 PowerShell 7（`pwsh`）
- Node.js **可选** —— `package.json` 只是命令薄封装

运行不需要安装 .NET 运行时：发布产物是自包含单文件 EXE。

## 构建

```powershell
# 日常验证用的快速构建 -> dist/fast/KiwiTraffic.exe
npm run build:exe

# 完整质量门禁 + 正式 Release 构建 -> dist/release/KiwiTraffic.exe
npm run build:release

# 只跑测试
dotnet test
```

不装 Node.js 时直接调脚本：

```powershell
pwsh -File scripts/build.ps1 -Configuration fast
pwsh -File scripts/build.ps1 -Configuration release
```

细节见 [BUILD.zh.md](BUILD.zh.md)。

## 使用

1. 运行 `KiwiTraffic.exe`。它没有安装程序，放哪里都行。
2. 首次启动会让你填写 VPS 别名（可选）、VEID 和 API Key，后两者在 KiwiVM
   控制面板的 *API* 页面可以找到。
3. 窗口显示当前周期的用量。用「刷新」重新获取，用「设置…」修改配置。

   目前**还没有**：置顶、位置记忆、托盘图标、自动刷新。关闭窗口即退出程序。
   这些属于 M3 与 M4 —— 见 [PROGRESS.md](PROGRESS.md)。

## 数据与隐私

全部在本机完成。没有遥测、没有云同步，除查询 KiwiVM API 外没有其他网络流量。

数据位于 `%LOCALAPPDATA%\KiwiTraffic\`：

| 文件 | 内容 |
| --- | --- |
| `settings.json` | 别名、VEID、代理模式、刷新间隔、窗口位置、提醒设置 |
| `credentials.dat` | DPAPI 加密的 API Key（以及可能存在的代理密码） |
| `cache.json` | 最近一次有效的归一化快照 |
| `notifications.json` | 已确认的周期与已触发阈值 |
| `logs/` | 有限大小、已脱敏、自动轮转的诊断日志 |

要彻底清除本地数据：先退出程序，在设置里关掉「开机启动」，再删除上述目录。

API Key 使用 DPAPI `CurrentUser` 范围加密，把 EXE 拷到别的电脑或换 Windows 用户后
无法解密，届时程序会引导你重新输入。

## 许可证

尚未指定。
