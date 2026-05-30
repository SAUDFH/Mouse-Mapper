# MouseMapper Design Spec

**Date:** 2026-05-27
**Status:** Approved
**Target:** Forza Horizon 6 on Windows 11

## Problem

Keyboard + mouse input in racing games is purely digital — steering and throttle are either full-on or full-off. There is no way to apply partial steering angle or partial throttle, making precise vehicle control impossible.

## Solution

A virtual Xbox 360 controller (ViGEm) driven by mouse input, with configurable response curves:

- **Mouse left/right movement** → left analog stick X-axis (steering). Mouse stop = hold angle.
- **Scroll wheel up/down** → right trigger (throttle), cumulative with middle-click reset.
- **Brake** stays on keyboard, not mapped.

## Architecture

Single-process WPF tray application (MouseMapper.exe).

```
GlobalMouseHook (WH_MOUSE_LL)
    → InputMapper (activation check → deadzone → curve → clamp)
        → Shared state (volatile float for steering, throttle)
            → ViGEmManager.SetAxis / SetSlider
            → OSD (WPF transparent overlay)

ConfigManager (JSON ↔ %APPDATA%\MouseMapper\config.json)
SettingsWindow (WPF Window, slider-based curve editing with live preview)
TrayIcon (system tray, right-click menu)
```

Threads:
- **Input Thread**: `WH_MOUSE_LL` hook in message loop, event-driven. Reads mouse → computes axis values → writes shared state.
- **ViGEm Update Thread**: 60Hz timer reads shared state → `SubmitReport()`.

## Tech Stack

| Component | Choice |
|---|---|
| Language | C# (.NET 8 or 9) |
| UI Framework | WPF |
| Virtual Controller | Nefarius.ViGEmClient (NuGet) |
| Global Mouse Hook | `WH_MOUSE_LL` via P/Invoke `SetWindowsHookEx` |
| Config Format | JSON (`System.Text.Json`) |
| Tray Icon | `H.NotifyIcon` (WPF) or `System.Windows.Forms.NotifyIcon` |

## Response Curve System

Two independent curves (steering + throttle), defined by 5 sliders each.

### Steering Curve Parameters

| Slider | Range | Default | Description |
|---|---|---|---|
| Deadzone | 0%–20% | 3% | Input below this outputs 0 |
| Low Slope | 0.1x–1.0x | 0.3x | Sensitivity in low input range |
| Knee Point | 10%–90% | 35% | Input position where slope transitions |
| High Slope | 1.0x–4.0x | 2.5x | Sensitivity in high input range |
| Smoothing | 0ms–200ms | 50ms | Output transition smoothing |

### Throttle Curve Parameters

| Slider | Range | Default | Description |
|---|---|---|---|
| Deadzone | 0%–5% | 0% | Input below this outputs 0 |
| Low Slope | 0.5x–3.0x | 1.5x | Sensitivity at low throttle |
| Knee Point | 10%–90% | 50% | Input position where slope transitions |
| High Slope | 0.1x–1.0x | 0.5x | Sensitivity at high throttle |
| Smoothing | 0ms–200ms | 30ms | Output transition smoothing |

### Curve Computation

Two-segment piecewise-linear function with smoothing:

```
def computeCurve(input, deadzone, lowSlope, kneePoint, highSlope, smoothingMs):
    if |input| < deadzone: return 0

    sign = input / |input|
    absIn = min(|input|, 1.0)

    if absIn <= kneePoint:
        raw = absIn * lowSlope * kneePoint
    else:
        kneeOutput = kneePoint * lowSlope * kneePoint # simplified
        raw = kneeOutput + (absIn - kneePoint) * highSlope * (1.0 - kneeOutput) / (1.0 - kneePoint)

    raw = clamp(raw * sign, -1, 1)

    # Exponential moving average smoothing
    output = lerp(prevOutput, raw, smoothingFactorFromMs(smoothingMs))

    return output
```

Steering outputs [-1, 1] (left to right). Throttle outputs [0, 1].

The live preview is a read-only Canvas rendering the computed curve, updating as sliders move.

## Activation

- Toggle on/off via configurable key (default: `~` / `Oem3`).
- When inactive, mouse behaves normally; no virtual controller axis updates.
- OSD indicator shows activation state (green dot = active, gray = inactive).

## Throttle Model

- Scroll wheel delta accumulates into a persistent value.
- Up = increase throttle, Down = decrease throttle.
- Middle mouse button resets throttle to 0.
- Accumulated value passes through throttle response curve.
- Output clamped to [0, 1] and sent as right trigger (`SetSlider`).

## OSD Overlay

WPF transparent window, always on top, click-through.

| Element | Visual | Description |
|---|---|---|
| Steering | Horizontal bar + % | Left side = left turn, right side = right turn |
| Throttle | Vertical bar + % | Bottom = 0%, top = 100% |
| Active indicator | Colored dot | Green = active, gray = inactive |
| Key hint | Small text | "Press ~ to toggle" (only when inactive) |

Operations:
- Drag: grab handle area at top, temporarily disables click-through
- Scale: scroll wheel on handle area, or via settings slider
- Show/Hide: tray menu or configurable hotkey
- Default position: bottom-right, 40px from edges

## Settings Window

WPF window with sidebar navigation:

| Page | Content |
|---|---|
| Key Bindings | Toggle key recorder, throttle reset key, OSD toggle hotkey |
| Curve Editor | Steering/Throttle tab, live preview Canvas, 5 sliders per curve, real-time input→output preview bar |
| OSD Settings | Enable/disable, scale slider, position reset button |
| About | Version, ViGEm driver status indicator |

Save/Cancel/Reset buttons at bottom.

## Config File

Location: `%APPDATA%\MouseMapper\config.json`

```jsonc
{
  "version": 1,
  "activation": {
    "toggleKey": "Oem3",
    "throttleResetButton": "Middle"
  },
  "steering": {
    "deadzone": 0.03,
    "lowSlope": 0.3,
    "kneePoint": 0.35,
    "highSlope": 2.5,
    "smoothingMs": 50,
    "sensitivity": 1.0
  },
  "throttle": {
    "deadzone": 0.0,
    "lowSlope": 1.5,
    "kneePoint": 0.5,
    "highSlope": 0.5,
    "smoothingMs": 30
  },
  "osd": {
    "enabled": true,
    "scale": 1.0,
    "positionX": 0,
    "positionY": -40
  }
}
```

First launch auto-generates defaults. GUI changes write immediately.

## Error Handling

| Scenario | Behavior |
|---|---|
| ViGEm driver not installed | About page shows "Not Installed", OSD shows warning |
| Virtual controller creation fails | Log error, show tray notification, retry on next activation |
| Config file corrupt | Reset to defaults, log warning, notify user |
| Global hook blocked by anti-cheat | Document as known limitation — user disables tool before launching protected games |

## Non-Goals

- Button mapping (A/B/X/Y, D-pad) beyond what's listed
- Multi-controller support
- Force feedback / rumble
- Auto-update mechanism
- Game-specific profiles (single profile for personal use)

## Dependencies

- [ViGEmBus](https://github.com/nefarius/ViGEmBus) — kernel driver, must be installed separately
- [Nefarius.ViGEmClient](https://www.nuget.org/packages/Nefarius.ViGEmClient) — .NET wrapper, via NuGet
- .NET 8 or 9 runtime
