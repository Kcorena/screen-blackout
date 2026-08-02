# ScreenBlackout — 一键黑屏 + MSI 键盘背光联动

超小的 Windows 工具：双击把屏幕变成纯黑（**不是**关屏/休眠，后台照常运行），再双击 / 按 Esc / 点鼠标恢复。在 MSI 游戏本上还会同时**关闭/恢复键盘背光**。

## 功能

- 全屏纯黑覆盖层，覆盖所有显示器，隐藏鼠标指针
- 开关式切换：再双击 exe / 按 Esc / 点击屏幕 → 恢复
- MSI 键盘联动：变黑时关闭键盘背光，恢复时还原（仅限带 Mystic Light MCU 的 MSI 机型）
- 无依赖、免安装、单 exe 约 10KB

## 用法

双击 `ScreenBlackout.exe`：

1. 第一次双击 → 屏幕全黑，键盘背光同时熄灭（MSI 机型）
2. 再双击 / 按 Esc / 点鼠标 → 屏幕和背光一起恢复

## 工作原理

### 黑屏
C# WinForms 无边框全屏置顶黑窗（取所有显示器边界并集）。用命名 Mutex 做单实例 + 命名事件做"优雅关闭"——二次点击时通知第一个实例自己关闭，确保恢复逻辑（键盘背光）一定会执行，而不是粗暴杀进程。

### 键盘背光
MSI Mystic Light 键盘 MCU 是一个 HID 设备（`VID 0x1462 / PID 0x1601`），通过 `hid.dll` 的 `HidD_SetFeature` 发送 64 字节 feature report（report ID = 2）：

| Packet ID | 作用 |
|---|---|
| 1 | 选择区域（0x0F = 全部 4 区） |
| 2 | 配置灯光效果（Animation Type = 0 即 Disable → 关灯） |
| 176 | 从闪存重新加载配置（→ 恢复灯光） |

协议逆向参考：[MSI Katana 15 B12V keyboard lighting protocol](https://gist.github.com/natanalt/06f1d5854230c788b9b9e7e33ab90b9f)（与 Pulse 15 同款 MCU）。

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
csc /nologo /target:winexe /out:ScreenBlackout.exe ScreenBlackout.cs MsiKb.cs
```

## 文件

| 文件 | 说明 |
|---|---|
| `ScreenBlackout.cs` | 主程序：黑屏窗体 + 开关逻辑 |
| `MsiKb.cs` | MSI 键盘 HID 控制封装（SetupAPI 找设备 + HidD_SetFeature 发命令） |
| `MsiKbTest.cs` | 命令行测试工具：`MsiKbTest.exe off` / `on` |

## License

MIT
