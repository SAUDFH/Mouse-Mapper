# MouseMapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows WPF tray application that maps mouse movement to a virtual Xbox 360 controller (ViGEm) for analog steering and throttle control in Forza Horizon 6.

**Architecture:** Single-process C# WPF app. `GlobalMouseHook` (WH_MOUSE_LL) captures raw mouse input on a dedicated thread. `InputMapper` applies deadzone, piecewise-linear response curves, and smoothing. Output values are read by `ViGEmManager` (60Hz push to virtual controller) and `OsdWindow` (30Hz WPF overlay). `ConfigManager` persists settings as JSON. `SettingsWindow` provides slider-based curve editing with a live Canvas preview.

**Tech Stack:** .NET 8, WPF, Nefarius.ViGEmClient NuGet, System.Windows.Forms.NotifyIcon, System.Text.Json

**Target files:**

```
MouseMapper/
├── MouseMapper.sln
├── MouseMapper/
│   ├── MouseMapper.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── Models/
│   │   ├── AppConfig.cs
│   │   └── CurveParameters.cs
│   ├── Services/
│   │   ├── CurveCalculator.cs
│   │   ├── ConfigManager.cs
│   │   ├── GlobalMouseHook.cs
│   │   ├── ViGEmManager.cs
│   │   └── InputMapper.cs
│   ├── ViewModels/
│   │   ├── SettingsViewModel.cs
│   │   └── RelayCommand.cs
│   ├── Views/
│   │   ├── SettingsWindow.xaml / .cs
│   │   ├── OsdWindow.xaml / .cs
│   │   └── Controls/
│   │       └── CurvePreviewControl.xaml / .cs
│   └── NativeMethods.cs
└── MouseMapper.Tests/
    ├── MouseMapper.Tests.csproj
    ├── CurveCalculatorTests.cs
    ├── InputMapperTests.cs
    └── ConfigManagerTests.cs
```

---

### Task 1: Verify .NET 8 SDK and install if missing

**Files:** None (environment check)

- [ ] **Step 1: Check if .NET 8 SDK is installed**

Run: `dotnet --list-sdks`
Check: Output contains `8.0.x`. If not, proceed to Step 2. If yes, skip to Task 2.

- [ ] **Step 2: Install .NET 8 SDK**

Download and run the installer from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
Use the Windows x64 SDK installer. After installation, verify:

Run: `dotnet --version`
Expected: `8.0.x`

- [ ] **Step 3: Verify ViGEmBus driver is installed**

Run: `sc query ViGEmBus`
Expected: SERVICE_NAME: ViGEmBus, STATE: 4 RUNNING

If not installed, the plan will handle the "not installed" case in About page (Task 12). The driver installer is at https://github.com/nefarius/ViGEmBus/releases.

---

### Task 2: Create solution and project structure

**Files:**
- Create: `MouseMapper/MouseMapper.sln`
- Create: `MouseMapper/MouseMapper/MouseMapper.csproj`
- Create: `MouseMapper/MouseMapper/App.xaml`
- Create: `MouseMapper/MouseMapper/App.xaml.cs`
- Create: `MouseMapper/MouseMapper.Tests/MouseMapper.Tests.csproj`

- [ ] **Step 1: Create solution and WPF project**

```bash
mkdir -p E:/AI/Claude\ Code/MouseMapper
cd E:/AI/Claude\ Code/MouseMapper
dotnet new sln -n MouseMapper
dotnet new wpf -n MouseMapper -f net8.0
dotnet sln add MouseMapper/MouseMapper.csproj
```

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 2: Create test project**

```bash
dotnet new nunit -n MouseMapper.Tests -f net8.0
dotnet sln add MouseMapper.Tests/MouseMapper.Tests.csproj
cd MouseMapper.Tests
dotnet add reference ../MouseMapper/MouseMapper.csproj
cd ..
```

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: 0 tests (empty), build succeeded.

- [ ] **Step 3: Add NuGet packages**

```bash
cd MouseMapper
dotnet add package Nefarius.ViGEmClient --version 1.21.256
cd ..
```

Run: `dotnet restore MouseMapper/MouseMapper.csproj`
Expected: Restore succeeded.

- [ ] **Step 4: Create directory structure**

```bash
mkdir -p MouseMapper/Models
mkdir -p MouseMapper/Services
mkdir -p MouseMapper/ViewModels
mkdir -p MouseMapper/Views/Controls
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution with WPF project and test project"
```

---

### Task 3: Define data models

**Files:**
- Create: `MouseMapper/MouseMapper/Models/CurveParameters.cs`
- Create: `MouseMapper/MouseMapper/Models/AppConfig.cs`

- [ ] **Step 1: Write CurveParameters**

`MouseMapper/MouseMapper/Models/CurveParameters.cs`:

```csharp
namespace MouseMapper.Models;

public class CurveParameters
{
    public float Deadzone { get; set; } = 0.03f;
    public float LowSlope { get; set; } = 0.3f;
    public float KneePoint { get; set; } = 0.35f;
    public float HighSlope { get; set; } = 2.5f;
    public int SmoothingMs { get; set; } = 50;

    public CurveParameters Clone() => new()
    {
        Deadzone = Deadzone,
        LowSlope = LowSlope,
        KneePoint = KneePoint,
        HighSlope = HighSlope,
        SmoothingMs = SmoothingMs
    };
}
```

- [ ] **Step 2: Write AppConfig**

`MouseMapper/MouseMapper/Models/AppConfig.cs`:

```csharp
using System.Text.Json.Serialization;

namespace MouseMapper.Models;

public class AppConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("activation")]
    public ActivationConfig Activation { get; set; } = new();

    [JsonPropertyName("steering")]
    public CurveParameters Steering { get; set; } = new()
    {
        Deadzone = 0.03f,
        LowSlope = 0.3f,
        KneePoint = 0.35f,
        HighSlope = 2.5f,
        SmoothingMs = 50
    };

    [JsonPropertyName("throttle")]
    public CurveParameters Throttle { get; set; } = new()
    {
        Deadzone = 0f,
        LowSlope = 1.5f,
        KneePoint = 0.5f,
        HighSlope = 0.5f,
        SmoothingMs = 30
    };

    [JsonPropertyName("osd")]
    public OsdConfig Osd { get; set; } = new();
}

public class ActivationConfig
{
    [JsonPropertyName("toggleKey")]
    public string ToggleKey { get; set; } = "Oem3";

    [JsonPropertyName("throttleResetButton")]
    public string ThrottleResetButton { get; set; } = "Middle";
}

public class OsdConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1.0;

    [JsonPropertyName("positionX")]
    public double PositionX { get; set; } = 0;

    [JsonPropertyName("positionY")]
    public double PositionY { get; set; } = -40;
}
```

- [ ] **Step 3: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MouseMapper/Models/
git commit -m "feat: add AppConfig and CurveParameters models"
```

---

### Task 4: Implement CurveCalculator with unit tests

**Files:**
- Create: `MouseMapper/MouseMapper/Services/CurveCalculator.cs`
- Create: `MouseMapper/MouseMapper.Tests/CurveCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests**

