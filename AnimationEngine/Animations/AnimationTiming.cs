using System;

namespace AnimationEngine.Animations;

public static class AnimationTiming
{
    /// <summary>
    /// Gets the speed multiplier for the animation duration.
    /// slow = 1.4 (longer duration), normal = 1.0, fast = 0.7 (shorter duration).
    /// </summary>
    public static double GetSpeedMultiplier(string? speed)
    {
        if (string.IsNullOrEmpty(speed))
            return 1.0;

        return speed.ToLowerInvariant() switch
        {
            "slow" => 1.4,
            "fast" => 0.7,
            "normal" => 1.0,
            _ => 1.0
        };
    }

    /// <summary>
    /// Cubic Ease In: f(t) = t^3
    /// Starts slow and accelerates.
    /// </summary>
    public static double EaseInCubic(double t)
    {
        return t * t * t;
    }

    /// <summary>
    /// Cubic Ease Out: f(t) = 1 - (1 - t)^3
    /// Starts fast and decelerates.
    /// </summary>
    public static double EaseOutCubic(double t)
    {
        double f = t - 1.0;
        return f * f * f + 1.0;
    }
}
