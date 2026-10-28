using System;
using System.Runtime.InteropServices;

namespace RadialMenu;

/// <summary>Grabs the pixels under the dial so we can blur them (real frosted glass).</summary>
internal static class ScreenCapture
{
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    public static System.Windows.Media.Imaging.BitmapSource? Grab(int x, int y, int size)
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(size, size));
            IntPtr h = bmp.GetHbitmap();
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    h, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                var opaque = new System.Windows.Media.Imaging.FormatConvertedBitmap(src, System.Windows.Media.PixelFormats.Bgr32, null, 0);
                opaque.Freeze();
                return opaque;
            }
            finally { DeleteObject(h); }
        }
        catch { return null; }
    }
}
