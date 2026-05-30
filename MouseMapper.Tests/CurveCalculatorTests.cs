using MouseMapper.Models;
using MouseMapper.Services;

namespace MouseMapper.Tests;

public class CurveCalculatorTests
{
    private static CurveParameters LinearNoSmoothing => new()
    {
        Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0,
        AntiDeadzone = 0f
    };

    [Test]
    public void Apply_InputBelowDeadzone_ReturnsZero()
    {
        var p = new CurveParameters { Deadzone = 0.03f, AntiDeadzone = 0f };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.02f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void Apply_InputAboveDeadzone_ReturnsNonZero()
    {
        var p = new CurveParameters { Deadzone = 0.03f, AntiDeadzone = 0f };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.10f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.GreaterThan(0f));
    }

    [Test]
    public void Apply_LinearCurveNoDeadzone_OutputMatchesInput()
    {
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.5f, LinearNoSmoothing, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Apply_FullInput_ReturnsCloseToOne()
    {
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(1.0f, LinearNoSmoothing, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void Apply_LowSlopeGentle_ProducesSmallOutput()
    {
        var p = new CurveParameters
        {
            Deadzone = 0f, LowSlope = 0.2f, KneePoint = 0.4f, HighSlope = 1f, SmoothingMs = 0,
            AntiDeadzone = 0f
        };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.2f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(0.04f).Within(0.001f));
    }

    [Test]
    public void Apply_HighSlopeSteep_ProducesLargerOutput()
    {
        var p = new CurveParameters
        {
            Deadzone = 0f, LowSlope = 1f, KneePoint = 0.3f, HighSlope = 3f, SmoothingMs = 0,
            AntiDeadzone = 0f
        };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.6f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void Apply_Smoothing_ReducesStepChange()
    {
        var p = new CurveParameters
        {
            Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 100,
            AntiDeadzone = 0f
        };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(1.0f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.GreaterThan(0.05f));
        Assert.That(result, Is.LessThan(0.2f));
    }

    [Test]
    public void Apply_ZeroSmoothing_OutputInstant()
    {
        float prevOutput = 0.5f;
        float result = CurveCalculator.Apply(1.0f, LinearNoSmoothing, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void Apply_DeadzoneEdge_ExactlyAtDeadzone_ReturnsZero()
    {
        var p = new CurveParameters { Deadzone = 0.05f, AntiDeadzone = 0f };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.05f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void Apply_JustAboveDeadzone_ReturnsSmallPositive()
    {
        var p = new CurveParameters { Deadzone = 0.05f, AntiDeadzone = 0f };
        float prevOutput = 0f;
        float result = CurveCalculator.Apply(0.051f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.GreaterThan(0f));
    }

    [Test]
    public void Apply_AntiDeadzone_RaisesOutputFloor()
    {
        var p = new CurveParameters
        {
            Deadzone = 0f, LowSlope = 1f, KneePoint = 1f, HighSlope = 1f, SmoothingMs = 0,
            AntiDeadzone = 0.25f
        };
        float prevOutput = 0f;
        // With linear curve, raw = input. With 0.25 anti-deadzone:
        // output = 0.25 + raw * 0.75
        float result = CurveCalculator.Apply(0f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(0.25f).Within(0.001f));

        result = CurveCalculator.Apply(1.0f, p, ref prevOutput, 0.016f);
        Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));
    }
}
