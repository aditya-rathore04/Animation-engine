using System;
using System.Drawing;
using System.Windows.Forms;

namespace AnimationEngine.Animations;

public class PlaneAnimationStub : IAnimation
{
    public string StyleKey => "plane";

    public Form CreateOverlay(string message, double speedMultiplier, Rectangle screenBounds)
    {
        var overlay = new PlaneStubOverlayForm(message, screenBounds);
        // Base duration is 2000ms, scaled by speedMultiplier
        int duration = (int)(2000 * speedMultiplier);
        overlay.Start(duration);
        return overlay;
    }

    private class PlaneStubOverlayForm : OverlayFormBase
    {
        private readonly string _message;

        public PlaneStubOverlayForm(string message, Rectangle screenBounds) : base(screenBounds)
        {
            _message = message;
        }

        public void Start(int durationMs)
        {
            StartAnimation(durationMs);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Calculate alpha for fade-in/fade-out
            // Fade in for the first half, fade out for the second half
            double opacity;
            if (AnimationProgress < 0.5)
            {
                opacity = AnimationProgress * 2.0; // 0.0 -> 1.0
            }
            else
            {
                opacity = (1.0 - AnimationProgress) * 2.0; // 1.0 -> 0.0
            }

            int alpha = (int)(opacity * 255);
            alpha = Math.Max(0, Math.Min(255, alpha));

            // Use high quality GDI+ settings
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Draw a rounded-style dark card in the center of the screen
            using var cardBrush = new SolidBrush(Color.FromArgb(alpha, 33, 37, 41));
            int rectWidth = 500;
            int rectHeight = 120;
            int rectX = (Bounds.Width - rectWidth) / 2;
            int rectY = (Bounds.Height - rectHeight) / 2;

            // Draw background rectangle
            e.Graphics.FillRectangle(cardBrush, rectX, rectY, rectWidth, rectHeight);

            // Draw message text
            using var textBrush = new SolidBrush(Color.FromArgb(alpha, 248, 249, 250));
            using var font = new Font("Segoe UI", 16, FontStyle.Regular);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            e.Graphics.DrawString(_message, font, textBrush, new Rectangle(rectX, rectY, rectWidth, rectHeight), sf);
        }
    }
}
