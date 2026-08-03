<p align="center">
  <img src="assets/logo.png" alt="ScreenBlackout" width="160">
</p>

<h1 align="center">ScreenBlackout — 一键黑屏 + MSI 键盘背光联动</h1>

<p align="center">超小的 Windows 托盘工具：单击托盘图标把屏幕变成纯黑（不是关屏/休眠，后台照常运行），再单击 / 按 Esc / 点鼠标恢复。在 MSI 游戏本上还会同时关闭/恢复键盘背光。开机自动启动，常驻系统托盘。</p>

## 功能

- 全屏纯黑覆盖层，覆盖所有显示器，隐藏鼠标指针
- **系统托盘常驻**：单击托盘图标切换黑屏，右键菜单可退出 / 开关自启
- **开机自启**（HKCU 注册表 Run 键，免管理员；默认开启，可在托盘菜单关闭）
- 开关式：再双击 exe / 按 Esc / 点击屏幕 → 恢复
- MSI 键盘联动：变黑时关闭键盘背光，恢复时还原（仅限带 Mystic Light MCU 的 MSI 机型）
- 无依赖、免安装、单 exe 约 100KB（含图标）

## 用法

- **单击托盘图标** → 黑屏（键盘背光同时灭，MSI 机型）
- **再单击 / 按 Esc / 点屏幕** → 恢复正常
- **右键托盘图标** → 黑屏/恢复、开机自启动开关、退出
- **双击 exe** → 和单击托盘图标一样（切换），如果程序没在运行则先启动托盘并黑屏
- 开机自启使用 `--autostart` 参数（托盘菜单控制，无需手改）

## 工作原理

### 黑屏
C# WinForms 无边框全屏置顶黑窗（取所有显示器边界并集）。命名 Mutex 单实例 + 命名事件跨进程通信——第二次启动时通知托盘实例切换，而不是粗暴杀进程，确保恢复逻辑（键盘背光）一定会执行。

### 键盘背光
MSI Mystic Light 键盘 MCU 是一个 HID 设备（`VID 0x1462 / PID 0x1601`），通过 `hid.dll` 的 `HidD_SetFeature` 发送 64 字节 feature report（report ID = 2）：

| Packet ID | 作用 |
|---|---|
| 1 | 选择区域（0x0F = 全部 4 区） |
| 2 | 配置灯光效果（Animation Type = 0 即 Disable → 关灯） |
| 176 | 从闪存重新加载配置（→ 恢复灯光） |

协议逆向参考：[MSI Katana 15 B12V keyboard lighting protocol](https://gist.github.com/natanalt/06f1d5854230c788b9b9e7e33ab90b9f)（与 Pulse 15 同款 MCU）。

### 图标
使用自定义设计图 `assets/icon-source.png`，由 `PngToIco.cs` 转成多尺寸 ICO（16/24/32/48/64/128/256）后通过 `/win32icon` 嵌入 exe。`IconGen.cs` 是早期的程序化生成方案（GDI+ 绘制），保留备用。

## 兼容性

| 功能 | 兼容范围 |
|---|---|
| 屏幕黑屏 | 所有 Windows 版本 |
| 键盘背光联动 | 仅部分 MSI 笔记本（HID `VID_1462&PID_1601`），如 Katana / Pulse / Sword / Cyborg 系列 2022–2023 机型 |

非 MSI 机器上会自动跳过键盘部分（找不到设备就静默忽略），黑屏功能不受影响。

## 编译

需要 .NET Framework 4.x（Win10/11 自带），直接跑：

```
build.bat
```

或手动（csc 在 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`）：

```
csc /nologo /codepage:65001 /win32icon:ScreenBlackout.ico /target:winexe /out:ScreenBlackout.exe ScreenBlackout.cs MsiKb.cs
```

## 文件

| 文件 | 说明 |
|---|---|
| `ScreenBlackout.cs` | 主程序：托盘常驻 + 黑屏窗体 + 自启开关 |
| `MsiKb.cs` | MSI 键盘 HID 控制封装（SetupAPI 找设备 + HidD_SetFeature 发命令） |
| `MsiKbTest.cs` | 命令行测试工具：`MsiKbTest.exe off` / `on` |
| `PngToIco.cs` | PNG → 多尺寸 ICO 转换器（当前图标用这个） |
| `IconGen.cs` | 图标生成器（GDI+ 程序化绘制，早期方案，备用） |
| `assets/icon-source.png` | 图标源图（自定义设计） |
| `ScreenBlackout.ico` | 生成的图标（已嵌入 exe） |

## License

MIT