`MouseMapper/MouseMapper.Tests/CurveCalculatorTests.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;

namespace MouseMapper.Tests;

public class CurveCalculatorTests
{
    [Test]
    public void Apply_InputBelowDeadzone_ReturnsZero()
    {
        var p = new CurveParameters { Deadzone = 0.03f, LowSlope = 1f, KneePoint = 0.5f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(0.02f, p, ref prev, 0.016f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void Apply_InputAboveDeadzone_ReturnsNonZero()
    {
        var p = new CurveParameters { Deadzone = 0.03f, LowSlope = 1f, KneePoint = 0.5f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(0.10f, p, ref prev, 0.016f);
        Assert.That(result, Is.GreaterThan(0f));
    }

    [Test]
    public void Apply_LinearCurveNoDeadzone_OutputMatchesInput()
    {
        var p = new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(0.5f, p, ref prev, 0.016f);
        Assert.That(result, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void Apply_FullInput_ReturnsCloseToOne()
    {
        var p = new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 0.5f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(1.0f, p, ref prev, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void Apply_LowSlopeGentle_ProducesSmallOutput()
    {
        var p = new CurveParameters { Deadzone = 0f, LowSlope = 0.2f, KneePoint = 0.4f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(0.2f, p, ref prev, 0.016f);
        // With low slope 0.2, knee at 0.4, input at 0.2: t=0.2, t/knee=0.5, output=0.5*lowSlope*knee=0.5*0.2*0.4=0.04
        Assert.That(result, Is.EqualTo(0.04f).Within(0.01f));
    }

    [Test]
    public void Apply_HighSlopeSteep_ProducesLargerOutput()
    {
        var p = new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 0.3f, HighSlope = 3f, SmoothingMs = 0 };
        float prev = 0f;
        // kneeOut = 0.3 * 1.0 = 0.3, input at 0.6: t=0.6, remaining=(0.6-0.3)/0.7=0.429 
        // linear between kneeOut and 1: base=0.3+0.429*0.7=0.6
        // with highSlope=3: output=0.3+0.429*0.7*3=1.2→clamped to 1.0
        float result = CurveCalculator.Apply(0.6f, p, ref prev, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void Apply_Smoothing_ReducesStepChange()
    {
        var p = new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 100 };
        float prev = 0f;
        // With 100ms smoothing and 16ms dt: alpha = 1 - exp(-16/100) ≈ 0.148
        // output = 0.148 * 1.0 + 0.852 * 0 = 0.148
        float result = CurveCalculator.Apply(1.0f, p, ref prev, 0.016f);
        Assert.That(result, Is.LessThan(0.2f));
        Assert.That(result, Is.GreaterThan(0.05f));
    }

    [Test]
    public void Apply_ZeroSmoothing_OutputInstant()
    {
        var p = new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0.5f;
        float result = CurveCalculator.Apply(1.0f, p, ref prev, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void Apply_DeadzoneEdge_ExactlyAtDeadzone_ReturnsZero()
    {
        var p = new CurveParameters { Deadzone = 0.05f, LowSlope = 1f, KneePoint = 0.5f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(0.05f, p, ref prev, 0.016f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void Apply_JustAboveDeadzone_ReturnsSmallPositive()
    {
        var p = new CurveParameters { Deadzone = 0.05f, LowSlope = 1f, KneePoint = 0.5f, HighSlope = 1f, SmoothingMs = 0 };
        float prev = 0f;
        float result = CurveCalculator.Apply(0.051f, p, ref prev, 0.016f);
        Assert.That(result, Is.GreaterThan(0f));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: 10 tests fail — `The type or namespace name 'CurveCalculator' does not exist`

- [ ] **Step 3: Implement CurveCalculator**

`MouseMapper/MouseMapper/Services/CurveCalculator.cs`:

```csharp
using MouseMapper.Models;

namespace MouseMapper.Services;

