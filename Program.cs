using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RadialMenu;

internal static class Program
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "RadialMenu.SingleInstance", out bool first);
        if (!first) return;

        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        var cfg = Config.Load();
        var items = MenuBuilder.Build();
        var overlay = new OverlayWindow(items, cfg);
        using var gesture = new GestureController(cfg, overlay, items);

        using var tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Radial Menu (hold right-click)", Visible = true };
        var menu = new ContextMenuStrip();
        var enabled = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
        enabled.CheckedChanged += (_, _) => gesture.Enabled = enabled.Checked;
        var startup = new ToolStripMenuItem("Start with Windows") { Checked = IsStartup(), CheckOnClick = true };
        startup.CheckedChanged += (_, _) => SetStartup(startup.Checked);
        SettingsWindow? settings = null;
        var look = new ToolStripMenuItem("Appearance…");
        look.Click += (_, _) =>
        {
            if (settings != null) { settings.Activate(); return; }
            gesture.Enabled = false;
            settings = new SettingsWindow(cfg, overlay);
            settings.Closed += (_, _) => { settings = null; gesture.Enabled = enabled.Checked; };
            settings.Show();
        };
        var openCfg = new ToolStripMenuItem("Open settings file");
        openCfg.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(Config.FilePath) { UseShellExecute = true }); } catch { } };
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => { tray.Visible = false; app.Shutdown(); };
        menu.Items.AddRange(new ToolStripItem[] { enabled, look, startup, openCfg, new ToolStripSeparator(), exit });
        tray.ContextMenuStrip = menu;

        app.Run();
    }

    static bool IsStartup()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("RadialMenu") != null; } catch { return false; }
    }

    static void SetStartup(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (k == null) return;
            if (on) k.SetValue("RadialMenu", "\"" + Environment.ProcessPath + "\"");
            else k.DeleteValue("RadialMenu", false);
        }
        catch { }
    }
}
