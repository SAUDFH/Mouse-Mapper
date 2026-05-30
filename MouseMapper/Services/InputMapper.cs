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
    private float _scrollScale = 1200f;
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

    public void SetScrollScale(float scale) => _scrollScale = scale;

    public void ProcessSteeringInput(float input, float deltaTime)
    {
        if (!IsActive) return;

        float absInput = Math.Abs(input);
        float sign = Math.Sign(input);

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

        float effectiveScale = _scrollScale / _throttleParams.InputRange;

        _accumulatedScroll += wheelDelta;
        _accumulatedScroll = Math.Clamp(_accumulatedScroll, 0f, effectiveScale);

        float normalized = _accumulatedScroll / effectiveScale;
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
        lock (_lock)
        {
            _steeringParams.Deadzone = newSteering.Deadzone;
            _steeringParams.LowSlope = newSteering.LowSlope;
            _steeringParams.KneePoint = newSteering.KneePoint;
            _steeringParams.HighSlope = newSteering.HighSlope;
            _steeringParams.SmoothingMs = newSteering.SmoothingMs;
            _steeringParams.AntiDeadzone = newSteering.AntiDeadzone;
            _steeringParams.InputRange = newSteering.InputRange;

            _throttleParams.Deadzone = newThrottle.Deadzone;
            _throttleParams.LowSlope = newThrottle.LowSlope;
            _throttleParams.KneePoint = newThrottle.KneePoint;
            _throttleParams.HighSlope = newThrottle.HighSlope;
            _throttleParams.SmoothingMs = newThrottle.SmoothingMs;
            _throttleParams.AntiDeadzone = newThrottle.AntiDeadzone;
            _throttleParams.InputRange = newThrottle.InputRange;
        }
    }
}
