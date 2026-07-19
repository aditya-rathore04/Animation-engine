using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AnimationEngine.Animations;
using AnimationEngine.Notifications;

namespace AnimationEngine;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly TriggerServer _server;
    private readonly AppConfig _config;
    private readonly Icon _customIcon;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public TrayContext()
    {
        // 1. Load Local Configuration (config.json)
        _config = AppConfig.Load();

        // 2. Create Context Menu
        _contextMenu = new ContextMenuStrip();
        
        var testPlaneItem = new ToolStripMenuItem("Test Plane", null, OnTestPlaneClicked);
        var testCometItem = new ToolStripMenuItem("Test Comet", null, OnTestCometClicked);
        var exitItem = new ToolStripMenuItem("Exit", null, OnExitClicked);

        _contextMenu.Items.Add(testPlaneItem);
        _contextMenu.Items.Add(testCometItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        // Force handle creation on context menu so we can use BeginInvoke safely for thread marshalling
        IntPtr forceHandle = _contextMenu.Handle;

        // 3. Draw a custom paper-airplane icon in memory
        _customIcon = CreateCustomIcon();

        // 4. Create NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Icon = _customIcon,
            ContextMenuStrip = _contextMenu,
            Text = "Animation Engine",
            Visible = true
        };

        // 5. Start HTTP Trigger Server
        _server = new TriggerServer(OnTriggerReceived);
        _server.Start();
    }

    private void OnTriggerReceived(TriggerRequest request)
    {
        // Marshal the HTTP callback thread onto the UI thread
        _contextMenu.BeginInvoke(new Action(() =>
        {
            try
            {
                // 1. Resolve style animation
                var animation = AnimationRegistry.Resolve(request.Style);
                
                // 2. Resolve speed setting:
                // If requested speed is default "normal", check if the config specifies a different default speed.
                string speedSetting = request.Speed;
                if (speedSetting == "normal" && _config.DefaultSpeed != "normal")
                {
                    speedSetting = _config.DefaultSpeed;
                }

                // 3. Calculate multiplier
                double speedMultiplier;
                if (speedSetting.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
                {
                    speedMultiplier = AnimationTiming.GetDynamicSpeedMultiplier(request.Message);
                }
                else
                {
                    speedMultiplier = AnimationTiming.GetSpeedMultiplier(speedSetting);
                }

                // 4. Resolve multi-monitor screen bounds (default to Primary Screen)
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                
                // 5. Create and show the overlay
                var overlay = animation.CreateOverlay(request.Message, speedMultiplier, bounds);
                overlay.Show();

                // 6. Fire native Windows toast notification (Action Center)
                ToastNotifier.ShowToast(request.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing animation: {ex.Message}");
            }
        }));
    }

    private void OnTestPlaneClicked(object? sender, EventArgs e)
    {
        // Mock a plane trigger
        OnTriggerReceived(new TriggerRequest { Message = "Test Plane Animation", Style = "plane", Speed = "normal" });
    }

    private void OnTestCometClicked(object? sender, EventArgs e)
    {
        // Mock a comet trigger
        OnTriggerReceived(new TriggerRequest { Message = "Test Comet Animation", Style = "comet", Speed = "fast" });
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        CleanUp();
        Application.Exit();
    }

    private void CleanUp()
    {
        _server.Stop();
        _server.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();

        // Safe cleanup of memory-drawn icon handle to prevent GDI resource leaks
        if (_customIcon != null)
        {
            DestroyIcon(_customIcon.Handle);
            _customIcon.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanUp();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Programmatically draws a sleek folded paper airplane icon at startup.
    /// Eliminates the need to package a separate .ico file.
    /// </summary>
    private static Icon CreateCustomIcon()
    {
        using (var bmp = new Bitmap(32, 32))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            
            // Draw a beautiful folded origami paper plane icon pointing upper-right
            PointF tip = new PointF(28, 4);
            PointF wingLeft = new PointF(4, 18);
            PointF wingRight = new PointF(18, 28);
            PointF center = new PointF(16, 16);

            // Facet 1: Left wing (Indigo)
            using (var brush = new SolidBrush(Color.FromArgb(255, 99, 102, 241)))
            {
                g.FillPolygon(brush, new[] { tip, wingLeft, center });
            }
            // Facet 2: Right wing (Deep Indigo)
            using (var brush = new SolidBrush(Color.FromArgb(255, 79, 70, 229)))
            {
                g.FillPolygon(brush, new[] { tip, wingRight, center });
            }
            // Facet 3: Shadow crease (Dark navy)
            using (var brush = new SolidBrush(Color.FromArgb(255, 49, 46, 129)))
            {
                g.FillPolygon(brush, new[] { center, wingLeft, new PointF(11, 21) });
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
    }
}
