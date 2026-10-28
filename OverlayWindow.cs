using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RadialMenu;

/// <summary>
/// Transparent, click-through, non-activating window that draws the two-ring "liquid glass" dial.
/// The glass is real: the pixels under the dial are captured, blurred and tinted.
/// </summary>
internal sealed class OverlayWindow : Window
{
    // Design units (the window is scaled to fit your screen)
    const double Box = 520, R0 = 30, R1 = 112, R2 = 186, Gap = 1.2;

    readonly MenuItem[] items;
    readonly Config cfg;
    readonly Canvas root = new() { Width = Box, Height = Box, RenderTransformOrigin = new Point(.5, .5) };
    readonly Canvas outerHost = new() { Width = Box, Height = Box, IsHitTestVisible = false };
    readonly ScaleTransform rootScale = new(1, 1);
    readonly RotateTransform rootRot = new(0);
    readonly List<SolidColorBrush> catBrushes = new(), childBrushes = new();
    readonly List<TextBlock> catGlyphs = new(), childGlyphs = new();
    readonly Dictionary<SolidColorBrush, Color> lastColor = new();
    readonly ScaleTransform hubScale = new(1, 1);
    readonly TextBlock hubIcon = new() { FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol") };
    readonly TextBlock label = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI") };
    readonly Border pill = new() { Width = 270, CornerRadius = new CornerRadius(15), Padding = new Thickness(12, 5, 12, 6), BorderThickness = new Thickness(1) };

    BitmapSource? backdrop;
    Color tint, hoverC, dimC;
    Brush textBrush = Brushes.White, rimBrush = Brushes.White, glossBrush = Brushes.Transparent;
    int cat = -1, child = -1, hideGen;
    string lastHub = "";
    Native.POINT centerPx;
    double unit = 1;

    public int Category => cat;
    public int Child => child;

    public OverlayWindow(MenuItem[] items, Config cfg)
    {
        this.items = items; this.cfg = cfg;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Focusable = false; Width = Box; Height = Box; Left = -20000; Top = -20000;
        root.RenderTransform = new TransformGroup { Children = { rootScale, rootRot } };
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = root };
        new WindowInteropHelper(this).EnsureHandle();
    }

    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e); ApplyExStyle(); }

    void ApplyExStyle()
    {
        var h = new WindowInteropHelper(this).Handle;
        long ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, (IntPtr)ex);
    }

    // ================= theme =================
    static Color Hex(string s, Color fallback)
    {
        try
        {
            s = s.TrimStart('#');
            return Color.FromRgb(byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber), byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber), byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber));
        }
        catch { return fallback; }
    }

    static Brush Frozen(Brush b) { b.Freeze(); return b; }

    void ComputeTheme()
    {
        bool light = cfg.Style.Equals("Light", StringComparison.OrdinalIgnoreCase);
        bool clear = cfg.Style.Equals("Clear", StringComparison.OrdinalIgnoreCase);
        var baseC = light ? Color.FromRgb(0xF4, 0xF6, 0xFC) : clear ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x14, 0x16, 0x1E);
        double a = 14 + Math.Clamp(cfg.TintStrength, 0, 100) / 100.0 * 205;
        if (clear) a *= 0.45;
        if (backdrop == null) a = Math.Max(a, 0xB0);       // no blur source -> keep it readable
        tint = Color.FromArgb((byte)a, baseC.R, baseC.G, baseC.B);
        var acc = Hex(cfg.Accent, Color.FromRgb(0x0A, 0x84, 0xFF));
        hoverC = Color.FromArgb(0xC8, acc.R, acc.G, acc.B);
        dimC = Color.FromArgb(0x78, acc.R, acc.G, acc.B);
        textBrush = Frozen(new SolidColorBrush(light ? Color.FromRgb(0x1C, 0x1C, 0x1E) : Colors.White));
        byte rimHi = (byte)(light ? 0xFF : 0xE6);
        rimBrush = Frozen(new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(rimHi, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(0x30, 255, 255, 255), 0.45),
            new GradientStop(Color.FromArgb(0x90, 255, 255, 255), 1),
        }, new Point(0, 0), new Point(1, 1)));
        byte g1 = (byte)(light ? 0x70 : 0x3A);
        glossBrush = Frozen(new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(g1, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(0x0C, 255, 255, 255), 0.55),
            new GradientStop(Color.FromArgb(0x00, 255, 255, 255), 1),
        }, new Point(0, 0), new Point(0, 1)));
    }

    // ================= geometry =================
    static Point P(double r, double a) { var t = a * Math.PI / 180; return new Point(Box / 2 + r * Math.Sin(t), Box / 2 - r * Math.Cos(t)); }

    static Geometry Wedge(double r0, double r1, double a0, double a1)
    {
        bool large = (a1 - a0) > 180;
        var f = new PathFigure { StartPoint = P(r1, a0), IsClosed = true };
        f.Segments.Add(new ArcSegment(P(r1, a1), new Size(r1, r1), 0, large, SweepDirection.Clockwise, true));
        f.Segments.Add(new LineSegment(P(r0, a1), true));
        f.Segments.Add(new ArcSegment(P(r0, a0), new Size(r0, r0), 0, large, SweepDirection.Counterclockwise, true));
        var g = new PathGeometry(); g.Figures.Add(f); g.Freeze(); return g;
    }

    static double ChildWidth(int count) => Math.Min(40, 300.0 / Math.Max(count, 1));

    TextBlock Glyph(string s, double size, Point at)
    {
        var t = new TextBlock { Text = s, FontSize = size, Foreground = textBrush, FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"), IsHitTestVisible = false, Tag = 1.0 };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = t.DesiredSize.Width, h = t.DesiredSize.Height;
        Canvas.SetLeft(t, at.X - w / 2); Canvas.SetTop(t, at.Y - h / 2);
        t.RenderTransform = new ScaleTransform(1, 1, w / 2, h / 2);
        return t;
    }

    FrameworkElement? MakeBackdrop(Geometry clip)
    {
        if (backdrop == null) return null;
        var img = new Image { Source = backdrop, Width = Box, Height = Box, Stretch = Stretch.Fill, IsHitTestVisible = false };
        if (cfg.BlurRadius > 0) img.Effect = new BlurEffect { Radius = cfg.BlurRadius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
        var host = new Canvas { Width = Box, Height = Box, Clip = clip, IsHitTestVisible = false };
        host.Children.Add(img);
        return host;
    }

    UIElement SoftShadow(Geometry g) => new Path
    {
        Data = g, Fill = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), IsHitTestVisible = false,
        Effect = new BlurEffect { Radius = 14 }, RenderTransform = new TranslateTransform(0, 5)
    };

    // ================= build =================
    void Build()
    {
        root.Children.Clear(); outerHost.Children.Clear();
        catBrushes.Clear(); catGlyphs.Clear(); childBrushes.Clear(); childGlyphs.Clear(); lastColor.Clear(); lastHub = "";
        ComputeTheme();
        int n = items.Length; double sec = 360.0 / n;

        var geos = new List<Geometry>(); var all = new GeometryGroup();
        for (int i = 0; i < n; i++)
        {
            double c = i * sec;
            var g = Wedge(R0 + 4, R1, c - sec / 2 + Gap, c + sec / 2 - Gap);
            geos.Add(g); all.Children.Add(g);
        }
        all.Children.Add(new EllipseGeometry(new Point(Box / 2, Box / 2), R0, R0));
        all.Freeze();

        root.Children.Add(SoftShadow(all));
        var bd = MakeBackdrop(all); if (bd != null) root.Children.Add(bd);

        for (int i = 0; i < n; i++)
        {
            var br = new SolidColorBrush(tint); catBrushes.Add(br);
            root.Children.Add(new Path { Data = geos[i], Fill = br, Stroke = rimBrush, StrokeThickness = 1.1, IsHitTestVisible = false });
            root.Children.Add(new Path { Data = geos[i], Fill = glossBrush, IsHitTestVisible = false });
            var gl = Glyph(items[i].Icon, 22, P((R0 + R1) / 2 + 2, i * sec)); catGlyphs.Add(gl); root.Children.Add(gl);
        }

        root.Children.Add(outerHost);

        var hub = new Grid { Width = R0 * 2, Height = R0 * 2, RenderTransformOrigin = new Point(.5, .5), RenderTransform = hubScale, IsHitTestVisible = false };
        hub.Children.Add(new Ellipse { Fill = new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, tint.A + 40), tint.R, tint.G, tint.B)), Stroke = rimBrush, StrokeThickness = 1.1 });
        hub.Children.Add(new Ellipse { Fill = glossBrush });
        hubIcon.Foreground = textBrush;
        if (hubIcon.Parent is Panel old) old.Children.Remove(hubIcon);
        hub.Children.Add(hubIcon);
        Canvas.SetLeft(hub, Box / 2 - R0); Canvas.SetTop(hub, Box / 2 - R0);
        root.Children.Add(hub);

        pill.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, tint.A + 30), tint.R, tint.G, tint.B));
        pill.BorderBrush = rimBrush; label.Foreground = textBrush;
        if (pill.Child == null) pill.Child = label;
        if (pill.Parent is Panel op) op.Children.Remove(pill);
        Canvas.SetLeft(pill, Box / 2 - 135); Canvas.SetTop(pill, Box / 2 + R2 + 8);
        root.Children.Add(pill);
    }

    void RebuildOuter(bool animate)
    {
        outerHost.Children.Clear(); childBrushes.Clear(); childGlyphs.Clear();
        if (cat < 0 || items[cat].Children is not { } kids) return;

        double w = ChildWidth(kids.Length), start = cat * 360.0 / items.Length - kids.Length * w / 2;
        var geos = new List<Geometry>(); var all = new GeometryGroup();
        for (int i = 0; i < kids.Length; i++)
        {
            var g = Wedge(R1 + 6, R2, start + i * w + Gap, start + (i + 1) * w - Gap);
            geos.Add(g); all.Children.Add(g);
        }
        all.Freeze();

        var scale = new ScaleTransform(1, 1); var rot = new RotateTransform(0);
        var host = new Canvas { Width = Box, Height = Box, RenderTransformOrigin = new Point(.5, .5), IsHitTestVisible = false, RenderTransform = new TransformGroup { Children = { scale, rot } } };
        host.Children.Add(SoftShadow(all));
        var bd = MakeBackdrop(all); if (bd != null) host.Children.Add(bd);
        for (int i = 0; i < kids.Length; i++)
        {
            var br = new SolidColorBrush(tint); childBrushes.Add(br);
            host.Children.Add(new Path { Data = geos[i], Fill = br, Stroke = rimBrush, StrokeThickness = 1.1, IsHitTestVisible = false });
            host.Children.Add(new Path { Data = geos[i], Fill = glossBrush, IsHitTestVisible = false });
            var gl = Glyph(kids[i].Icon, 19, P((R1 + R2) / 2 + 3, start + i * w + w / 2)); childGlyphs.Add(gl); host.Children.Add(gl);
        }
        outerHost.Children.Add(host);

        if (animate && cfg.Animations)
        {
            Pop(scale, .82, 1, 260);
            host.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, D(170)));
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-16, 0, D(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            for (int i = 0; i < childGlyphs.Count; i++) Pop((ScaleTransform)childGlyphs[i].RenderTransform, 0, 1, 240, 40 + i * 28);
        }
    }

    // ================= animation helpers =================
    TimeSpan D(double ms) => TimeSpan.FromMilliseconds(cfg.Animations ? ms * 100.0 / Math.Max(10, cfg.AnimSpeed) : 0);

    void Pop(ScaleTransform st, double from, double to, double ms, double delay = 0)
    {
        if (!cfg.Animations) { st.BeginAnimation(ScaleTransform.ScaleXProperty, null); st.BeginAnimation(ScaleTransform.ScaleYProperty, null); st.ScaleX = st.ScaleY = to; return; }
        DoubleAnimation Make() => new(from, to, D(ms)) { BeginTime = D(delay), EasingFunction = new BackEase { Amplitude = .55, EasingMode = EasingMode.EaseOut } };
        st.ScaleX = st.ScaleY = from;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, Make());
        st.BeginAnimation(ScaleTransform.ScaleYProperty, Make());
    }

    void AnimColor(SolidColorBrush b, Color to)
    {
        var from = lastColor.TryGetValue(b, out var f) ? f : tint;
        if (from == to) return;
        lastColor[b] = to;
        if (!cfg.Animations) { b.BeginAnimation(SolidColorBrush.ColorProperty, null); b.Color = to; return; }
        b.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, D(140)));
    }

    void AnimGlyph(TextBlock g, double to)
    {
        double cur = g.Tag is double d ? d : 1.0;
        if (cur == to) return;
        g.Tag = to;
        var st = (ScaleTransform)g.RenderTransform;
        if (!cfg.Animations) { st.ScaleX = st.ScaleY = to; return; }
        var a = new DoubleAnimation(to, D(170)) { EasingFunction = new BackEase { Amplitude = .6, EasingMode = EasingMode.EaseOut } };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a.Clone());
    }

    // ================= show / hide =================
    /// <summary>Pixel size of the window at this point (auto-fits the monitor) and pixels per design unit.</summary>
    public (int size, double unit) Measure(Native.POINT p)
    {
        var mon = Native.MonitorFromPoint(p, 2);
        double dpi = Native.GetDpiForMonitor(mon, 0, out var dx, out _) == 0 ? dx / 96.0 : 1.0;
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        double hDip = Native.GetMonitorInfo(mon, ref mi) ? (mi.rcMonitor.B - mi.rcMonitor.T) / dpi : 1080;
        double f = Math.Clamp(hDip / 1080.0, 0.8, 1.3) * Math.Clamp(cfg.SizePercent, 40, 200) / 100.0;
        int s = Math.Max(180, (int)Math.Round(Box * f * dpi));
        return (s, s / Box);
    }

    void Place(int s)
    {
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST,
            centerPx.X - s / 2, centerPx.Y - s / 2, s, s, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    public void ShowAt(Native.POINT px, bool recapture = true)
    {
        hideGen++;
        if (IsVisible && recapture) { BeginAnimation(OpacityProperty, null); Opacity = 0; Hide(); }
        centerPx = px;
        var (s, u) = Measure(px); unit = u;
        if (recapture) backdrop = ScreenCapture.Grab(px.X - s / 2, px.Y - s / 2, s);
        cat = child = -1;
        Build(); Refresh();

        Opacity = 0; ApplyExStyle();
        if (!IsVisible) Show();
        Place(s);

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, D(140)));
        if (cfg.Animations)
        {
            Pop(rootScale, .7, 1, 330);
            rootRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-24, 0, D(380)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            Pop(hubScale, .3, 1, 320, 40);
            for (int i = 0; i < catGlyphs.Count; i++) Pop((ScaleTransform)catGlyphs[i].RenderTransform, 0, 1, 280, 50 + i * 26);
        }
        else { rootScale.ScaleX = rootScale.ScaleY = 1; rootRot.Angle = 0; }
    }

    /// <summary>Re-apply appearance settings live (used by the settings window).</summary>
    public void Reapply()
    {
        if (!IsVisible) return;
        var (s, u) = Measure(centerPx); unit = u;
        Place(s);
        Build(); RebuildOuter(false); Refresh();
    }

    public void HideNow()
    {
        int gen = ++hideGen;
        var fade = new DoubleAnimation(Opacity, 0, D(100));
        fade.Completed += (_, _) => { if (gen == hideGen) Hide(); };
        BeginAnimation(OpacityProperty, fade);
        if (cfg.Animations)
        {
            var a = new DoubleAnimation(rootScale.ScaleX, .9, D(120)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, a.Clone());
        }
    }

    // ================= interaction =================
    public void Update(Native.POINT px)
    {
        double dx = (px.X - centerPx.X) / unit, dy = (px.Y - centerPx.Y) / unit;
        double r = Math.Sqrt(dx * dx + dy * dy);
        int n = items.Length; double sec = 360.0 / n;
        int newCat = cat, newChild = -1;
        if (r < R0) newCat = -1;
        else
        {
            double ang = Math.Atan2(dx, -dy) * 180 / Math.PI; if (ang < 0) ang += 360;
            if (r <= R1 + 4 || cat < 0)
                newCat = (int)Math.Floor(((ang + sec / 2) % 360) / sec);
            else if (items[cat].Children is { } kids)
            {
                double w = ChildWidth(kids.Length), start = cat * sec - kids.Length * w / 2;
                double rel = ((ang - start) % 360 + 360) % 360;
                if (rel < kids.Length * w) newChild = (int)(rel / w);
            }
        }
        if (newCat != cat)
        {
            cat = newCat; RebuildOuter(true);
            if (cat >= 0 && items[cat].Label == "Brightness" && BrightnessController.Level < 0) BrightnessController.Refresh();
        }
        child = newChild;
        Refresh();
    }

    public void Wheel(int dir)
    {
        if (cat < 0 || items[cat].Wheel is not { } w) return;
        w(dir);
        Refresh();
    }

    // ---- used by the settings window to demo the dial ----
    public void PreviewStep(int step)
    {
        int n = items.Length; double sec = 360.0 / n;
        int c = (step / 2) % n;
        if (step % 2 == 0) { Update(Polar(c * sec, (R0 + R1) / 2)); return; }
        int m = items[c].Children?.Length ?? 0;
        if (m == 0) return;
        double w = ChildWidth(m), start = c * sec - m * w / 2;
        int idx = (step / 2 / n + step / 2) % m;
        Update(Polar(start + (idx + .5) * w, (R1 + R2) / 2));
    }

    Native.POINT Polar(double deg, double r)
    {
        double t = deg * Math.PI / 180;
        return new Native.POINT { X = (int)(centerPx.X + r * unit * Math.Sin(t)), Y = (int)(centerPx.Y - r * unit * Math.Cos(t)) };
    }

    void Refresh()
    {
        for (int i = 0; i < catBrushes.Count; i++)
        {
            AnimColor(catBrushes[i], i != cat ? tint : (child >= 0 ? dimC : hoverC));
            AnimGlyph(catGlyphs[i], i == cat ? 1.22 : 1.0);
        }
        for (int i = 0; i < childBrushes.Count; i++)
        {
            AnimColor(childBrushes[i], i == child ? hoverC : tint);
            AnimGlyph(childGlyphs[i], i == child ? 1.28 : 1.0);
        }

        string hub = cat >= 0 ? items[cat].Icon : "✕";
        if (hub != lastHub) { lastHub = hub; hubIcon.Text = hub; Pop(hubScale, .65, 1, 220); }

        string? t = null;
        if (cat >= 0 && child >= 0 && items[cat].Children is { } k) t = k[child].Label;
        else if (cat >= 0) t = items[cat].Status?.Invoke() ?? items[cat].Label;
        if (cat >= 0 && child < 0 && items[cat].Wheel != null) t += "  ·  scroll";
        label.Text = t ?? "";
        pill.Visibility = t == null ? Visibility.Hidden : Visibility.Visible;
    }
}
