using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AnimationEngine.Animations;

public class PlaneAnimation : IAnimation
{
    public string StyleKey => "plane";

    public Form CreateOverlay(string message, double speedMultiplier, Rectangle screenBounds)
    {
        var overlay = new PlaneOverlayForm(message, screenBounds);
        // Base duration is 12000ms (12 seconds) for a slow linear cruise across the screen, scaled by the speedMultiplier
        int duration = (int)(12000 * speedMultiplier);
        overlay.Start(duration);
        return overlay;
    }

    private class PlaneOverlayForm : OverlayFormBase
    {
        private readonly string _message;
        private int _baseY;
        private int _systemWidth;
        private int _bannerWidth;
        private int _bannerHeight;
        private int _textWidth;
        private int _textHeight;
        
        // Large plane size (approx 7-8 times larger than initial 60x30 stub)
        private const int PlaneWidth = 420;
        private const int PlaneHeight = 210;
        private const int ConnectorLength = 60;
        private const int MaxTextWidth = 600; // Restrict banner width for long messages

        public PlaneOverlayForm(string message, Rectangle screenBounds) : base(screenBounds)
        {
            _message = message;
            
            // Set vertical center in the upper third
            _baseY = screenBounds.Height / 3;

            // Measure text size using a large, readable font and supporting wrapping
            using (var g = this.CreateGraphics())
            using (var font = new Font("Segoe UI", 26, FontStyle.Bold))
            {
                var singleLineSize = g.MeasureString(_message, font);
                if (singleLineSize.Width <= MaxTextWidth)
                {
                    _textWidth = (int)Math.Ceiling(singleLineSize.Width);
                    _textHeight = (int)Math.Ceiling(singleLineSize.Height);
                }
                else
                {
                    // Word wrap within MaxTextWidth
                    var wrappedSize = g.MeasureString(_message, font, MaxTextWidth);
                    _textWidth = MaxTextWidth;
                    _textHeight = (int)Math.Ceiling(wrappedSize.Height);
                }
            }

            // Calculate banner and system width
            _bannerWidth = _textWidth + 80; // 40px padding left and right
            _bannerHeight = Math.Max(PlaneHeight + 20, _textHeight + 50); // scales height for multi-line text
            
            _systemWidth = PlaneWidth + ConnectorLength + _bannerWidth;
        }

        public void Start(int durationMs)
        {
            StartAnimation(durationMs);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 1. Set High-Quality Graphics
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // 2. Linear motion (no speed-up or slow-down at ends)
            double easedProgress = AnimationProgress;

            // Left edge of the banner starts off-screen left, right edge of plane ends off-screen right
            int startX = -_systemWidth - 50;
            int endX = Bounds.Width + 50;
            int currentX = startX + (int)((endX - startX) * easedProgress);

            // Add vertical bobbing motion (simulates flying through air drafts)
            int bobbingOffset = (int)(Math.Sin(AnimationProgress * Math.PI * 4) * 20);
            int currentY = _baseY + bobbingOffset;

            // Define sub-positions
            int planeX = currentX + _bannerWidth + ConnectorLength;
            int planeY = currentY + (_bannerHeight - PlaneHeight) / 2;

            int bannerX = currentX;
            int bannerY = currentY;

            // 3. Draw Drop Shadow for the Banner
            using (var shadowPath = GetRoundedRectPath(new Rectangle(bannerX + 10, bannerY + 10, _bannerWidth, _bannerHeight), 24))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            // 4. Draw Connecting String/Cable
            using (var pen = new Pen(Color.FromArgb(160, 200, 200, 210), 3f))
            {
                pen.DashStyle = DashStyle.Dash;
                // Connect plane tail center to banner front center
                e.Graphics.DrawLine(pen, 
                    planeX + 25, planeY + PlaneHeight / 2, 
                    bannerX + _bannerWidth, bannerY + _bannerHeight / 2);
            }

            // 5. Draw Banner Card
            using (var bannerPath = GetRoundedRectPath(new Rectangle(bannerX, bannerY, _bannerWidth, _bannerHeight), 24))
            // Premium background gradient from dark graphite to deep slate-indigo
            using (var bannerBrush = new LinearGradientBrush(
                new Rectangle(bannerX, bannerY, _bannerWidth, _bannerHeight),
                Color.FromArgb(245, 18, 20, 28),
                Color.FromArgb(245, 33, 37, 51),
                15f))
            using (var borderPen = new Pen(Color.FromArgb(100, 100, 110, 130), 2f))
            {
                e.Graphics.FillPath(bannerBrush, bannerPath);
                e.Graphics.DrawPath(borderPen, bannerPath);
            }

            // 6. Draw Message Text with word-wrap support
            using (var font = new Font("Segoe UI", 26, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(255, 248, 249, 250)))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                
                // Position text container with 40px left/right padding
                var textRect = new Rectangle(bannerX + 40, bannerY + (_bannerHeight - _textHeight) / 2, _textWidth, _textHeight);
                e.Graphics.DrawString(_message, font, textBrush, textRect, sf);
            }

            // 7. Draw Origami Paper Airplane (Slate Indigo Theme)
            // Coordinates relative to planeX, planeY
            PointF tip = new PointF(planeX + PlaneWidth, planeY + PlaneHeight / 2f);
            PointF centerCrease = new PointF(planeX + PlaneWidth * 0.4f, planeY + PlaneHeight / 2f);
            PointF topEdgeBack = new PointF(planeX + PlaneWidth * 0.1f, planeY + PlaneHeight * 0.08f);
            PointF bottomEdgeBack = new PointF(planeX + PlaneWidth * 0.1f, planeY + PlaneHeight * 0.92f);
            PointF wingTopBack = new PointF(planeX, planeY);
            PointF wingBottomBack = new PointF(planeX, planeY + PlaneHeight);

            // Facet 1: Underside/Body Top (Darker shadow)
            using (var brush = new SolidBrush(Color.FromArgb(255, 67, 56, 202)))
            {
                e.Graphics.FillPolygon(brush, new[] { tip, centerCrease, topEdgeBack });
            }
            // Facet 2: Underside/Body Bottom (Deepest shadow)
            using (var brush = new SolidBrush(Color.FromArgb(255, 49, 46, 129)))
            {
                e.Graphics.FillPolygon(brush, new[] { tip, centerCrease, bottomEdgeBack });
            }
            // Facet 3: Wing Top (Lightest / Main reflected surface)
            using (var brush = new SolidBrush(Color.FromArgb(255, 129, 140, 248)))
            {
                e.Graphics.FillPolygon(brush, new[] { tip, topEdgeBack, wingTopBack });
            }
            // Facet 4: Wing Bottom (Medium reflected surface)
            using (var brush = new SolidBrush(Color.FromArgb(255, 99, 102, 241)))
            {
                e.Graphics.FillPolygon(brush, new[] { tip, bottomEdgeBack, wingBottomBack });
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
