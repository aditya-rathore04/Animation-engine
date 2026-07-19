using System.Drawing;
using System.Windows.Forms;

namespace AnimationEngine.Animations;

public interface IAnimation
{
    /// <summary>
    /// Unique style key this animation registers under, e.g. "plane".
    /// </summary>
    string StyleKey { get; }

    /// <summary>
    /// Produce a self-contained overlay Form. The form must show itself,
    /// run its own animation loop, and Close()+Dispose() itself when done.
    /// Must not block the calling thread.
    /// </summary>
    Form CreateOverlay(string message, double speedMultiplier, Rectangle screenBounds);
}
