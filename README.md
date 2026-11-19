# 代理环境变量管理器

一个面向 Windows 的轻量 GUI 小工具，用于一键设置或清除当前用户的 `http_proxy`、`https_proxy`、`no_proxy` 环境变量。

## 功能

- 从本机 Clash Verge 配置自动读取代理端口。
- 从 Windows 当前用户系统代理读取 HTTP/HTTPS 地址。
- 支持手动输入代理地址，缺少协议时自动补全为 `http://`。
- 支持设置 `no_proxy`；读取 Windows 系统代理时会把绕过列表转换为逗号分隔格式。
- 一键保存到 `HKCU\Environment`，不修改 Windows 系统代理开关。
- 一键清除这三个环境变量，不影响其他环境变量。
- 最小化到 Windows 系统托盘，双击托盘图标恢复主界面。
- 窗口、EXE 和系统托盘使用统一的原创应用图标。

## 直接使用

运行：

```text
dist\ProxyEnvManager.exe
```

选择代理来源，确认或修改地址和 `no_proxy`，然后点击“应用并保存”。已经打开的终端、IDE、VS Code 等程序通常需要重启后才会读取新的环境变量。

Clash Verge 的发现逻辑会按以下顺序读取当前用户配置目录中的文件：

1. `clash-verge.yaml` 的顶层 `mixed-port`
2. `config.yaml` 的顶层 `mixed-port`
3. `verge.yaml` 的 `verge_mixed_port`
4. 运行时 `port` 或 `verge_port`

如果自动检测失败，可以将 Clash Verge 中的混合端口手动填入，例如 `127.0.0.1:7897`。

## 构建

项目仅依赖 Windows 自带的 .NET Framework WinForms 和 `csc.exe`，无需下载 NuGet 包。

```powershell
.\build.ps1
```

生成文件：`dist\ProxyEnvManager.exe`，可单独复制使用。

## 应用图标

图标为蓝底白色双向传输箭头，无文字，专门优化了 16px 和 20px 系统托盘尺寸。

- 可编辑源稿：`assets\proxy-env-manager-logo.svg`
- 512px 预览图：`assets\proxy-env-manager-logo.png`
- 多尺寸程序图标：`assets\proxy-env-manager.ico`（16、20、24、32、40、48、64、128、256px）

修改设计后可重新生成 PNG 和 ICO：

```powershell
.\tools\Generate-AppIcon.ps1
```

## 测试

```powershell
.\test.ps1
```

测试覆盖代理地址规范化、`no_proxy` 转换、Windows 系统代理格式解析、Clash 配置端口优先级和 YAML 顶层字段识别。

## 说明

- 工具只管理当前用户环境变量，不需要管理员权限。
- 工具不会启动、停止或配置 Clash Verge。
- 环境变量中的代理密码会以普通文本保存在当前用户注册表中，请只在可信账户中使用。
- 自动广播环境变量变更面向 Windows 应用；部分程序仍只会在启动时读取环境变量。
