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
    /// Computes the speed multiplier dynamically based on the message word count.
    /// Baseline is 5 words = 1.0 multiplier. Each word deviation shifts the duration by 8%.
    /// Clamped between 0.5 (fastest) and 2.0 (slowest).
    /// </summary>
    public static double GetDynamicSpeedMultiplier(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return 1.0;

        string[] words = message.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;

        double multiplier = 1.0 + (wordCount - 5) * 0.08;
        return Math.Max(0.5, Math.Min(2.0, multiplier));
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

    /// <summary>
    /// Cubic Ease In Out: Smooth acceleration at entry and deceleration at exit.
    /// </summary>
    public static double EaseInOutCubic(double t)
    {
        return t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) / 2.0;
    }
}
