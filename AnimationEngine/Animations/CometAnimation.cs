using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AnimationEngine.Animations;

public class CometAnimation : IAnimation
{
    public string StyleKey => "comet";

    public Form CreateOverlay(string message, double speedMultiplier, Rectangle screenBounds)
    {
        var overlay = new CometOverlayForm(message, screenBounds);
        // Comet is punchy: base duration is 3200ms, scaled by the speedMultiplier
        int duration = (int)(3200 * speedMultiplier);
        overlay.Start(duration);
        return overlay;
    }

    private class CometOverlayForm : OverlayFormBase
    {
        private readonly string _message;
        
        // Flight path coordinates
        private readonly PointF _startPoint;
        private readonly PointF _crashPoint;
        private readonly float _trailLength = 260f;

        public CometOverlayForm(string message, Rectangle screenBounds) : base(screenBounds)
        {
            _message = message;

            // Start off-screen top-right
            _startPoint = new PointF(screenBounds.Width - 100f, -150f);
            
            // Crash in the lower-left center quadrant
            _crashPoint = new PointF(screenBounds.Width / 3.2f, screenBounds.Height * 0.78f);
        }

        public void Start(int durationMs)
        {
            StartAnimation(durationMs);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            double p = AnimationProgress;

            if (p < 0.4)
            {
                // ==========================================
                // PHASE 1: STREAK (0.0 to 0.4)
                // ==========================================
                double normalizedStreak = p / 0.4;
                double easedStreak = AnimationTiming.EaseInCubic(normalizedStreak);

                // Calculate current head position
                float headX = _startPoint.X + (float)((_crashPoint.X - _startPoint.X) * easedStreak);
                float headY = _startPoint.Y + (float)((_crashPoint.Y - _startPoint.Y) * easedStreak);
                var headPt = new PointF(headX, headY);

                // Calculate trail vector (opposite of movement direction)
                float dx = _startPoint.X - _crashPoint.X;
                float dy = _startPoint.Y - _crashPoint.Y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                float ux = dx / length;
                float uy = dy / length;

                // Tail endpoints
                var tailPt = new PointF(headX + ux * _trailLength, headY + uy * _trailLength);

                // Draw fiery linear gradient trail
                using (var trailBrush = new LinearGradientBrush(headPt, tailPt, Color.White, Color.Transparent))
                {
                    // Create color blend for fire: White -> Gold/Yellow -> Orange/Red -> Transparent
                    var blend = new ColorBlend(4);
                    blend.Colors = new[]
                    {
                        Color.FromArgb(255, 255, 255, 255),      // White core
                        Color.FromArgb(240, 255, 200, 0),        // Gold
                        Color.FromArgb(180, 255, 69, 0),         // Red-Orange
                        Color.FromArgb(0, 220, 20, 20)           // Transparent fade
                    };
                    blend.Positions = new[] { 0.0f, 0.15f, 0.5f, 1.0f };
                    trailBrush.InterpolationColors = blend;

                    using (var trailPen = new Pen(trailBrush, 14f))
                    {
                        trailPen.StartCap = LineCap.Round;
                        trailPen.EndCap = LineCap.Flat;
                        e.Graphics.DrawLine(trailPen, headPt, tailPt);
                    }
                }

                // Draw glow/halo around comet head
                using (var radialPath = new GraphicsPath())
                {
                    radialPath.AddEllipse(headX - 25, headY - 25, 50, 50);
                    using (var pgb = new PathGradientBrush(radialPath))
                    {
                        pgb.CenterColor = Color.FromArgb(200, 255, 240, 150);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, 255, 140, 0) };
                        e.Graphics.FillPath(pgb, radialPath);
                    }
                }

                // Draw central bright nucleus
                using (var headBrush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillEllipse(headBrush, headX - 8, headY - 8, 16, 16);
                }
            }
            else
            {
                // ==========================================
                // PHASE 2: CINEMATIC TITLE CARD (0.4 to 1.0)
                // ==========================================
                double cardOpacity = 0.0;
                
                if (p < 0.5)
                {
                    // Rapid fade-in of black overlay: 0.4 -> 0.5
                    cardOpacity = (p - 0.4) / 0.1;
                }
                else if (p < 0.82)
                {
                    // Hold fully visible: 0.5 -> 0.82
                    cardOpacity = 1.0;
                }
                else
                {
                    // Smooth fade-out: 0.82 -> 1.0
                    cardOpacity = (1.0 - p) / 0.18;
                }

                int alphaBg = (int)(cardOpacity * 242); // 95% opacity
                int alphaText = (int)(cardOpacity * 255);

                alphaBg = Math.Max(0, Math.Min(242, alphaBg));
                alphaText = Math.Max(0, Math.Min(255, alphaText));

                // 1. Fill entire screen with dark graphite/black overlay
                using (var bgBrush = new SolidBrush(Color.FromArgb(alphaBg, 12, 12, 15)))
                {
                    e.Graphics.FillRectangle(bgBrush, this.ClientRectangle);
                }

                // 2. Draw cinematic message centered
                using (var font = new Font("Segoe UI", 36, FontStyle.Bold))
                // Off-white text with high contrast
                using (var textBrush = new SolidBrush(Color.FromArgb(alphaText, 245, 245, 248)))
                // Soft gold drop shadow for a premium feel
                using (var shadowBrush = new SolidBrush(Color.FromArgb((int)(alphaText * 0.45), 212, 175, 55)))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    // Draw shadow offset by 3px
                    var shadowRect = this.ClientRectangle;
                    shadowRect.Offset(3, 3);
                    e.Graphics.DrawString(_message, font, shadowBrush, shadowRect, sf);

                    // Draw main message
                    e.Graphics.DrawString(_message, font, textBrush, this.ClientRectangle, sf);
                }
            }
        }
    }
}
