using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RadialMenu;

/// <summary>
/// Built-in/laptop panel brightness through WMI (run via PowerShell so we need no extra packages).
/// External monitors usually don't expose this (they need DDC/CI) - see README.
/// </summary>
internal static class BrightnessController
{
    static int _level = -1;
    static readonly object _lock = new();
    static Timer? _apply;
    static bool _tried;

    public static int Level => _level;

    /// <summary>Read the current brightness in the background when the menu opens.</summary>
    public static void Refresh()
    {
        if (_tried) return; _tried = true;
        Task.Run(() =>
    {
        var s = Ps("(Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightness | Select-Object -First 1).CurrentBrightness", true);
        if (int.TryParse(s?.Trim(), out var v)) lock (_lock) { if (_apply == null) _level = v; }
    });
    }

    public static void Change(int delta)
    {
        lock (_lock)
        {
            if (_level < 0) return; // not read yet / unsupported display
            _level = Math.Clamp(_level + delta, 0, 100);
            _apply?.Dispose();
            _apply = new Timer(_ =>
            {
                int target; lock (_lock) { target = _level; _apply = null; }
                Ps($"Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightnessMethods | Invoke-CimMethod -MethodName WmiSetBrightness -Arguments @{{Timeout=1;Brightness={target}}}", false);
            }, null, 120, Timeout.Infinite);
        }
    }

    static string? Ps(string cmd, bool read)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{cmd.Replace("\"", "\\\"")}\"")
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = read };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var o = read ? p.StandardOutput.ReadToEnd() : null;
            p.WaitForExit(5000);
            return o;
        }
        catch { return null; }
    }
}
