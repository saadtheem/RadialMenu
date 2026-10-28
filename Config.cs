using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RadialMenu;

internal sealed class Config
{
    /// <summary>Hold the right button this long (ms) to open the dial.</summary>
    public int HoldMs { get; set; } = 220;
    /// <summary>...or move this many physical pixels while holding (~1 inch is about 96).</summary>
    public int MoveThresholdPx { get; set; } = 14;
    /// <summary>Process names (no .exe) where the right button is left alone.</summary>
    public List<string> ExcludedProcesses { get; set; } = new() { "mstsc", "vmconnect" };

    // ---- appearance ----
    /// <summary>Frosted-glass blur of what is behind the dial, 0 (none) .. 40.</summary>
    public int BlurRadius { get; set; } = 22;
    /// <summary>How strong the colour tint is, 0 (clear) .. 100 (almost solid).</summary>
    public int TintStrength { get; set; } = 40;
    /// <summary>Dark, Light or Clear.</summary>
    public string Style { get; set; } = "Dark";
    /// <summary>Highlight colour as RRGGBB.</summary>
    public string Accent { get; set; } = "0A84FF";
    /// <summary>Dial size relative to the automatic size for your screen (60..140).</summary>
    public int SizePercent { get; set; } = 90;
    public bool Animations { get; set; } = true;
    /// <summary>Animation speed in percent (50..200).</summary>
    public int AnimSpeed { get; set; } = 100;

    public void ResetAppearance()
    {
        var d = new Config();
        BlurRadius = d.BlurRadius; TintStrength = d.TintStrength; Style = d.Style; Accent = d.Accent;
        SizePercent = d.SizePercent; Animations = d.Animations; AnimSpeed = d.AnimSpeed;
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RadialMenu");
    public static string FilePath => Path.Combine(Dir, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config();
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Config(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        return new Config();
    }
}
