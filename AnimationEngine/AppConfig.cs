using System;
using System.IO;
using System.Text.Json;

namespace AnimationEngine;

public class AppConfig
{
    public string DefaultSpeed { get; set; } = "dynamic";

    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    /// <summary>
    /// Loads configuration from config.json. If missing or invalid, generates a default configuration file.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Error loading config.json: {ex.Message}");
        }

        // Generate default config file
        var defaultConfig = new AppConfig();
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(defaultConfig, options);
            File.WriteAllText(ConfigPath, json);
            Console.WriteLine("[Config] Generated default config.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Error writing default config.json: {ex.Message}");
        }

        return defaultConfig;
    }
}