public static class CurveCalculator
{
    /// <summary>
    /// Apply the response curve to a normalized input value.
    /// </summary>
    /// <param name="input">Normalized input in [0, 1] (absolute value, caller handles sign)</param>
    /// <param name="p">Curve parameters</param>
    /// <param name="prevOutput">Previous output value (for EMA smoothing), updated in place</param>
    /// <param name="deltaTime">Time since last update in seconds</param>
    /// <returns>Output value in [0, 1]</returns>
    public static float Apply(float input, CurveParameters p, ref float prevOutput, float deltaTime)
    {
        if (input < p.Deadzone)
            return 0f;

        float t = (input - p.Deadzone) / (1f - p.Deadzone);

        float raw;
        float kneeOutput = p.KneePoint * p.LowSlope;

        if (t <= p.KneePoint)
        {
            raw = (t / p.KneePoint) * kneeOutput;
        }
        else
        {
            float blend = (t - p.KneePoint) / (1f - p.KneePoint);
            raw = kneeOutput + blend * (1f - kneeOutput) * p.HighSlope;
        }

        raw = Math.Clamp(raw, 0f, 1f);

        if (p.SmoothingMs <= 0)
        {
            prevOutput = raw;
            return raw;
        }

        float alpha = 1f - MathF.Exp(-deltaTime / (p.SmoothingMs / 1000f));
        float smoothed = alpha * raw + (1f - alpha) * prevOutput;
        prevOutput = smoothed;
        return smoothed;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add MouseMapper/Services/CurveCalculator.cs MouseMapper.Tests/CurveCalculatorTests.cs
git commit -m "feat: add CurveCalculator with piecewise-linear response and EMA smoothing"
```

---

### Task 5: Implement ConfigManager with tests

**Files:**
- Create: `MouseMapper/MouseMapper/Services/ConfigManager.cs`
- Create: `MouseMapper/MouseMapper.Tests/ConfigManagerTests.cs`

- [ ] **Step 1: Write failing tests**

`MouseMapper/MouseMapper.Tests/ConfigManagerTests.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;
using System.IO;
using System.Text.Json;

namespace MouseMapper.Tests;

public class ConfigManagerTests
{
    private string _testDir = null!;

    [SetUp]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MouseMapperTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Test]
    public void Load_FileDoesNotExist_CreatesDefaultAndReturnsIt()
    {
        var mgr = new ConfigManager(_testDir);
        var config = mgr.Load();
        Assert.That(config, Is.Not.Null);
        Assert.That(config.Version, Is.EqualTo(1));
        Assert.That(config.Steering.Deadzone, Is.EqualTo(0.03f).Within(0.001f));
        Assert.That(config.Throttle.LowSlope, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(config.Activation.ToggleKey, Is.EqualTo("Oem3"));
    }

    [Test]
    public void Load_FileExists_LoadsAndReturnsIt()
    {
        var original = new AppConfig
        {
            Version = 1,
            Steering = new CurveParameters { Deadzone = 0.05f, LowSlope = 0.5f, KneePoint = 0.4f, HighSlope = 3f, SmoothingMs = 40 }
        };
        string configPath = Path.Combine(_testDir, "config.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(original));

        var mgr = new ConfigManager(_testDir);
        var loaded = mgr.Load();
        Assert.That(loaded.Steering.Deadzone, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(loaded.Steering.LowSlope, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Load_FileCorrupted_ReturnsDefaultsAndDeletesCorruptedFile()
    {
        string configPath = Path.Combine(_testDir, "config.json");
        File.WriteAllText(configPath, "{ this is not valid json }");

        var mgr = new ConfigManager(_testDir);
        var config = mgr.Load();
        Assert.That(config.Version, Is.EqualTo(1));
        // Corrupted file should be gone (or ignored)
        Assert.That(File.Exists(configPath), Is.False);
    }

    [Test]
    public void Save_WritesFileAndCanReload()
    {
        var config = new AppConfig
        {
            Steering = new CurveParameters { Deadzone = 0.07f, LowSlope = 0.25f, KneePoint = 0.35f, HighSlope = 2.8f, SmoothingMs = 60 }
        };

        var mgr = new ConfigManager(_testDir);
        mgr.Save(config);

        string configPath = Path.Combine(_testDir, "config.json");
        Assert.That(File.Exists(configPath));

        var reloaded = mgr.Load();
        Assert.That(reloaded.Steering.Deadzone, Is.EqualTo(0.07f).Within(0.001f));
        Assert.That(reloaded.Steering.SmoothingMs, Is.EqualTo(60));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: 4 new tests fail — ConfigManager class doesn't exist.

- [ ] **Step 3: Implement ConfigManager**

`MouseMapper/MouseMapper/Services/ConfigManager.cs`:

```csharp
using MouseMapper.Models;
using System.IO;
using System.Text.Json;

namespace MouseMapper.Services;

public class ConfigManager
{
    private readonly string _configDir;
    private string ConfigPath => Path.Combine(_configDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigManager(string configDir)
    {
        _configDir = configDir;
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (JsonException)
        {
            try { File.Delete(ConfigPath); } catch { /* best effort */ }
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_configDir);
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: All 14 tests pass.

- [ ] **Step 5: Commit**

```bash
git add MouseMapper/Services/ConfigManager.cs MouseMapper.Tests/ConfigManagerTests.cs
git commit -m "feat: add ConfigManager with JSON load/save and corruption recovery"
```

---

### Task 6: Implement GlobalMouseHook

**Files:**
- Create: `MouseMapper/MouseMapper/NativeMethods.cs`
- Create: `MouseMapper/MouseMapper/Services/GlobalMouseHook.cs`

- [ ] **Step 1: Write NativeMethods (P/Invoke declarations)**

`MouseMapper/MouseMapper/NativeMethods.cs`:

```csharp
using System.Runtime.InteropServices;

namespace MouseMapper;

public static class NativeMethods
{
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;

    public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public struct POINT
    {
        public int x;
        public int y;
    }
}
```

- [ ] **Step 2: Write GlobalMouseHook**

`MouseMapper/MouseMapper/Services/GlobalMouseHook.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseMapper.Services;

public class MouseEventArgs : EventArgs
{
    public int DeltaX { get; init; }
    public int DeltaY { get; init; }
    public int WheelDelta { get; init; }
    public bool MiddleButtonPressed { get; init; }
}

public class GlobalMouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _proc;
    private Thread? _hookThread;
    private bool _running;

    public event EventHandler<MouseEventArgs>? MouseEvent;

    public void Start()
    {
        _running = true;
        _hookThread = new Thread(RunHookLoop)
        {
            Name = "MouseHookThread",
            IsBackground = true
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    private void RunHookLoop()
    {
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        if (curModule?.ModuleName == null) return;

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        // Message pump — required for low-level hooks
        MSG msg;
        while (_running && NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                // Only report if there's actual movement
                if (hookStruct.pt.x != 0 || hookStruct.pt.y != 0)
                {
                    // We track absolute position changes ourselves via raw input delta.
                    // The hook gives us screen coordinates; we compute dx/dy in InputMapper.
                }
            }
            else if (msg == NativeMethods.WM_MOUSEWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                short wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                bool middleDown = (NativeMethods.GetAsyncKeyState(0x04) & 0x8000) != 0;

                MouseEvent?.Invoke(this, new MouseEventArgs
                {
                    DeltaX = 0,
                    DeltaY = 0,
                    WheelDelta = wheelDelta,
                    MiddleButtonPressed = middleDown
                });
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Stop()
    {
        _running = false;
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        // Post a dummy message to wake up GetMessage
        NativeMethods.PostThreadMessage((uint)(_hookThread?.ManagedThreadId ?? 0), 0, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        Stop();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
}
```

- [ ] **Step 3: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MouseMapper/NativeMethods.cs MouseMapper/Services/GlobalMouseHook.cs
git commit -m "feat: add GlobalMouseHook with WH_MOUSE_LL low-level hook"
```

---

### Task 7: Implement ViGEmManager

**Files:**
- Create: `MouseMapper/MouseMapper/Services/ViGEmManager.cs`

- [ ] **Step 1: Write ViGEmManager**

`MouseMapper/MouseMapper/Services/ViGEmManager.cs`:

```csharp
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace MouseMapper.Services;

public class ViGEmManager : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private Thread? _updateThread;
    private volatile bool _running;
    private volatile float _steering;   // [-1, 1]
    private volatile float _throttle;   // [0, 1]
    private readonly object _stateLock = new();

    public bool IsConnected => _controller != null;

    public void SetSteering(float value)
    {
        _steering = Math.Clamp(value, -1f, 1f);
    }

    public void SetThrottle(float value)
    {
        _throttle = Math.Clamp(value, 0f, 1f);
    }

    public bool Connect()
    {
        try
        {
            _client = new ViGEmClient();
            _controller = new Xbox360Controller(_client);
            _controller.Connect();
            _running = true;
            _updateThread = new Thread(UpdateLoop)
            {
                Name = "ViGEmUpdateThread",
                IsBackground = true
            };
            _updateThread.Start();
            return true;
        }
        catch (Exception)
        {
            _controller = null;
            _client?.Dispose();
            _client = null;
            return false;
        }
    }

    private void UpdateLoop()
    {
        while (_running)
        {
            if (_controller != null)
            {
                // Convert steering [-1, 1] to left thumb X [-32768, 32767]
                short thumbX = (short)(_steering * 32767);

                // Convert throttle [0, 1] to right trigger [0, 255]
                byte rightTrigger = (byte)(_throttle * 255);

                _controller.SetAxisValue(Xbox360Axes.LeftThumbX, thumbX);
                _controller.SetSliderValue(Xbox360Sliders.RightTrigger, rightTrigger);
            }

            Thread.Sleep(16); // ~60Hz
        }
    }

    public void Disconnect()
    {
        _running = false;
        _updateThread?.Join(500);
        _controller?.Disconnect();
        _controller = null;
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded (Nefarius.ViGEmClient must be available).

- [ ] **Step 3: Commit**

```bash
git add MouseMapper/Services/ViGEmManager.cs
git commit -m "feat: add ViGEmManager wrapping Nefarius.ViGEmClient for virtual Xbox 360 controller"
```

---

### Task 8: Implement InputMapper with tests

**Files:**
- Create: `MouseMapper/MouseMapper/Services/InputMapper.cs`
- Create: `MouseMapper/MouseMapper.Tests/InputMapperTests.cs`

- [ ] **Step 1: Write failing tests**

`MouseMapper/MouseMapper.Tests/InputMapperTests.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;

namespace MouseMapper.Tests;

public class InputMapperTests
{
    [Test]
    public void ProcessMouseMove_Inactive_DoesNotChangeSteering()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = false;

        float initialSteering = mapper.SteeringOutput;
        mapper.ProcessMouseMove(100, 0, 0.016f);
        Assert.That(mapper.SteeringOutput, Is.EqualTo(initialSteering));
    }

    [Test]
    public void ProcessMouseMove_Active_ChangesSteering()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = true;
        mapper.SetSensitivityScale(1000f); // dx=100 → 0.1 normalized

        mapper.ProcessMouseMove(500, 0, 0.016f); // dx=500 → 0.5 normalized
        Assert.That(mapper.SteeringOutput, Is.GreaterThan(0.3f));
    }

    [Test]
    public void ProcessScroll_AccumulatesThrottle()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = true;

        // WHEEL_DELTA is typically 120 per notch
        mapper.ProcessScroll(120, false, 0.016f);
        Assert.That(mapper.ThrottleOutput, Is.GreaterThan(0f));
    }

    [Test]
    public void ProcessScroll_MiddleButton_ResetsThrottle()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = true;

        mapper.ProcessScroll(240, false, 0.016f);
        float beforeReset = mapper.ThrottleOutput;
        Assert.That(beforeReset, Is.GreaterThan(0f));

        mapper.ProcessScroll(0, true, 0.016f);
        Assert.That(mapper.ThrottleOutput, Is.EqualTo(0f));
    }

    [Test]
    public void ProcessScroll_Down_DecreasesThrottle()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = true;

        mapper.ProcessScroll(360, false, 0.016f); // accumulate up
        float up = mapper.ThrottleOutput;

        mapper.ProcessScroll(-120, false, 0.016f); // scroll down
        float after = mapper.ThrottleOutput;
        Assert.That(after, Is.LessThan(up));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: 5 new tests fail — InputMapper class doesn't exist.

- [ ] **Step 3: Implement InputMapper**

`MouseMapper/MouseMapper/Services/InputMapper.cs`:

```csharp
using MouseMapper.Models;

namespace MouseMapper.Services;

public class InputMapper
{
    public volatile bool IsActive;

    private float _steeringOutput;
    private float _throttleOutput;
    private float _prevSteeringOut;
    private float _prevThrottleOut;
    private float _accumulatedScroll;
    private float _sensitivityScale = 1000f;
    private float _scrollScale = 1200f; // 10 notches = full range
    private readonly CurveParameters _steeringParams;
    private readonly CurveParameters _throttleParams;
    private readonly object _lock = new();

    public float SteeringOutput
    {
        get { lock (_lock) return _steeringOutput; }
    }

    public float ThrottleOutput
    {
        get { lock (_lock) return _throttleOutput; }
    }

    public InputMapper(CurveParameters steeringParams, CurveParameters throttleParams)
    {
        _steeringParams = steeringParams;
        _throttleParams = throttleParams;
    }

    public void SetSensitivityScale(float scale) => _sensitivityScale = scale;
    public void SetScrollScale(float scale) => _scrollScale = scale;

    public void ProcessMouseMove(int deltaX, int deltaY, float deltaTime)
    {
        if (!IsActive) return;

        float normalizedDx = Math.Clamp(deltaX / _sensitivityScale, -1f, 1f);
        float absInput = Math.Abs(normalizedDx);
        float sign = Math.Sign(normalizedDx);

        float output = CurveCalculator.Apply(absInput, _steeringParams, ref _prevSteeringOut, deltaTime);
        float steering = output * sign;

        lock (_lock)
        {
            _steeringOutput = steering;
        }
    }

    public void ProcessScroll(int wheelDelta, bool middlePressed, float deltaTime)
    {
        if (!IsActive) return;

        if (middlePressed)
        {
            _accumulatedScroll = 0f;
            lock (_lock)
            {
                _throttleOutput = 0f;
            }
            return;
        }

        _accumulatedScroll += wheelDelta;
        _accumulatedScroll = Math.Clamp(_accumulatedScroll, 0f, _scrollScale);

        float normalized = _accumulatedScroll / _scrollScale;

        float output = CurveCalculator.Apply(normalized, _throttleParams, ref _prevThrottleOut, deltaTime);

        lock (_lock)
        {
            _throttleOutput = output;
        }
    }

    public void ResetThrottle()
    {
        _accumulatedScroll = 0f;
        lock (_lock)
        {
            _throttleOutput = 0f;
        }
    }

    public void UpdateParameters(CurveParameters newSteering, CurveParameters newThrottle)
    {
        // Copy properties into the live parameter objects used by Apply().
        // Since the params are passed by ref through _prev*Out, we update in place.
        lock (_lock)
        {
            _steeringParams.Deadzone = newSteering.Deadzone;
            _steeringParams.LowSlope = newSteering.LowSlope;
            _steeringParams.KneePoint = newSteering.KneePoint;
            _steeringParams.HighSlope = newSteering.HighSlope;
            _steeringParams.SmoothingMs = newSteering.SmoothingMs;

            _throttleParams.Deadzone = newThrottle.Deadzone;
            _throttleParams.LowSlope = newThrottle.LowSlope;
            _throttleParams.KneePoint = newThrottle.KneePoint;
            _throttleParams.HighSlope = newThrottle.HighSlope;
            _throttleParams.SmoothingMs = newThrottle.SmoothingMs;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj`
Expected: All 19 tests pass.

- [ ] **Step 5: Commit**

```bash
git add MouseMapper/Services/InputMapper.cs MouseMapper.Tests/InputMapperTests.cs
git commit -m "feat: add InputMapper with mouse-to-axis mapping logic and tests"
```

---

### Task 9: Implement tray icon and application entry point

**Files:**
- Modify: `MouseMapper/MouseMapper/App.xaml` — remove StartupUri
- Create: `MouseMapper/MouseMapper/App.xaml.cs`

- [ ] **Step 1: Write App.xaml (no StartupUri — we start from code)**

`MouseMapper/MouseMapper/App.xaml`:

```xml
<Application x:Class="MouseMapper.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Write App.xaml.cs with tray icon and startup logic**

`MouseMapper/MouseMapper/App.xaml.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;
using MouseMapper.Views;
using System.Windows;

namespace MouseMapper;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private GlobalMouseHook? _mouseHook;
    private ViGEmManager? _viGEm;
    private InputMapper? _mapper;
    private ConfigManager? _configManager;
    private AppConfig? _config;
    private OsdWindow? _osd;
    private SettingsWindow? _settings;

    private volatile bool _isActive;
    private DateTime _lastInputTime = DateTime.UtcNow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MouseMapper");
        _configManager = new ConfigManager(configDir);
        _config = _configManager.Load();

        _mapper = new InputMapper(_config.Steering, _config.Throttle);

        _viGEm = new ViGEmManager();
        var connected = _viGEm.Connect();

        SetupTrayIcon();
        SetupMouseHook();

        if (_config.Osd.Enabled)
            ShowOsd();

        if (!connected)
        {
            _trayIcon?.ShowBalloonTip(3000, "MouseMapper",
                "ViGEm driver not found. Virtual controller unavailable.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "MouseMapper",
            Visible = true
        };

        // Use a simple embedded icon or system icon
        using var stream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("MouseMapper.app.ico");
        _trayIcon.Icon = stream != null
            ? new System.Drawing.Icon(stream)
            : System.Drawing.SystemIcons.Application;

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var toggleItem = new System.Windows.Forms.ToolStripMenuItem("Active")
        {
            Checked = false,
            CheckOnClick = true
        };
        toggleItem.Click += (s, e) =>
        {
            _isActive = !_isActive;
            toggleItem.Checked = _isActive;
            if (_mapper != null) _mapper.IsActive = _isActive;
            if (_osd != null) _osd.IsActive = _isActive;
        };

        var osdItem = new System.Windows.Forms.ToolStripMenuItem("OSD")
        {
            Checked = _config?.Osd.Enabled ?? true,
            CheckOnClick = true
        };
        osdItem.Click += (s, e) => ToggleOsd();

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings", null,
            (s, e) => ShowSettings());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit", null,
            (s, e) => Shutdown());

        contextMenu.Items.Add(toggleItem);
        contextMenu.Items.Add(osdItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = contextMenu;
    }

    private void SetupMouseHook()
    {
        _mouseHook = new GlobalMouseHook();
        _mouseHook.MouseEvent += OnMouseEvent;
        _mouseHook.Start();

        // Start a 60Hz timer to compute inputs and push to ViGEm
        var inputTimer = new System.Timers.Timer(16) { AutoReset = true };
        inputTimer.Elapsed += (s, e) => ProcessInputFrame();
        inputTimer.Start();
    }

    private int _pendingDx, _pendingDy, _pendingWheel;
    private bool _pendingMiddle;
    private readonly object _pendingLock = new();

    private void OnMouseEvent(object? sender, MouseEventArgs e)
    {
        lock (_pendingLock)
        {
            _pendingWheel += e.WheelDelta;
            if (e.MiddleButtonPressed) _pendingMiddle = true;
        }
        _lastInputTime = DateTime.UtcNow;
    }

    private void ProcessInputFrame()
    {
        if (_mapper == null || _viGEm == null) return;

        float dt = 0.016f;

        int wheel;
        bool middle;
        lock (_pendingLock)
        {
            wheel = _pendingWheel;
            middle = _pendingMiddle;
            _pendingWheel = 0;
            _pendingMiddle = false;
        }

        // For mouse movement, use raw input via GetCursorPos delta
        // (simplified: we use hook-based wheel/buttons, 
        //  and a raw input approach for movement)
        // The hook captures moves; we compute dx from last known position

        _mapper.ProcessScroll(wheel, middle, dt);
        _viGEm.SetSteering(_mapper.SteeringOutput);
        _viGEm.SetThrottle(_mapper.ThrottleOutput);
    }

    private void ShowOsd()
    {
        if (_osd != null) return;
        _osd = new OsdWindow(_mapper!, _config!.Osd);
        _osd.Closed += (s, e) => _osd = null;
        _osd.Show();
    }

    private void HideOsd()
    {
        _osd?.Close();
        _osd = null;
    }

    private void ToggleOsd()
    {
        if (_osd != null) HideOsd(); else ShowOsd();
    }

    private void ShowSettings()
    {
        if (_settings != null)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow(_config!, _mapper!);
        _settings.ConfigSaved += OnConfigSaved;
        _settings.Closed += (s, e) => _settings = null;
        _settings.Show();
    }

    private void OnConfigSaved(object? sender, AppConfig config)
    {
        _config = config;
        _configManager?.Save(config);
        _mapper?.UpdateParameters(config.Steering, config.Throttle);
        if (_osd != null)
        {
            _osd.UpdateConfig(config.Osd);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mouseHook?.Dispose();
        _viGEm?.Dispose();
        _osd?.Close();
        _settings?.Close();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 2: Add System.Windows.Forms reference and System.Timers to csproj**

`MouseMapper/MouseMapper/MouseMapper.csproj` needs these additions. Open the file and ensure it contains:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Nefarius.ViGEmClient" Version="1.21.256" />
  </ItemGroup>
</Project>
```

Edit: `MouseMapper/MouseMapper/MouseMapper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Nefarius.ViGEmClient" Version="1.21.256" />
  </ItemGroup>
</Project>
```

`UseWindowsForms` enables `System.Windows.Forms.NotifyIcon` and `System.Timers.Timer`.

- [ ] **Step 3: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded. May warn about missing app.ico — that's fine for now.

- [ ] **Step 4: Commit**

```bash
git add MouseMapper/App.xaml MouseMapper/App.xaml.cs MouseMapper/MouseMapper.csproj
git commit -m "feat: add tray icon, startup logic, and application entry point"
```

---

### Task 10: Implement OSD overlay window

**Files:**
- Create: `MouseMapper/MouseMapper/Views/OsdWindow.xaml`
- Create: `MouseMapper/MouseMapper/Views/OsdWindow.xaml.cs`

- [ ] **Step 1: Write OsdWindow XAML**

`MouseMapper/MouseMapper/Views/OsdWindow.xaml`:

```xml
<Window x:Class="MouseMapper.Views.OsdWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        Width="220" Height="160">

    <Window.Resources>
        <SolidColorBrush x:Key="OsdBg" Color="#CC1A1A2E" />
        <SolidColorBrush x:Key="OsdGreen" Color="#FF4ECDC4" />
        <SolidColorBrush x:Key="OsdGray" Color="#FF555555" />
        <SolidColorBrush x:Key="OsdText" Color="#FFFFFFFF" />
    </Window.Resources>

    <Grid>
        <!-- Drag handle -->
        <Border x:Name="DragHandle"
                Height="16"
                VerticalAlignment="Top"
                Background="#44FFFFFF"
                MouseLeftButtonDown="DragHandle_MouseLeftButtonDown" />

        <!-- Main content -->
        <Border Background="{StaticResource OsdBg}"
                CornerRadius="6"
                Margin="4,16,4,4"
                Padding="10">

            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <!-- Active indicator -->
                <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="0,0,0,6">
                    <Ellipse x:Name="ActiveDot" Width="8" Height="8" Fill="{StaticResource OsdGray}" Margin="0,0,6,0" />
                    <TextBlock x:Name="StatusText" Text="INACTIVE" Foreground="{StaticResource OsdGray}"
                               FontSize="11" FontFamily="Segoe UI" FontWeight="SemiBold" />
                    <TextBlock x:Name="KeyHint" Text="  Press ~ to toggle"
                               Foreground="{StaticResource OsdGray}" FontSize="10"
                               FontFamily="Segoe UI" Margin="8,0,0,0" />
                </StackPanel>

                <!-- Steering bar -->
                <Grid Grid.Row="1" Margin="0,4,0,4">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="52" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="40" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="STEER" Foreground="{StaticResource OsdText}"
                               FontSize="10" VerticalAlignment="Center" />
                    <Rectangle Grid.Column="1" Height="10" RadiusX="3" RadiusY="3"
                               Fill="#33FFFFFF" Stroke="#55FFFFFF" StrokeThickness="0.5" />
                    <Rectangle x:Name="SteeringFill" Grid.Column="1" Height="10" RadiusX="3" RadiusY="3"
                               Fill="{StaticResource OsdGreen}" Width="50"
                               HorizontalAlignment="Center" VerticalAlignment="Center" />
                    <TextBlock x:Name="SteeringText" Grid.Column="2" Text="0%"
                               Foreground="{StaticResource OsdText}" FontSize="10"
                               TextAlignment="Right" VerticalAlignment="Center" />
                </Grid>

                <!-- Throttle bar -->
                <Grid Grid.Row="2" Margin="0,4,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="52" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="40" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="THR" Foreground="{StaticResource OsdText}"
                               FontSize="10" VerticalAlignment="Center" />
                    <Rectangle Grid.Column="1" Height="10" RadiusX="3" RadiusY="3"
                               Fill="#33FFFFFF" Stroke="#55FFFFFF" StrokeThickness="0.5" />
                    <Rectangle x:Name="ThrottleFill" Grid.Column="1" Height="10" RadiusX="3" RadiusY="3"
                               Fill="#FFFF6B6B" Width="0"
                               HorizontalAlignment="Left" VerticalAlignment="Center" />
                    <TextBlock x:Name="ThrottleText" Grid.Column="2" Text="0%"
                               Foreground="{StaticResource OsdText}" FontSize="10"
                               TextAlignment="Right" VerticalAlignment="Center" />
                </Grid>
            </Grid>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Write OsdWindow code-behind**

`MouseMapper/MouseMapper/Views/OsdWindow.xaml.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MouseMapper.Views;

public partial class OsdWindow : Window
{
    private readonly InputMapper _mapper;
    private OsdConfig _config;
    private DispatcherTimer? _timer;
    private bool _isDragHandleHovered;

    public bool IsActive
    {
        set
        {
            Dispatcher.Invoke(() =>
            {
                ActiveDot.Fill = value
                    ? new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0xC4))
                    : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                StatusText.Text = value ? "ACTIVE" : "INACTIVE";
                StatusText.Foreground = value
                    ? new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0xC4))
                    : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                KeyHint.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            });
        }
    }

    public OsdWindow(InputMapper mapper, OsdConfig config)
    {
        InitializeComponent();
        _mapper = mapper;
        _config = config;

        ApplyConfig(config);

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render,
            (s, e) => UpdateDisplay(), Dispatcher);
        _timer.Start();

        // Make window click-through except for drag handle
        this.Loaded += (s, e) => SetClickThrough(true);
        DragHandle.MouseEnter += (s, e) => { _isDragHandleHovered = true; SetClickThrough(false); };
        DragHandle.MouseLeave += (s, e) => { _isDragHandleHovered = false; SetClickThrough(true); };
    }

    private void SetClickThrough(bool clickThrough)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = NativeMethods.GetWindowLong(hwnd, -20); // GWL_EXSTYLE
        if (clickThrough)
            exStyle |= 0x00000020; // WS_EX_TRANSPARENT
        else
            exStyle &= ~0x00000020;
        NativeMethods.SetWindowLong(hwnd, -20, exStyle);
    }

    private void UpdateDisplay()
    {
        float steering = _mapper.SteeringOutput;
        float throttle = _mapper.ThrottleOutput;

        // Steering: map [-1,1] to bar fill. Center = 50% width, left = less, right = more
        double barWidth = SteeringFill.Parent is FrameworkElement parent ? parent.ActualWidth - 92 : 140;
        double centerX = barWidth / 2;
        double offset = steering * centerX;
        SteeringFill.Width = barWidth * 0.5 + offset;
        SteeringFill.HorizontalAlignment = HorizontalAlignment.Center;
        SteeringText.Text = $"{(int)(steering * 100)}%";

        // Throttle: 0 to 1 mapped to bar width
        ThrottleFill.Width = throttle * barWidth;
        ThrottleFill.HorizontalAlignment = HorizontalAlignment.Left;
        ThrottleText.Text = $"{(int)(throttle * 100)}%";
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    public void ApplyConfig(OsdConfig config)
    {
        _config = config;
        var scale = config.Scale;
        this.Width = 220 * scale;
        this.Height = 160 * scale;

        // Position from bottom-right
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen != null)
        {
            this.Left = screen.WorkingArea.Right - this.Width - 40 + config.PositionX;
            this.Top = screen.WorkingArea.Bottom - this.Height - 40 + config.PositionY;
        }
    }

    public void UpdateConfig(OsdConfig config)
    {
        ApplyConfig(config);
    }
}
```

- [ ] **Step 3: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MouseMapper/Views/OsdWindow.xaml MouseMapper/Views/OsdWindow.xaml.cs
git commit -m "feat: add OSD overlay window with steering/throttle display"
```

---

### Task 11: Implement CurvePreviewControl

**Files:**
- Create: `MouseMapper/MouseMapper/Views/Controls/CurvePreviewControl.xaml`
- Create: `MouseMapper/MouseMapper/Views/Controls/CurvePreviewControl.xaml.cs`

- [ ] **Step 1: Write CurvePreviewControl XAML**

`MouseMapper/MouseMapper/Views/Controls/CurvePreviewControl.xaml`:

```xml
<UserControl x:Class="MouseMapper.Views.Controls.CurvePreviewControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Height="200" SizeChanged="OnSizeChanged">
    <Border Background="#FF1E1E2E" CornerRadius="4" Padding="4">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>
            <TextBlock Text="Curve Preview" FontSize="10" Foreground="#888"
                       Margin="4,2,0,4" />
            <Canvas x:Name="Canvas" Grid.Row="1" Background="#FF252540"
                    ClipToBounds="True" />
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 2: Write CurvePreviewControl code-behind**

`MouseMapper/MouseMapper/Views/Controls/CurvePreviewControl.xaml.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MouseMapper.Views.Controls;

public partial class CurvePreviewControl : UserControl
{
    private float _deadzone = 0.03f;
    private float _lowSlope = 0.3f;
    private float _kneePoint = 0.35f;
    private float _highSlope = 2.5f;
    private int _smoothingMs;

    public void UpdateParameters(float deadzone, float lowSlope, float kneePoint, float highSlope, int smoothingMs)
    {
        _deadzone = deadzone;
        _lowSlope = lowSlope;
        _kneePoint = kneePoint;
        _highSlope = highSlope;
        _smoothingMs = smoothingMs;
        DrawCurve();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCurve();
    }

    private void DrawCurve()
    {
        Canvas.Children.Clear();

        double w = Canvas.ActualWidth;
        double h = Canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double margin = 8;
        double graphW = w - 2 * margin;
        double graphH = h - 2 * margin;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x55));

        for (int i = 1; i < 4; i++)
        {
            double y = margin + graphH * i / 4;
            Canvas.Children.Add(new Line { X1 = margin, Y1 = y, X2 = margin + graphW, Y2 = y, Stroke = gridBrush, StrokeThickness = 0.5 });
            double x = margin + graphW * i / 4;
            Canvas.Children.Add(new Line { X1 = x, Y1 = margin, X2 = x, Y2 = margin + graphH, Stroke = gridBrush, StrokeThickness = 0.5 });
        }

        var p = new CurveParameters
        {
            Deadzone = _deadzone,
            LowSlope = _lowSlope,
            KneePoint = _kneePoint,
            HighSlope = _highSlope,
            SmoothingMs = _smoothingMs
        };

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0xC4)),
            StrokeThickness = 2
        };

        float prev = 0f;
        for (int i = 0; i <= 100; i++)
        {
            float input = i / 100f;
            float output = CurveCalculator.Apply(input, p, ref prev, 0f);

            double px = margin + input * graphW;
            double py = margin + graphH - output * graphH;
            polyline.Points.Add(new Point(px, py));
        }

        Canvas.Children.Add(polyline);
    }
}
```

- [ ] **Step 3: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MouseMapper/Views/Controls/CurvePreviewControl.xaml MouseMapper/Views/Controls/CurvePreviewControl.xaml.cs
git commit -m "feat: add CurvePreviewControl with real-time curve rendering"
```

---

### Task 12: Implement SettingsWindow with ViewModel

**Files:**
- Create: `MouseMapper/MouseMapper/ViewModels/RelayCommand.cs`
- Create: `MouseMapper/MouseMapper/ViewModels/SettingsViewModel.cs`
- Create: `MouseMapper/MouseMapper/Views/SettingsWindow.xaml`
- Create: `MouseMapper/MouseMapper/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Write RelayCommand**

`MouseMapper/MouseMapper/ViewModels/RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace MouseMapper.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
}
```

- [ ] **Step 2: Write SettingsViewModel**

`MouseMapper/MouseMapper/ViewModels/SettingsViewModel.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MouseMapper.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppConfig _config;
    private readonly InputMapper _mapper;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppConfig>? ConfigSaved;

    // Steering properties
    public float SteeringDeadzone { get => _config.Steering.Deadzone * 100; set { _config.Steering.Deadzone = value / 100; OnChanged(); } }
    public float SteeringLowSlope { get => _config.Steering.LowSlope; set { _config.Steering.LowSlope = value; OnChanged(); } }
    public float SteeringKneePoint { get => _config.Steering.KneePoint * 100; set { _config.Steering.KneePoint = value / 100; OnChanged(); } }
    public float SteeringHighSlope { get => _config.Steering.HighSlope; set { _config.Steering.HighSlope = value; OnChanged(); } }
    public int SteeringSmoothing { get => _config.Steering.SmoothingMs; set { _config.Steering.SmoothingMs = value; OnChanged(); } }

    // Throttle properties
    public float ThrottleDeadzone { get => _config.Throttle.Deadzone * 100; set { _config.Throttle.Deadzone = value / 100; OnChanged(); } }
    public float ThrottleLowSlope { get => _config.Throttle.LowSlope; set { _config.Throttle.LowSlope = value; OnChanged(); } }
    public float ThrottleKneePoint { get => _config.Throttle.KneePoint * 100; set { _config.Throttle.KneePoint = value / 100; OnChanged(); } }
    public float ThrottleHighSlope { get => _config.Throttle.HighSlope; set { _config.Throttle.HighSlope = value; OnChanged(); } }
    public int ThrottleSmoothing { get => _config.Throttle.SmoothingMs; set { _config.Throttle.SmoothingMs = value; OnChanged(); } }

    // Activation
    public string ToggleKey { get => _config.Activation.ToggleKey; set { _config.Activation.ToggleKey = value; OnChanged(); } }
    public string ThrottleResetButton { get => _config.Activation.ThrottleResetButton; set { _config.Activation.ThrottleResetButton = value; OnChanged(); } }

    // OSD
    public bool OsdEnabled { get => _config.Osd.Enabled; set { _config.Osd.Enabled = value; OnChanged(); } }
    public double OsdScale { get => _config.Osd.Scale; set { _config.Osd.Scale = value; OnChanged(); } }

    // Commands
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetCommand { get; }

    private readonly AppConfig _originalConfig;

    public SettingsViewModel(AppConfig config, InputMapper mapper)
    {
        _config = config;
        _mapper = mapper;
        _originalConfig = CloneConfig(config);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        ResetCommand = new RelayCommand(ResetToDefaults);
    }

    private void OnChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        // Live-preview: push params to mapper
        _mapper.UpdateParameters(_config.Steering, _config.Throttle);
    }

    private void Save()
    {
        ConfigSaved?.Invoke(this, _config);
    }

    private void Cancel()
    {
        // Restore from original copy
        _config.Steering = _originalConfig.Steering.Clone();
        _config.Throttle = _originalConfig.Throttle.Clone();
        _config.Activation = new ActivationConfig
        {
            ToggleKey = _originalConfig.Activation.ToggleKey,
            ThrottleResetButton = _originalConfig.Activation.ThrottleResetButton
        };
        _config.Osd = new OsdConfig
        {
            Enabled = _originalConfig.Osd.Enabled,
            Scale = _originalConfig.Osd.Scale,
            PositionX = _originalConfig.Osd.PositionX,
            PositionY = _originalConfig.Osd.PositionY
        };
        _mapper.UpdateParameters(_config.Steering, _config.Throttle);
        RefreshAllProperties();
    }

    private void ResetToDefaults()
    {
        var defaults = new AppConfig();
        _config.Steering = defaults.Steering;
        _config.Throttle = defaults.Throttle;
        _config.Activation = defaults.Activation;
        _config.Osd = defaults.Osd;
        _mapper.UpdateParameters(_config.Steering, _config.Throttle);
        RefreshAllProperties();
    }

    private void RefreshAllProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static AppConfig CloneConfig(AppConfig src) => new()
    {
        Steering = src.Steering.Clone(),
        Throttle = src.Throttle.Clone(),
        Activation = new ActivationConfig
        {
            ToggleKey = src.Activation.ToggleKey,
            ThrottleResetButton = src.Activation.ThrottleResetButton
        },
        Osd = new OsdConfig
        {
            Enabled = src.Osd.Enabled,
            Scale = src.Osd.Scale,
            PositionX = src.Osd.PositionX,
            PositionY = src.Osd.PositionY
        }
    };
}
```

- [ ] **Step 3: Write SettingsWindow XAML**

`MouseMapper/MouseMapper/Views/SettingsWindow.xaml`:

```xml
<Window x:Class="MouseMapper.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:MouseMapper.Views.Controls"
        Title="MouseMapper Settings"
        Width="600" Height="480"
        WindowStartupLocation="CenterScreen"
        ResizeMode="CanMinimize">

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="140" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>

        <!-- Sidebar -->
        <ListBox x:Name="NavList" Grid.Column="0" Background="#FF1E1E2E"
                 Foreground="White" BorderThickness="0">
            <ListBoxItem Content="Key Bindings" Padding="12,8" />
            <ListBoxItem Content="Steering Curve" Padding="12,8" />
            <ListBoxItem Content="Throttle Curve" Padding="12,8" />
            <ListBoxItem Content="OSD" Padding="12,8" />
            <ListBoxItem Content="About" Padding="12,8" />
        </ListBox>

        <!-- Pages via ContentControl -->
        <Grid Grid.Column="1" Margin="16">
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <ContentControl x:Name="PageContent" Grid.Row="0" />

            <StackPanel Grid.Row="1" Orientation="Horizontal"
                        HorizontalAlignment="Right" Margin="0,12,0,0">
                <Button Content="Reset Defaults" Margin="0,0,8,0"
                        Command="{Binding ResetCommand}" Width="100" Height="28" />
                <Button Content="Cancel" Margin="0,0,8,0"
                        Command="{Binding CancelCommand}" Width="60" Height="28" />
                <Button Content="Save" Command="{Binding SaveCommand}"
                        Width="60" Height="28"
                        Background="#FF4ECDC4" Foreground="Black" FontWeight="SemiBold" />
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 4: Write SettingsWindow code-behind**

`MouseMapper/MouseMapper/Views/SettingsWindow.xaml.cs`:

```csharp
using MouseMapper.Models;
using MouseMapper.Services;
using MouseMapper.ViewModels;
using MouseMapper.Views.Controls;
using System.Windows;
using System.Windows.Controls;

namespace MouseMapper.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppConfig _config;
    private CurvePreviewControl? _steeringPreview;
    private CurvePreviewControl? _throttlePreview;

    public event EventHandler<AppConfig>? ConfigSaved;

    public SettingsWindow(AppConfig config, InputMapper mapper)
    {
        InitializeComponent();
        _config = config;
        _vm = new SettingsViewModel(config, mapper);
        _vm.ConfigSaved += (s, cfg) =>
        {
            ConfigSaved?.Invoke(this, cfg);
            this.Close();
        };
        DataContext = _vm;

        NavList.SelectionChanged += (s, e) => ShowPage(NavList.SelectedIndex);
        NavList.SelectedIndex = 0;
    }

    private void ShowPage(int index)
    {
        PageContent.Content = index switch
        {
            0 => CreateKeyBindingsPage(),
            1 => CreateCurvePage("Steering", _config.Steering, p => _steeringPreview = p),
            2 => CreateCurvePage("Throttle", _config.Throttle, p => _throttlePreview = p),
            3 => CreateOsdPage(),
            4 => CreateAboutPage(),
            _ => null
        };
    }

    private FrameworkElement CreateKeyBindingsPage()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Key Bindings", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 12) });

        stack.Children.Add(CreateLabeledTextBox("Toggle Key:", "{Binding ToggleKey}"));
        stack.Children.Add(CreateLabeledTextBox("Throttle Reset Button:", "{Binding ThrottleResetButton}"));

        stack.Children.Add(new TextBlock
        {
            Text = "Click the field and press the desired key/button to bind it.",
            FontSize = 11,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return stack;
    }

    private FrameworkElement CreateCurvePage(string title, CurveParameters cp, Action<CurvePreviewControl> setPreview)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.Children.Add(new TextBlock { Text = $"{title} Curve", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 8) });

        var preview = new CurvePreviewControl();
        Grid.SetRow(preview, 1);
        setPreview(preview);
        grid.Children.Add(preview);

        var sliders = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(sliders, 2);

        string prefix = title.ToLower() == "steering" ? "Steering" : "Throttle";

        sliders.Children.Add(CreateSlider($"Deadzone: {{0}}%", $"{{{prefix}Deadzone}}", 0, 20, v => preview.UpdateParameters(v / 100, cp.LowSlope, cp.KneePoint, cp.HighSlope, cp.SmoothingMs)));
        sliders.Children.Add(CreateSlider($"Low Slope: {{0:F1}}x", $"{{{prefix}LowSlope}}", 0.1, title == "Steering" ? 1.0 : 3.0, v => preview.UpdateParameters(cp.Deadzone, v, cp.KneePoint, cp.HighSlope, cp.SmoothingMs)));
        sliders.Children.Add(CreateSlider($"Knee Point: {{0}}%", $"{{{prefix}KneePoint}}", 10, 90, v => preview.UpdateParameters(cp.Deadzone, cp.LowSlope, v / 100, cp.HighSlope, cp.SmoothingMs)));
        sliders.Children.Add(CreateSlider($"High Slope: {{0:F1}}x", $"{{{prefix}HighSlope}}", title == "Steering" ? 1.0 : 0.1, title == "Steering" ? 4.0 : 1.0, v => preview.UpdateParameters(cp.Deadzone, cp.LowSlope, cp.KneePoint, v, cp.SmoothingMs)));
        sliders.Children.Add(CreateSlider($"Smoothing: {{0}}ms", $"{{{prefix}Smoothing}}", 0, 200, v => preview.UpdateParameters(cp.Deadzone, cp.LowSlope, cp.KneePoint, cp.HighSlope, (int)v)));

        grid.Children.Add(sliders);
        return grid;
    }

    private FrameworkElement CreateOsdPage()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "OSD Settings", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 12) });

        var cb = new CheckBox { Content = "Enable OSD Overlay", Foreground = System.Windows.Media.Brushes.White };
        cb.SetBinding(CheckBox.IsCheckedProperty, "{Binding OsdEnabled}");
        stack.Children.Add(cb);

        stack.Children.Add(CreateSlider("Scale: {0:F1}x", "{Binding OsdScale}", 0.5, 3.0, null));

        return stack;
    }

    private FrameworkElement CreateAboutPage()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "About", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 12) });

        stack.Children.Add(new TextBlock { Text = "MouseMapper v1.0", Foreground = System.Windows.Media.Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
        stack.Children.Add(new TextBlock { Text = "Maps mouse input to virtual Xbox 360 controller via ViGEm.", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)), FontSize = 11, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap });

        bool driverOk = false;
        try { using var client = new Nefarius.ViGEm.Client.ViGEmClient(); driverOk = true; } catch { }

        stack.Children.Add(new TextBlock
        {
            Text = driverOk ? "ViGEm Driver: CONNECTED" : "ViGEm Driver: NOT FOUND",
            Foreground = driverOk
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4E, 0xCD, 0xC4))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        });

