namespace MouseMapper.Models;

public class CurveParameters
{
    public float Deadzone { get; set; } = 0.03f;
    public float LowSlope { get; set; } = 0.3f;
    public float KneePoint { get; set; } = 0.35f;
    public float HighSlope { get; set; } = 2.5f;
    public int SmoothingMs { get; set; } = 50;
    public float AntiDeadzone { get; set; } = 0.24f;
    public float InputRange { get; set; } = 1.0f;

    public CurveParameters Clone() => new()
    {
        Deadzone = Deadzone,
        LowSlope = LowSlope,
        KneePoint = KneePoint,
        HighSlope = HighSlope,
        SmoothingMs = SmoothingMs,
        AntiDeadzone = AntiDeadzone,
        InputRange = InputRange
    };
}
