using System;

namespace AnimationEngine;

static class Program
{
    [STAThread]
    static void Main()
    {
        Console.WriteLine("Initializing Trigger Server (Milestone 1 Console Mode)...");
        
        using var server = new TriggerServer(request =>
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("RECEIVED TRIGGER REQUEST:");
            Console.WriteLine($"Message: '{request.Message}'");
            Console.WriteLine($"Style:   '{request.Style}'");
            Console.WriteLine($"Speed:   '{request.Speed}'");
            Console.WriteLine("========================================");
            Console.WriteLine();
        });

        try
        {
            server.Start();
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical Error: {ex.Message}");
        }
        finally
        {
            server.Stop();
        }
    }
}