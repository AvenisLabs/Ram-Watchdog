// Config.cs — Settings persistence for RamWatchdog v1.9.0
using System.Text.Json;

namespace RamWatchdog;

/// <summary>
/// Manages ignored-process list, alert state, and threshold. Persists to %APPDATA%\RamWatchdog\config.json.
/// Threshold modes: ThresholdGB > 0 = fixed GB, ThresholdPercent > 0 = percent of total RAM, both 0 = auto.
/// </summary>
public sealed class Config
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamWatchdog");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public List<string> IgnoredProcesses { get; set; } = [];
    public bool AlertsSilenced { get; set; }

    /// <summary>Fixed threshold in GB. 0 = not using GB mode.</summary>
    public double ThresholdGB { get; set; }

    /// <summary>Threshold as percent of total RAM (1-90). 0 = not using percent mode.</summary>
    public double ThresholdPercent { get; set; }

    /// <summary>Display floor in GB. 0 = default (0.5 GB / 500 MB).</summary>
    public double DisplayFloorGB { get; set; }

    /// <summary>Master toggle for toast notifications. When false, no toasts fire (tray icon flash still works).</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>System-wide RAM usage alert threshold (1-99%). 0 = disabled. Default 90.</summary>
    public int SystemUsageThresholdPercent { get; set; } = 90;

    public static Config Load()
    {
        if (!File.Exists(ConfigPath))
            return new Config();

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<Config>(json, JsonOpts) ?? new Config();
        }
        catch
        {
            return new Config();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        string json = JsonSerializer.Serialize(this, JsonOpts);
        File.WriteAllText(ConfigPath, json);
    }

    public bool IsIgnored(string processName) =>
        IgnoredProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);

    public void AddIgnored(string processName)
    {
        if (!IsIgnored(processName))
        {
            IgnoredProcesses.Add(processName);
            Save();
        }
    }

    public void RemoveIgnored(string processName)
    {
        int removed = IgnoredProcesses.RemoveAll(
            p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            Save();
    }
}
