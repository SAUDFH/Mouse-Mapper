using MouseMapper.Models;
using MouseMapper.Services;
using MouseMapper.Views;
using System.IO;
using System.Windows;
using Timer = System.Timers.Timer;
using WpfApplication = System.Windows.Application;

namespace MouseMapper;

public partial class App : WpfApplication
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _toggleMenuItem;
    private GlobalMouseHook? _mouseHook;
    private GlobalKeyboardHook? _keyboardHook;
    private ViGEmManager? _viGEm;
    private InputMapper? _mapper;
    private ConfigManager? _configManager;
    private AppConfig? _config;
    private OsdWindow? _osd;
    private SettingsWindow? _settings;

    private volatile bool _isActive;
    private int _pendingWheel;
    private bool _pendingMiddle;
    private bool _pendingGearUp;
    private bool _pendingGearDown;
    private int _steeringResetVk;
    private int _gearUpVk;
    private int _gearDownVk;
    private float _centerOffset;
    private bool _altWasDown;
    private readonly object _pendingLock = new();

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
        SetupKeyboardHook();

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
        _toggleMenuItem = toggleItem;
        toggleItem.Click += (s, e) =>
        {
            _isActive = toggleItem.Checked;
            if (_mapper != null) _mapper.IsActive = _isActive;
            if (_osd != null) _osd.SetActive(_isActive);
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

        var inputTimer = new Timer(16) { AutoReset = true };
        inputTimer.Elapsed += (s, e) => ProcessInputFrame();
        inputTimer.Start();
    }

    private void SetupKeyboardHook()
    {
        _keyboardHook = new GlobalKeyboardHook();
        int toggleVk = ParseKeyName(_config!.Activation.ToggleKey);
        _steeringResetVk = ParseKeyName(_config.Activation.SteeringResetKey);
        _gearUpVk = ParseKeyName(_config.Activation.GearUpKey);
        _gearDownVk = ParseKeyName(_config.Activation.GearDownKey);

        _keyboardHook.KeyDown += (s, vkCode) =>
        {
            if (vkCode == toggleVk)
            {
                _isActive = !_isActive;
                _mapper!.IsActive = _isActive;
                Dispatcher.InvokeAsync(() =>
                {
                    _osd?.SetActive(_isActive);
                    if (_toggleMenuItem != null) _toggleMenuItem.Checked = _isActive;
                });
            }

            if (vkCode == _gearUpVk)
                _pendingGearUp = true;

            if (vkCode == _gearDownVk)
                _pendingGearDown = true;
        };

        _keyboardHook.Start();
    }

    private static int ParseKeyName(string name)
    {
        if (Enum.TryParse<System.Windows.Forms.Keys>(name, out var key))
            return (int)key;
        return 0xC0; // default to Oem3 (~)
    }

    private void OnMouseEvent(object? sender, GlobalMouseHook.MouseEventArgs e)
    {
        lock (_pendingLock)
        {
            _pendingWheel += e.WheelDelta;
            if (e.MiddleButtonPressed) _pendingMiddle = true;
            if (e.XButton1Pressed) _pendingGearDown = true;
            if (e.XButton2Pressed) _pendingGearUp = true;
        }
    }

    private void ProcessInputFrame()
    {
        if (_mapper == null || _viGEm == null || _mouseHook == null) return;
        if (!_isActive) return;

        float dt = 0.016f;

        NativeMethods.GetCursorPos(out var pt);
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        float halfWidth = screen.Bounds.Width / 2f;
        float centerX = screen.Bounds.X + halfWidth;

        bool altDown = (NativeMethods.GetAsyncKeyState(_steeringResetVk) & 0x8000) != 0;
        float steeringInput;

        if (altDown)
        {
            steeringInput = 0f;
        }
        else
        {
            if (_altWasDown)
                _centerOffset = pt.x - centerX;

            float effectiveCenterX = centerX + _centerOffset;
            float baseInput = (pt.x - effectiveCenterX) / halfWidth;
            steeringInput = Math.Clamp(baseInput * _config!.Steering.InputRange, -1f, 1f);
        }
        _altWasDown = altDown;

        int wheel;
        bool middle;
        bool gearUp;
        bool gearDown;
        lock (_pendingLock)
        {
            wheel = _pendingWheel;
            middle = _pendingMiddle;
            gearUp = _pendingGearUp;
            gearDown = _pendingGearDown;
            _pendingWheel = 0;
            _pendingMiddle = false;
            _pendingGearUp = false;
            _pendingGearDown = false;
        }

        _mapper.ProcessSteeringInput(steeringInput, dt);
        _mapper.ProcessScroll(wheel, middle, dt);

        if (gearUp) _viGEm.PressGearUp();
        if (gearDown) _viGEm.PressGearDown();

        _viGEm.SetSteering(_mapper.SteeringOutput);
        _viGEm.SetThrottle(_mapper.ThrottleOutput);
    }

    private void ShowOsd()
    {
        if (_osd != null) return;
        _osd = new OsdWindow(_mapper!, _config!.Osd);
        _osd.SetActive(_isActive);
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
        _osd?.UpdateConfig(config.Osd);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mouseHook?.Dispose();
        _keyboardHook?.Dispose();
        _viGEm?.Dispose();
        _osd?.Close();
        _settings?.Close();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
