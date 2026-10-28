using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RadialMenu;

/// <summary>Appearance settings with a live, self-animating preview of the dial next to the window.</summary>
internal sealed class SettingsWindow : Window
{
    static readonly (string name, string hex)[] Accents =
    {
        ("Blue", "0A84FF"), ("Teal", "40C8E0"), ("Green", "30D158"), ("Orange", "FF9F0A"), ("Pink", "FF375F"), ("Purple", "BF5AF2"),
    };

    readonly Config cfg;
    readonly OverlayWindow overlay;
    readonly DispatcherTimer demo = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    int step;

    public SettingsWindow(Config cfg, OverlayWindow overlay)
    {
        this.cfg = cfg; this.overlay = overlay;
        Title = "Radial Menu — Appearance";
        Width = 360; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)); Foreground = Brushes.White;
        WindowStartupLocation = WindowStartupLocation.Manual; Left = 60; Top = 90;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        BuildUi();

        demo.Tick += (_, _) => overlay.PreviewStep(step++);
        ContentRendered += (_, _) => StartPreview();
        Closed += (_, _) => { demo.Stop(); overlay.HideNow(); cfg.Save(); };
    }

    void StartPreview()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int rightPx = (int)((Left + ActualWidth) * dpi), midY = (int)((Top + ActualHeight / 2) * dpi);
        var (s, _) = overlay.Measure(new Native.POINT { X = rightPx + 200, Y = midY });
        int cx = rightPx + s / 2 + 16;
        if (cx + s / 2 > SystemParameters.PrimaryScreenWidth * dpi) cx = (int)(Left * dpi) - s / 2 - 16;
        overlay.ShowAt(new Native.POINT { X = cx, Y = midY });
        step = 0; demo.Start();
    }

    void BuildUi()
    {
        var sp = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        sp.Children.Add(new TextBlock { Text = "Appearance", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) });
        sp.Children.Add(new TextBlock { Text = "Changes apply live to the dial on the right.", Opacity = .6, Margin = new Thickness(0, 0, 0, 14) });

        AddSlider("Glass blur", 0, 40, cfg.BlurRadius, v => cfg.BlurRadius = (int)v, sp);
        AddSlider("Tint", 0, 100, cfg.TintStrength, v => cfg.TintStrength = (int)v, sp);
        AddSlider("Size", 60, 140, cfg.SizePercent, v => cfg.SizePercent = (int)v, sp, "%");
        AddSlider("Animation speed", 50, 200, cfg.AnimSpeed, v => cfg.AnimSpeed = (int)v, sp, "%");

        var anim = new CheckBox { Content = "Animations", IsChecked = cfg.Animations, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 14) };
        anim.Click += (_, _) => { cfg.Animations = anim.IsChecked == true; overlay.Reapply(); };
        sp.Children.Add(anim);

        sp.Children.Add(Caption("Glass style"));
        var styles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 14) };
        foreach (var s in new[] { "Dark", "Light", "Clear" })
        {
            var rb = new RadioButton { Content = s, GroupName = "style", Foreground = Brushes.White, Margin = new Thickness(0, 0, 18, 0), IsChecked = cfg.Style.Equals(s, StringComparison.OrdinalIgnoreCase) };
            rb.Checked += (_, _) => { cfg.Style = s; overlay.Reapply(); };
            styles.Children.Add(rb);
        }
        sp.Children.Add(styles);

        sp.Children.Add(Caption("Highlight colour"));
        var sw = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 18) };
        foreach (var (name, hex) in Accents)
        {
            var c = (Color)ColorConverter.ConvertFromString("#" + hex);
            var dot = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand,
                Background = new SolidColorBrush(c), ToolTip = name,
                BorderThickness = new Thickness(2), BorderBrush = cfg.Accent.Equals(hex, StringComparison.OrdinalIgnoreCase) ? Brushes.White : Brushes.Transparent,
            };
            dot.MouseLeftButtonDown += (_, _) =>
            {
                cfg.Accent = hex; overlay.Reapply();
                foreach (Border b in sw.Children) b.BorderBrush = Brushes.Transparent;
                dot.BorderBrush = Brushes.White;
            };
            sw.Children.Add(dot);
        }
        sp.Children.Add(sw);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var reset = new Button { Content = "Reset", Padding = new Thickness(16, 5, 16, 5), Margin = new Thickness(0, 0, 8, 0) };
        reset.Click += (_, _) => { cfg.ResetAppearance(); BuildUi(); overlay.Reapply(); };
        var done = new Button { Content = "Done", Padding = new Thickness(18, 5, 18, 5), IsDefault = true };
        done.Click += (_, _) => Close();
        row.Children.Add(reset); row.Children.Add(done);
        sp.Children.Add(row);

        Content = sp;
    }

    TextBlock Caption(string s) => new() { Text = s, Opacity = .75 };

    void AddSlider(string name, double min, double max, double value, Action<double> set, Panel host, string unit = "")
    {
        var head = new DockPanel();
        var val = new TextBlock { Text = $"{(int)value}{unit}", Opacity = .6 };
        DockPanel.SetDock(val, Dock.Right);
        head.Children.Add(val); head.Children.Add(Caption(name));
        var sl = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 4, 0, 12), IsMoveToPointEnabled = true };
        sl.ValueChanged += (_, e) => { set(e.NewValue); val.Text = $"{(int)e.NewValue}{unit}"; overlay.Reapply(); };
        host.Children.Add(head); host.Children.Add(sl);
    }
}