        if (!driverOk)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Install ViGEmBus from: https://github.com/nefarius/ViGEmBus/releases",
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }

        return stack;
    }

    private static FrameworkElement CreateLabeledTextBox(string label, string binding)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        var tb = new TextBox { Width = 120, Height = 26, HorizontalAlignment = HorizontalAlignment.Left };
        tb.SetBinding(TextBox.TextProperty, binding);
        stack.Children.Add(tb);
        return stack;
    }

    private static FrameworkElement CreateSlider(string format, string binding, double min, double max, Action<double>? onValueChanged)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock { Foreground = System.Windows.Media.Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 140 };
        var slider = new Slider { Minimum = min, Maximum = max, Margin = new Thickness(8, 0, 8, 0) };
        var value = new TextBlock { Foreground = System.Windows.Media.Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 50, TextAlignment = TextAlignment.Right };

        slider.SetBinding(Slider.ValueProperty, binding);
        value.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(binding) { StringFormat = format.Replace("{0}", "{0:F0}").Replace("{0:F1}", "{0:F1}") });

        if (onValueChanged != null)
        {
            slider.ValueChanged += (s, e) => onValueChanged(e.NewValue);
        }

        Grid.SetColumn(label, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(value, 2);
        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(value);

        return grid;
    }
}
```

- [ ] **Step 5: Build to verify no errors**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add MouseMapper/ViewModels/ MouseMapper/Views/SettingsWindow.xaml MouseMapper/Views/SettingsWindow.xaml.cs
git commit -m "feat: add SettingsWindow with curve editor, key bindings, OSD, and About pages"
```

