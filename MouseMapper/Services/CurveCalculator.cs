using MouseMapper.Models;

namespace MouseMapper.Services;

public static class CurveCalculator
{
    /// Apply the response curve to a normalized input value.
    /// input: Normalized input in [0, 1] (absolute value, caller handles sign)
    /// p: Curve parameters
    /// prevOutput: Previous output value (for EMA smoothing), updated in place
    /// deltaTime: Time since last update in seconds
    /// Returns: Output value in [0, 1]
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
        raw = p.AntiDeadzone + raw * (1f - p.AntiDeadzone);

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
