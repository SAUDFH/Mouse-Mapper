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

    public float SteeringDeadzone { get => _config.Steering.Deadzone * 100; set { _config.Steering.Deadzone = value / 100; OnChanged(); } }
    public float SteeringLowSlope { get => _config.Steering.LowSlope; set { _config.Steering.LowSlope = value; OnChanged(); } }
    public float SteeringKneePoint { get => _config.Steering.KneePoint * 100; set { _config.Steering.KneePoint = value / 100; OnChanged(); } }
    public float SteeringHighSlope { get => _config.Steering.HighSlope; set { _config.Steering.HighSlope = value; OnChanged(); } }
    public int SteeringSmoothing { get => _config.Steering.SmoothingMs; set { _config.Steering.SmoothingMs = value; OnChanged(); } }
    public float SteeringAntiDeadzone { get => _config.Steering.AntiDeadzone * 100; set { _config.Steering.AntiDeadzone = value / 100; OnChanged(); } }
    public float SteeringInputRange { get => _config.Steering.InputRange; set { _config.Steering.InputRange = value; OnChanged(); } }

    public float ThrottleDeadzone { get => _config.Throttle.Deadzone * 100; set { _config.Throttle.Deadzone = value / 100; OnChanged(); } }
    public float ThrottleLowSlope { get => _config.Throttle.LowSlope; set { _config.Throttle.LowSlope = value; OnChanged(); } }
    public float ThrottleKneePoint { get => _config.Throttle.KneePoint * 100; set { _config.Throttle.KneePoint = value / 100; OnChanged(); } }
    public float ThrottleHighSlope { get => _config.Throttle.HighSlope; set { _config.Throttle.HighSlope = value; OnChanged(); } }
    public int ThrottleSmoothing { get => _config.Throttle.SmoothingMs; set { _config.Throttle.SmoothingMs = value; OnChanged(); } }
    public float ThrottleAntiDeadzone { get => _config.Throttle.AntiDeadzone * 100; set { _config.Throttle.AntiDeadzone = value / 100; OnChanged(); } }
    public float ThrottleInputRange { get => _config.Throttle.InputRange; set { _config.Throttle.InputRange = value; OnChanged(); } }

    public string ToggleKey { get => _config.Activation.ToggleKey; set { _config.Activation.ToggleKey = value; OnChanged(); } }
    public string ThrottleResetButton { get => _config.Activation.ThrottleResetButton; set { _config.Activation.ThrottleResetButton = value; OnChanged(); } }
    public string SteeringResetKey { get => _config.Activation.SteeringResetKey; set { _config.Activation.SteeringResetKey = value; OnChanged(); } }
    public string GearUpKey { get => _config.Activation.GearUpKey; set { _config.Activation.GearUpKey = value; OnChanged(); } }
    public string GearDownKey { get => _config.Activation.GearDownKey; set { _config.Activation.GearDownKey = value; OnChanged(); } }

    public bool OsdEnabled { get => _config.Osd.Enabled; set { _config.Osd.Enabled = value; OnChanged(); } }
    public double OsdScale { get => _config.Osd.Scale; set { _config.Osd.Scale = value; OnChanged(); } }

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
        _mapper.UpdateParameters(_config.Steering, _config.Throttle);
    }

    private void Save()
    {
        ConfigSaved?.Invoke(this, _config);
    }

    private void Cancel()
    {
        _config.Steering = _originalConfig.Steering.Clone();
        _config.Throttle = _originalConfig.Throttle.Clone();
        _config.Activation = new ActivationConfig
        {
            ToggleKey = _originalConfig.Activation.ToggleKey,
            ThrottleResetButton = _originalConfig.Activation.ThrottleResetButton,
            SteeringResetKey = _originalConfig.Activation.SteeringResetKey,
            GearUpKey = _originalConfig.Activation.GearUpKey,
            GearDownKey = _originalConfig.Activation.GearDownKey
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
            ThrottleResetButton = src.Activation.ThrottleResetButton,
            SteeringResetKey = src.Activation.SteeringResetKey,
            GearUpKey = src.Activation.GearUpKey,
            GearDownKey = src.Activation.GearDownKey
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
