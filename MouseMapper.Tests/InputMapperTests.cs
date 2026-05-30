using MouseMapper.Models;
using MouseMapper.Services;

namespace MouseMapper.Tests;

public class InputMapperTests
{
    [Test]
    public void ProcessSteeringInput_Inactive_DoesNotChangeSteering()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = false;

        float initialSteering = mapper.SteeringOutput;
        mapper.ProcessSteeringInput(0.5f, 0.016f);
        Assert.That(mapper.SteeringOutput, Is.EqualTo(initialSteering));
    }

    [Test]
    public void ProcessSteeringInput_Active_ChangesSteering()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = true;

        mapper.ProcessSteeringInput(0.5f, 0.016f);
        Assert.That(mapper.SteeringOutput, Is.GreaterThan(0.3f));
    }

    [Test]
    public void ProcessScroll_AccumulatesThrottle()
    {
        var mapper = new InputMapper(
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 },
            new CurveParameters { Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0 });
        mapper.IsActive = true;

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

        mapper.ProcessScroll(360, false, 0.016f);
        float up = mapper.ThrottleOutput;

        mapper.ProcessScroll(-120, false, 0.016f);
        float after = mapper.ThrottleOutput;
        Assert.That(after, Is.LessThan(up));
    }
}
