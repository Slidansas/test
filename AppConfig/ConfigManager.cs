using System;
using System.IO;
using System.Text.Json;
using AROKIS.Backend.Models;

namespace Photino.Blazor.AROKIS.AppConfig;

public static class ConfigManager
{
    private static readonly string AppName        = "AROKIS";
    private static readonly string ConfigFileName = "AROKISconfig.json";

    private static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppName, ConfigFileName);
    }

    public static AppConfig EnsureConfigExists()
    {
        var path = GetConfigPath();
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        var def = new AppConfig();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(def,
            new JsonSerializerOptions { WriteIndented = true }));
        return def;
    }

    public static (ArokisSettings settings, AppConfig config) LoadConfig()
    {
        var cfg = EnsureConfigExists();
        var settings = new ArokisSettings
        {
            MmPerUnit    = cfg.MmPerUnitConfig,
            ControllerIp = cfg.ControllerIpConfig
        };
        return (settings, cfg);
    }

    public static void SaveConfig(AppConfig config)
    {
        var path = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
