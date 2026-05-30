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
        SmoothingMs = 50,
        AntiDeadzone = 0.24f
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

    [JsonPropertyName("steeringResetKey")]
    public string SteeringResetKey { get; set; } = "LMenu";

    [JsonPropertyName("gearUpKey")]
    public string GearUpKey { get; set; } = "XButton2";

    [JsonPropertyName("gearDownKey")]
    public string GearDownKey { get; set; } = "XButton1";
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
