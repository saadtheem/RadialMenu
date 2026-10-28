using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using static RadialMenu.Native;

namespace RadialMenu;

/// <summary>
/// Global right-button gesture. Idle -> (right down, swallowed) Pending -> (hold / move) Open -> (release) run action.
/// A quick right click is replayed on release so normal context menus keep working.
/// </summary>
internal sealed class GestureController : IDisposable
{
    enum S { Idle, Pending, Open }

    readonly Config cfg;
    readonly OverlayWindow overlay;
    readonly MenuItem[] items;
    readonly Dispatcher ui = Dispatcher.CurrentDispatcher;
    readonly DispatcherTimer holdTimer, failsafe;
    readonly HookProc proc; // keep alive
    IntPtr hook;
    S state = S.Idle;
    POINT start, last;
    bool updateQueued;

    public bool Enabled { get; set; } = true;

    public GestureController(Config cfg, OverlayWindow overlay, MenuItem[] items)
    {
        this.cfg = cfg; this.overlay = overlay; this.items = items;
        holdTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(cfg.HoldMs) };
        holdTimer.Tick += (_, _) => { holdTimer.Stop(); if (state == S.Pending) OpenMenu(); };
        failsafe = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
        failsafe.Tick += (_, _) => { failsafe.Stop(); if (state == S.Open) { state = S.Idle; overlay.HideNow(); } };
        proc = Callback;
        using var p = Process.GetCurrentProcess();
        hook = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(p.MainModule?.ModuleName), 0);
        if (hook == IntPtr.Zero) throw new InvalidOperationException("Could not install mouse hook (error " + Marshal.GetLastWin32Error() + ")");
    }

    IntPtr Callback(int code, IntPtr w, IntPtr l)
    {
        if (code < 0) return CallNextHookEx(hook, code, w, l);
        var h = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(l);
        if (h.dwExtraInfo == InputSender.Magic) return CallNextHookEx(hook, code, w, l); // our own injected input
        int msg = (int)w;
        bool swallow = false;

        switch (state)
        {
            case S.Idle:
                if (msg == WM_RBUTTONDOWN && Enabled && !IsExcluded())
                {
                    state = S.Pending; start = last = h.pt; holdTimer.Start(); swallow = true;
                }
                break;

            case S.Pending:
                if (msg == WM_MOUSEMOVE)
                {
                    long dx = h.pt.X - start.X, dy = h.pt.Y - start.Y;
                    if (dx * dx + dy * dy >= (long)cfg.MoveThresholdPx * cfg.MoveThresholdPx) { holdTimer.Stop(); last = h.pt; OpenMenu(); }
                }
                else if (msg == WM_RBUTTONUP)
                {
                    holdTimer.Stop(); state = S.Idle; swallow = true;
                    ui.InvokeAsync(InputSender.RightClick);       // it was just a normal click
                }
                break;

            case S.Open:
                switch (msg)
                {
                    case WM_MOUSEMOVE:
                        last = h.pt;
                        if (!updateQueued) { updateQueued = true; ui.InvokeAsync(() => { updateQueued = false; if (state == S.Open) overlay.Update(last); }, DispatcherPriority.Input); }
                        break;
                    case WM_MOUSEWHEEL:
                        int delta = (short)((h.mouseData >> 16) & 0xFFFF);
                        ui.InvokeAsync(() => overlay.Wheel(delta > 0 ? 1 : -1));
                        swallow = true; break;
                    case WM_RBUTTONUP:
                        last = h.pt; state = S.Idle; failsafe.Stop(); swallow = true;
                        ui.InvokeAsync(Commit);
                        break;
                    case WM_LBUTTONDOWN: case WM_LBUTTONUP: case WM_MBUTTONDOWN: case WM_MBUTTONUP: case WM_RBUTTONDOWN:
                        swallow = true; break; // no accidental clicks behind the dial
                }
                break;
        }
        return swallow ? (IntPtr)1 : CallNextHookEx(hook, code, w, l);
    }

    void OpenMenu()
    {
        state = S.Open;
        failsafe.Start();
        ui.InvokeAsync(() =>   // keep the hook callback itself fast
        {
            GetCursorPos(out var cur);
            overlay.ShowAt(cur);
            overlay.Update(cur);
        });
    }

    void Commit()
    {
        overlay.Update(last);
        int c = overlay.Category, ch = overlay.Child;
        overlay.HideNow();
        try
        {
            if (c < 0) return;
            var item = items[c];
            if (ch >= 0 && item.Children != null) item.Children[ch].Action?.Invoke();
            else if (item.Children == null) item.Action?.Invoke();
        }
        catch { }
    }

    bool IsExcluded()
    {
        if (cfg.ExcludedProcesses.Count == 0) return false;
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            using var p = Process.GetProcessById((int)pid);
            foreach (var n in cfg.ExcludedProcesses)
                if (string.Equals(p.ProcessName, n, StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }
        return false;
    }

    public void Dispose() { if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }
}
