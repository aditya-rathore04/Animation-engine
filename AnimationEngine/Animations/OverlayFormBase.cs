using System;
using System.Drawing;
using System.Windows.Forms;

namespace AnimationEngine.Animations;

public class OverlayFormBase : Form
{
    private System.Windows.Forms.Timer? _timer;
    private DateTime _startTime;
    private int _durationMs;
    
    /// <summary>
    /// Current animation progress from 0.0 (start) to 1.0 (end).
    /// </summary>
    protected double AnimationProgress { get; private set; }

    public OverlayFormBase(Rectangle screenBounds)
    {
        // 1. Core visual styling
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.DoubleBuffered = true;
        
        // 2. Position control
        this.StartPosition = FormStartPosition.Manual;
        this.Bounds = screenBounds;

        // 3. Transparency key setup
        // Uses dark key color to prevent purple/magenta halo fringing on anti-aliased edges
        this.BackColor = Color.FromArgb(1, 1, 1);
        this.TransparencyKey = Color.FromArgb(1, 1, 1);
    }

    /// <summary>
    /// Set extended window styles for click-through and non-activating window.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            
            // WS_EX_LAYERED (0x00080000): Layered window style (needed for custom opacity/rendering)
            // WS_EX_TRANSPARENT (0x00000020): True click-through window (mouse/keyboard input goes through)
            // WS_EX_NOACTIVATE (0x08000000): Prevent the window from stealing focus on display
            cp.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000;
            
            return cp;
        }
    }

    /// <summary>
    /// Starts the animation timer with the specified duration.
    /// </summary>
    protected void StartAnimation(int durationMs)
    {
        _durationMs = durationMs;
        _startTime = DateTime.Now;
        AnimationProgress = 0.0;

        _timer = new System.Windows.Forms.Timer();
        _timer.Interval = 16; // ~60 FPS
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.Now - _startTime).TotalMilliseconds;
        AnimationProgress = Math.Min(1.0, elapsed / _durationMs);

        if (AnimationProgress >= 1.0)
        {
            StopAndClose();
        }
        else
        {
            OnAnimationProgressUpdated();
            this.Invalidate();
        }
    }

    /// <summary>
    /// Hook for subclasses to execute code on each animation frame tick.
    /// </summary>
    protected virtual void OnAnimationProgressUpdated()
    {
    }

    private void StopAndClose()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer.Dispose();
            _timer = null;
        }
        this.Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }
        base.Dispose(disposing);
    }
}
