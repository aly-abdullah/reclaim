using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Win32;
using Reclaim.Core;

namespace Reclaim.Ui;

internal static class TrayIconRenderer
{
    public static bool TaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    public static Icon Render(double ratio, bool lightTaskbar, int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float stroke = Math.Max(2f, size * 0.16f);
            float inset = stroke / 2f + size * 0.03f;
            var ring = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);

            var trackColor = lightTaskbar ? Color.FromArgb(55, 0, 0, 0) : Color.FromArgb(70, 255, 255, 255);
            using (var track = new Pen(trackColor, stroke))
                g.DrawEllipse(track, ring);

            var (start, end) = ratio switch
            {
                >= 0.9 => (Color.FromArgb(0xF5, 0x6B, 0x5B), Color.FromArgb(0xF2, 0x45, 0x5A)),
                >= 0.8 => (Color.FromArgb(0xF5, 0xB5, 0x24), Color.FromArgb(0xF5, 0x85, 0x24)),
                _ => (Color.FromArgb(0x8E, 0x7F, 0xFF), Color.FromArgb(0x2B, 0xD4, 0xE8))
            };
            using (var brush = new LinearGradientBrush(new RectangleF(0, 0, size, size), start, end, 45f))
            using (var arc = new Pen(brush, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(arc, ring, -90, (float)(360 * Math.Clamp(ratio, 0.03, 1)));

            float dot = size * 0.2f;
            using var center = new SolidBrush(lightTaskbar ? Color.FromArgb(0x1A, 0x1D, 0x26) : Color.White);
            g.FillEllipse(center, (size - dot) / 2f, (size - dot) / 2f, dot, dot);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }
}
