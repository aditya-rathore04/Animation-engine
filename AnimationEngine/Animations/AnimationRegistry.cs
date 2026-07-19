using System;
using System.Collections.Generic;

namespace AnimationEngine.Animations;

public static class AnimationRegistry
{
    private static readonly Dictionary<string, IAnimation> _registry = new(StringComparer.OrdinalIgnoreCase);

    static AnimationRegistry()
    {
        // Register default plane animation
        Register(new PlaneAnimation());
        Register(new CometAnimation());
    }

    /// <summary>
    /// Registers an animation. Registration takes exactly one line here.
    /// </summary>
    public static void Register(IAnimation animation)
    {
        if (animation == null) throw new ArgumentNullException(nameof(animation));
        _registry[animation.StyleKey] = animation;
    }

    /// <summary>
    /// Resolves an animation by styleKey. Unknown styles log and fall back to the plane style.
    /// </summary>
    public static IAnimation Resolve(string? styleKey)
    {
        if (string.IsNullOrWhiteSpace(styleKey) || !_registry.TryGetValue(styleKey, out var animation))
        {
            Console.WriteLine($"[Registry] Style '{styleKey}' unrecognized or missing. Falling back to default 'plane' style.");
            return _registry["plane"];
        }
        return animation;
    }
}
