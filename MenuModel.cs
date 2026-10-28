using System;
using System.Diagnostics;

namespace RadialMenu;

/// <summary>One slice. Top-level slices have Children (outer ring); children have an Action.</summary>
internal sealed class MenuItem
{
    public string Icon { get; init; } = "";
    public string Label { get; init; } = "";
    public Action? Action { get; init; }
    public MenuItem[]? Children { get; init; }
    /// <summary>Mouse-wheel handler while this slice is hovered (+1 / -1).</summary>
    public Action<int>? Wheel { get; init; }
    /// <summary>Live status text, e.g. "Volume 45%".</summary>
    public Func<string>? Status { get; init; }
}

/// <summary>
/// THE place to customise the menu. Add/remove/reorder slices freely;
/// the ring re-divides itself automatically.
/// </summary>
internal static class MenuBuilder
{
    static Action Keys(params ushort[] k) => () => InputSender.Chord(k);
    static Action Run(string target) => () =>
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    };
    static MenuItem I(string icon, string label, Action a) => new() { Icon = icon, Label = label, Action = a };

    public static MenuItem[] Build() => new[]
    {
        new MenuItem
        {
            Icon = "🔊", Label = "Volume",
            Wheel = d => AudioController.Change(d * 2),
            Status = () => AudioController.Muted ? "Volume — muted" : $"Volume {AudioController.Volume}%",
            Children = new[]
            {
                I("➕", "Volume up", () => AudioController.Change(+5)),
                I("🔇", "Mute", AudioController.ToggleMute),
                I("➖", "Volume down", () => AudioController.Change(-5)),
            }
        },
        new MenuItem
        {
            Icon = "🔆", Label = "Brightness",
            Wheel = d => BrightnessController.Change(d * 5),
            Status = () => BrightnessController.Level >= 0 ? $"Brightness {BrightnessController.Level}%" : "Brightness (not supported here)",
            Children = new[]
            {
                I("➕", "Brighter", () => BrightnessController.Change(+10)),
                I("🌙", "Night light settings", Run("ms-settings:nightlight")),
                I("➖", "Dimmer", () => BrightnessController.Change(-10)),
            }
        },
        new MenuItem
        {
            Icon = "🖥", Label = "Windows",
            Children = new[]
            {
                I("⇄", "Switch app (Alt+Tab)", Keys(VK.Alt, VK.Tab)),
                I("▦", "Task view", Keys(VK.Win, VK.Tab)),
                I("⛶", "Full screen (F11)", Keys(VK.F11)),
                I("🗖", "Maximize", Keys(VK.Win, VK.Up)),
                I("🗕", "Minimize", Keys(VK.Win, VK.Down)),
                I("◧", "Snap left", Keys(VK.Win, VK.Left)),
                I("◨", "Snap right", Keys(VK.Win, VK.Right)),
                I("🏠", "Show desktop", Keys(VK.Win, VK.D)),
            }
        },
        new MenuItem
        {
            Icon = "🎵", Label = "Media",
            Children = new[]
            {
                I("⏮", "Previous", Keys(VK.MediaPrev)),
                I("⏯", "Play / Pause", Keys(VK.MediaPlay)),
                I("⏭", "Next", Keys(VK.MediaNext)),
                I("⏹", "Stop", Keys(VK.MediaStop)),
            }
        },
        new MenuItem
        {
            Icon = "🌐", Label = "Browser / tabs",
            Children = new[]
            {
                I("⬅", "Back", Keys(VK.Alt, VK.Left)),
                I("➡", "Forward", Keys(VK.Alt, VK.Right)),
                I("↻", "Refresh", Keys(VK.F5)),
                I("🗂", "New tab", Keys(VK.Ctrl, VK.T)),
                I("✖", "Close tab", Keys(VK.Ctrl, VK.W)),
                I("♻", "Reopen closed tab", Keys(VK.Ctrl, VK.Shift, VK.T)),
            }
        },
        new MenuItem
        {
            Icon = "✂", Label = "Edit",
            Children = new[]
            {
                I("📄", "Copy", Keys(VK.Ctrl, VK.C)),
                I("📋", "Paste", Keys(VK.Ctrl, VK.V)),
                I("✂", "Cut", Keys(VK.Ctrl, VK.X)),
                I("↶", "Undo", Keys(VK.Ctrl, VK.Z)),
                I("↷", "Redo", Keys(VK.Ctrl, VK.Y)),
                I("▣", "Select all", Keys(VK.Ctrl, VK.A)),
            }
        },
        new MenuItem
        {
            Icon = "📷", Label = "Capture",
            Children = new[]
            {
                I("✂", "Snipping tool", Keys(VK.Win, VK.Shift, 0x53 /*S*/)),
                I("🖼", "Full screenshot", Keys(VK.PrintScreen)),
                I("😀", "Emoji panel", Keys(VK.Win, VK.Period)),
                I("🗒", "Clipboard history", Keys(VK.Win, VK.V)),
            }
        },
        new MenuItem
        {
            Icon = "⚙", Label = "System",
            Children = new[]
            {
                I("📊", "Task Manager", Keys(VK.Ctrl, VK.Shift, VK.Esc)),
                I("⚙", "Settings", Keys(VK.Win, VK.I)),
                I("📁", "File Explorer", Keys(VK.Win, VK.E)),
                I("▶", "Run…", Keys(VK.Win, VK.R)),
                I("🔒", "Lock PC", Keys(VK.Win, VK.L)),
                I("❌", "Close window", Keys(VK.Alt, VK.F4)),
            }
        },
    };
}
