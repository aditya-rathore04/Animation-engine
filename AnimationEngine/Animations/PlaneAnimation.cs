using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
        private readonly Image? _planeImage;
        private readonly int _baseY;
        private readonly int _systemWidth;
        private readonly int _bannerWidth;
        private readonly int _bannerHeight;
        private readonly int _textWidth;
        private readonly int _textHeight;
        private readonly int _planeWidth;
        private readonly int _planeHeight;

        private const int TargetPlaneWidth = 350;
        private const int ConnectorLength = 140; // Spacing for horizontal tow line + V-harness
        private const int SwallowtailNotch = 50;  // Left swallowtail cut depth
        private const int MaxTextWidth = 600;     // Restrict banner width for long messages

        public PlaneOverlayForm(string message, Rectangle screenBounds) : base(screenBounds)
        {
            _message = message;

            // Set vertical center in the upper third
            _baseY = screenBounds.Height / 3;

            // Load plane PNG image safely
            _planeImage = TryLoadPlaneImage();

            if (_planeImage != null)
            {
                _planeWidth = TargetPlaneWidth;
                _planeHeight = Math.Max(50, (int)Math.Round((double)_planeWidth * _planeImage.Height / _planeImage.Width));
            }
            else
            {
                // Default fallback dimensions if image is missing
                _planeWidth = 350;
                _planeHeight = 175;
            }

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
            _bannerWidth = _textWidth + SwallowtailNotch + 80; // Padding for text + swallowtail cut
            _bannerHeight = Math.Max(_planeHeight + 20, _textHeight + 50); // scales height for multi-line text

            _systemWidth = _planeWidth + ConnectorLength + _bannerWidth;
        }

        private Image? TryLoadPlaneImage()
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "plane.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "plane.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "plane.png")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return Image.FromFile(path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PlaneAnimation] Failed loading image at {path}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("[PlaneAnimation] Warning: Assets/plane.png not found.");
            return null;
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
            int planeY = currentY + (_bannerHeight - _planeHeight) / 2;

            int bannerX = currentX;
            int bannerY = currentY;

            // 3. Draw Towing Rigging (#B3B3B3, 4px thick solid gray rope in V-harness / Y-shape)
            Color ropeColor = Color.FromArgb(255, 179, 179, 179); // #B3B3B3
            using (var ropePen = new Pen(ropeColor, 4f))
            {
                ropePen.StartCap = LineCap.Round;
                ropePen.EndCap = LineCap.Round;
                ropePen.LineJoin = LineJoin.Round;

                Point pTail = new Point(planeX + 5, planeY + _planeHeight / 2);
                Point pSplit = new Point(bannerX + _bannerWidth + 70, bannerY + _bannerHeight / 2);
                Point pTopGrommet = new Point(bannerX + _bannerWidth - 4, bannerY + 12);
                Point pBotGrommet = new Point(bannerX + _bannerWidth - 4, bannerY + _bannerHeight - 12);

                // Horizontal line from plane tail to harness split point
                e.Graphics.DrawLine(ropePen, pTail, pSplit);

                // Sideways triangle V-harness arms to top and bottom banner corners
                e.Graphics.DrawLine(ropePen, pSplit, pTopGrommet);
                e.Graphics.DrawLine(ropePen, pSplit, pBotGrommet);
            }

            // 4. Draw Swallowtail Cloth Banner (White background with 2px thin black border)
            using (var bannerPath = GetSwallowtailBannerPath(bannerX, bannerY, _bannerWidth, _bannerHeight, SwallowtailNotch))
            using (var bannerBrush = new SolidBrush(Color.White))
            using (var borderPen = new Pen(Color.Black, 2f))
            {
                e.Graphics.FillPath(bannerBrush, bannerPath);
                e.Graphics.DrawPath(borderPen, bannerPath);
            }

            // 5. Draw Attachment Grommet Rings on Banner Right Corners
            using (var grommetBrush = new SolidBrush(Color.FromArgb(255, 120, 120, 120)))
            using (var holeBrush = new SolidBrush(Color.White))
            {
                int gSize = 10;
                // Top Right Grommet
                e.Graphics.FillEllipse(grommetBrush, bannerX + _bannerWidth - 14, bannerY + 7, gSize, gSize);
                e.Graphics.FillEllipse(holeBrush, bannerX + _bannerWidth - 12, bannerY + 9, gSize - 4, gSize - 4);

                // Bottom Right Grommet
                e.Graphics.FillEllipse(grommetBrush, bannerX + _bannerWidth - 14, bannerY + _bannerHeight - 17, gSize, gSize);
                e.Graphics.FillEllipse(holeBrush, bannerX + _bannerWidth - 12, bannerY + _bannerHeight - 15, gSize - 4, gSize - 4);
            }

            // 6. Draw Message Text in Solid Black (centered in banner main body)
            using (var font = new Font("Segoe UI", 26, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.Black))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                var textRect = new Rectangle(bannerX + SwallowtailNotch + 40, bannerY + (_bannerHeight - _textHeight) / 2, _textWidth, _textHeight);
                e.Graphics.DrawString(_message, font, textBrush, textRect, sf);
            }

            // 7. Draw Plane Image
            if (_planeImage != null)
            {
                e.Graphics.DrawImage(_planeImage, planeX, planeY, _planeWidth, _planeHeight);
            }
            else
            {
                // Fallback rendering if plane.png missing
                using (var pen = new Pen(Color.Red, 4f))
                {
                    e.Graphics.DrawRectangle(pen, planeX, planeY, _planeWidth, _planeHeight);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _planeImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        private GraphicsPath GetSwallowtailBannerPath(int x, int y, int width, int height, int notchDepth)
        {
            var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new Point(x, y),                                   // Top Left
                new Point(x + width, y),                            // Top Right
                new Point(x + width, y + height),                   // Bottom Right
                new Point(x, y + height),                          // Bottom Left
                new Point(x + notchDepth, y + height / 2)           // Swallowtail V-notch center
            });
            return path;
        }
    }
}


