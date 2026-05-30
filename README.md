# MouseMapper

[English](#english) | [简体中文](#简体中文)

---

<a id="english"></a>

## MouseMapper

Map mouse input to a virtual Xbox 360 controller for racing games. Turn your mouse into an analog steering wheel and your scroll wheel into a progressive throttle — no more binary keyboard controls.

**Target:** Forza Horizon 6 / Windows 11

### How It Works

| Physical Input | Virtual Output |
|---|---|
| Mouse left/right movement | Left analog stick X-axis (steering) |
| Scroll wheel up/down | Right trigger (throttle) |
| Middle mouse button | Reset throttle to 0 |
| Left Alt (configurable) | Reset steering to 0 (recenter) |
| XButton1 / XButton2 | Gear down / gear up |
| Configurable toggle key (default `~`) | Activate / deactivate mapping |

### Features

- **Response curves** — Piecewise-linear curve with adjustable deadzone, low/high slope, knee point, and EMA smoothing. Tune steering and throttle independently.
- **OSD overlay** — Transparent, always-on-top HUD showing real-time steering/throttle levels and active status. Draggable and resizable.
- **Persistent throttle** — Scroll wheel accumulates into a held throttle value. Middle-click resets to zero.
- **Steering recenter** — Hold Left Alt (configurable) to temporarily reset steering to center. Release to resume control from the current cursor position.
- **Configurable key bindings** — Toggle key, throttle reset, steering recenter, gear shift keys.
- **System tray** — Lives in the tray. Right-click menu for quick toggle, OSD, settings, and exit.
- **JSON config** — Settings saved to `%APPDATA%\MouseMapper\config.json`. Auto-generates defaults on first launch.

### Requirements

- **Windows 11** (Windows 10 should work)
- **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** (or SDK)
- **[ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases)** — kernel-level virtual gamepad driver, install once

### Quick Start

```bash
# Clone and build
git clone https://github.com/SAUDFH/Mouse-Mapper.git
cd Mouse-Mapper
dotnet build MouseMapper/MouseMapper.csproj -c Debug

# Or use the batch file
run.bat
```

The app starts minimized to the system tray. Press `~` (configurable) to toggle mapping on/off.

### Project Structure

```
MouseMapper/
├── MouseMapper/              # WPF application (.NET 8)
│   ├── Models/
│   │   ├── AppConfig.cs      # Config schema + defaults
│   │   └── CurveParameters.cs # Curve parameter model
│   ├── Services/
│   │   ├── ConfigManager.cs  # JSON config read/write
│   │   ├── CurveCalculator.cs # Response curve math
│   │   ├── GlobalKeyboardHook.cs # WH_KEYBOARD_LL hook
│   │   ├── GlobalMouseHook.cs # WH_MOUSE_LL hook
│   │   ├── InputMapper.cs    # Input → virtual axis pipeline
│   │   └── ViGEmManager.cs   # Virtual Xbox 360 controller
│   ├── ViewModels/           # MVVM glue
│   ├── Views/
│   │   ├── Controls/         # CurvePreviewControl
│   │   ├── OsdWindow.xaml    # Transparent overlay
│   │   └── SettingsWindow.xaml # Settings UI
│   ├── NativeMethods.cs      # P/Invoke declarations
│   ├── App.xaml.cs           # Application entry point, tray & hooks
│   └── MainWindow.xaml       # (hidden, tray-only app)
├── MouseMapper.Tests/        # Unit tests
│   ├── CurveCalculatorTests.cs
│   ├── InputMapperTests.cs
│   └── ConfigManagerTests.cs
├── docs/                     # Design specs & plans
├── MouseMapper.sln
└── run.bat                   # Build + launch shortcut
```

### Response Curve

The curve is a two-segment piecewise-linear function with EMA smoothing:

```
Raw output = f(|input|) after deadzone:
  Segment 1 (0 → knee): linear, slope = LowSlope
  Segment 2 (knee → 1): linear, slope = HighSlope

Clamp → apply anti-deadzone → EMA smoothing → final output

Steering: output ∈ [-1, 1]   (negative = left)
Throttle: output ∈ [0, 1]
```

Adjustable in Settings → Steering Curve / Throttle Curve with live preview.

### Known Limitations

- ViGEmBus driver must be installed manually before first use
- Anti-cheat software may block global hooks — disable MouseMapper before launching protected games
- **XInput deadzone** — many games apply their own XInput deadzone (typically ~20%) on the virtual controller input. MouseMapper's curve deadzone is applied *before* the game receives the signal, but the game's built-in deadzone is not bypassable from the app side. Lowering the game's own deadzone setting (if available) is recommended. Alternatively, increase the **Anti-Deadzone** value in MouseMapper's curve settings to compensate. For example, in Forza Horizon 6, setting Anti-Deadzone to 24% effectively counteracts the game's default XInput deadzone.
- Single profile only (no per-game profiles)
- No force feedback / rumble support

### Dependencies

| Package | Purpose |
|---|---|
| [Nefarius.ViGEm.Client](https://www.nuget.org/packages/Nefarius.ViGEmClient) | .NET wrapper for ViGEm virtual controller |
| [ViGEmBus](https://github.com/nefarius/ViGEmBus) | Kernel driver (install separately) |

---

<a id="简体中文"></a>

## MouseMapper（鼠标映射器）

将鼠标输入映射为虚拟 Xbox 360 手柄，专为赛车游戏设计。把你的鼠标变成模拟方向盘，滚轮变成线性油门 — 告别键盘的纯数字操控。

**目标场景：** Forza Horizon 6（极限竞速：地平线 6）/ Windows 11

### 工作原理

| 物理输入 | 虚拟输出 |
|---|---|
| 鼠标左右移动 | 左摇杆 X 轴（转向） |
| 滚轮上下滚动 | 右扳机键（油门） |
| 鼠标中键 | 油门归零 |
| 左 Alt（可配置） | 转向归零（回中） |
| XButton1 / XButton2 | 降档 / 升档 |
| 可配置切换键（默认 `~`） | 启用 / 停用映射 |

### 功能特性

- **响应曲线** — 分段线性曲线，支持可调节的死区、低/高灵敏度斜率、拐点以及 EMA 平滑。转向和油门独立调节。
- **OSD 叠加层** — 透明置顶 HUD，实时显示转向/油门幅度及激活状态。可拖拽、可缩放。
- **持续油门** — 滚轮累积保持油门值，中键归零。
- **转向回中** — 按住左 Alt（可配置）临时将转向归零。松开后从当前光标位置恢复控制。
- **可配置按键** — 切换键、油门重置、转向回中、升降档键均可自定义。
- **系统托盘** — 常驻托盘，右键菜单快速切换、OSD、设置和退出。
- **JSON 配置** — 设置保存在 `%APPDATA%\MouseMapper\config.json`，首次启动自动生成默认配置。

### 环境要求

- **Windows 11**（Windows 10 理论上可用）
- **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)**（或 SDK）
- **[ViGEmBus 驱动](https://github.com/nefarius/ViGEmBus/releases)** — 内核级虚拟手柄驱动，需安装一次

### 快速开始

```bash
# 克隆并编译
git clone https://github.com/SAUDFH/Mouse-Mapper.git
cd Mouse-Mapper
dotnet build MouseMapper/MouseMapper.csproj -c Debug

# 或直接使用批处理文件
run.bat
```

程序启动后最小化到系统托盘。按 `~`（可配置）切换映射开关。

### 项目结构

```
MouseMapper/
├── MouseMapper/              # WPF 应用 (.NET 8)
│   ├── Models/
│   │   ├── AppConfig.cs      # 配置模型与默认值
│   │   └── CurveParameters.cs # 曲线参数模型
│   ├── Services/
│   │   ├── ConfigManager.cs  # JSON 配置读写
│   │   ├── CurveCalculator.cs # 响应曲线计算
│   │   ├── GlobalKeyboardHook.cs # WH_KEYBOARD_LL 全局钩子
│   │   ├── GlobalMouseHook.cs # WH_MOUSE_LL 全局钩子
│   │   ├── InputMapper.cs    # 输入 → 虚拟轴管线
│   │   └── ViGEmManager.cs   # 虚拟 Xbox 360 手柄管理
│   ├── ViewModels/           # MVVM 视图模型
│   ├── Views/
│   │   ├── Controls/         # 曲线预览控件
│   │   ├── OsdWindow.xaml    # 透明叠加层
│   │   └── SettingsWindow.xaml # 设置窗口
│   ├── NativeMethods.cs      # P/Invoke 声明
│   ├── App.xaml.cs           # 应用入口、托盘和钩子管理
│   └── MainWindow.xaml       # （隐藏，纯托盘应用）
├── MouseMapper.Tests/        # 单元测试
│   ├── CurveCalculatorTests.cs
│   ├── InputMapperTests.cs
│   └── ConfigManagerTests.cs
├── docs/                     # 设计文档与计划
├── MouseMapper.sln
└── run.bat                   # 编译并启动快捷脚本
```

### 响应曲线

曲线为两段分段线性函数，带 EMA 平滑：

```
Raw 输出 = f(|输入|) 经过死区后:
  第 1 段（0 → 拐点）：线性，斜率 = LowSlope
  第 2 段（拐点 → 1）：线性，斜率 = HighSlope

钳制 → 施加反死区 → EMA 平滑 → 最终输出

转向：输出 ∈ [-1, 1]（负值 = 左转）
油门：输出 ∈ [0, 1]
```

可在设置 → 转向曲线 / 油门曲线中调节，带实时预览。

### 已知限制

- ViGEmBus 驱动需在首次使用前手动安装
- 反作弊软件可能会拦截全局钩子 — 启动受保护的游戏前请先关闭 MouseMapper
- **XInput 死区** — 很多游戏会在虚拟手柄输入上施加自己的 XInput 死区（通常约 20%）。MouseMapper 的曲线死区是在信号*到达游戏之前*生效的，但游戏内置的死区无法从应用端绕开。建议在游戏中（如果有相关设置）将死区尽可能调低，或者在 MouseMapper 的曲线设置中调高 **Anti-Deadzone** 值来补偿。例如在《极限竞速：地平线 6》中，将 Anti-Deadzone 设为 24% 可以有效抵消游戏的默认 XInput 死区。
- 仅支持单配置文件（无多游戏独立配置）
- 不支持力反馈 / 震动

### 依赖项

| 包 | 用途 |
|---|---|
| [Nefarius.ViGEm.Client](https://www.nuget.org/packages/Nefarius.ViGEmClient) | ViGEm 虚拟手柄的 .NET 封装 |
| [ViGEmBus](https://github.com/nefarius/ViGEmBus) | 内核驱动（需单独安装） |
