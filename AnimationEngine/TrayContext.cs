using System;
using System.Drawing;
using System.Windows.Forms;
using AnimationEngine.Animations;

namespace AnimationEngine;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly TriggerServer _server;

    public TrayContext()
    {
        // 1. Create Context Menu
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

        // 2. Create NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            ContextMenuStrip = _contextMenu,
            Text = "Animation Engine",
            Visible = true
        };

        // 3. Start HTTP Trigger Server
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
                var animation = AnimationRegistry.Resolve(request.Style);
                double speedMultiplier = AnimationTiming.GetSpeedMultiplier(request.Speed);
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                
                var overlay = animation.CreateOverlay(request.Message, speedMultiplier, bounds);
                overlay.Show();
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanUp();
        }
        base.Dispose(disposing);
    }
}