---

### Task 13: Fix mouse movement input and final integration

**Files:**
- Modify: `MouseMapper/MouseMapper/App.xaml.cs`
- Modify: `MouseMapper/MouseMapper/Services/GlobalMouseHook.cs`

**Purpose:** The current GlobalMouseHook doesn't capture mouse movement deltas properly for steering — it needs to use raw input or GetCursorPos deltas. Fix the hook and wire the movement through InputMapper.

- [ ] **Step 1: Update GlobalMouseHook to track cursor position deltas**

Modify `MouseMapper/MouseMapper/Services/GlobalMouseHook.cs`. Add cursor tracking fields and a method to get deltas:

In the class, add:

```csharp
private int _lastCursorX;
private int _lastCursorY;
private bool _hasLastCursor;

public (int dx, int dy) GetCursorDelta()
{
    var pos = GetCursorPos();
    int dx = 0, dy = 0;
    if (_hasLastCursor)
    {
        dx = pos.x - _lastCursorX;
        dy = pos.y - _lastCursorY;
    }
    _lastCursorX = pos.x;
    _lastCursorY = pos.y;
    _hasLastCursor = true;
    return (dx, dy);
}

private static (int x, int y) GetCursorPos()
{
    POINT pt;
    GetCursorPos(out pt);
    return (pt.x, pt.y);
}

[DllImport("user32.dll")]
private static extern bool GetCursorPos(out POINT lpPoint);
```

- [ ] **Step 2: Update App.xaml.cs ProcessInputFrame to use cursor deltas**

In `MouseMapper/MouseMapper/App.xaml.cs`, update `ProcessInputFrame()`:

```csharp
private void ProcessInputFrame()
{
    if (_mapper == null || _viGEm == null) return;
    if (!_isActive) return;

    float dt = 0.016f;

    // Get cursor movement delta
    var (dx, dy) = _mouseHook?.GetCursorDelta() ?? (0, 0);

    int wheel;
    bool middle;
    lock (_pendingLock)
    {
        wheel = _pendingWheel;
        middle = _pendingMiddle;
        _pendingWheel = 0;
        _pendingMiddle = false;
    }

    _mapper.ProcessMouseMove(dx, dy, dt);
    _mapper.ProcessScroll(wheel, middle, dt);

    _viGEm.SetSteering(_mapper.SteeringOutput);
    _viGEm.SetThrottle(_mapper.ThrottleOutput);
}
```

Also update the toggle behavior in `SetupTrayIcon` to handle the toggle properly:

Replace the toggle Click handler in SetupTrayIcon:

```csharp
toggleItem.Click += (s, e) =>
{
    _isActive = toggleItem.Checked;
    if (_mapper != null) _mapper.IsActive = _isActive;
    if (_osd != null) _osd.IsActive = _isActive;
};
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build MouseMapper/MouseMapper.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add MouseMapper/App.xaml.cs MouseMapper/Services/GlobalMouseHook.cs
git commit -m "fix: wire cursor delta tracking for mouse movement input"
```

---

### Task 14: Final integration build and smoke test

**Files:** None (verification)

- [ ] **Step 1: Full build solution**

```bash
cd E:/AI/Claude\ Code/MouseMapper
dotnet build MouseMapper.sln
```

Expected: Build succeeded, no errors, no warnings (or minor warnings about unused fields).

- [ ] **Step 2: Run all unit tests**

```bash
dotnet test MouseMapper.Tests/MouseMapper.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 3: Manual smoke test checklist**

1. Run `dotnet run --project MouseMapper/MouseMapper.csproj`
2. Verify: tray icon appears in system notification area
3. Right-click tray icon → "Settings" → Settings window opens
4. Navigate to "Steering Curve" page → adjust sliders → curve preview updates
5. Navigate to "Throttle Curve" page → same verification
6. Navigate to "About" page → ViGEm driver status shown (connected or not installed)
7. Click "Save" → settings window closes
8. Right-click tray → toggle "Active" → OSD shows (if ViGEm available) or warning bubble shows
9. Right-click tray → "Exit" → application exits cleanly

- [ ] **Step 4: Commit any final fixes**

```bash
git add -A
git commit -m "chore: final integration build and test verification"
```

---

### Task 15: Create a batch launcher for convenience

**Files:**
- Create: `MouseMapper/run.bat`

- [ ] **Step 1: Write run.bat**

`MouseMapper/run.bat`:

```bat
@echo off
cd /d "%~dp0"
dotnet run --project MouseMapper/MouseMapper.csproj
```

- [ ] **Step 2: Commit**

```bash
git add MouseMapper/run.bat
git commit -m "chore: add run.bat for quick launch"
```
