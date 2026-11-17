// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using PrinterSwitch.Core;

namespace PrinterSwitch.Ui;

/// <summary>
/// Notification area icons, drawn at runtime in the exact pixel size the taskbar
/// wants (24 px at 150 %), so they are never blurry. Glyphs come from the system
/// icon font, which keeps them consistent with the rest of Windows 11.
/// </summary>
public static class IconArt
{
    // Segoe Fluent Icons (Windows 11) / Segoe MDL2 Assets (Windows 10) code points
    public const string GlyphPrint = "";
    public const string GlyphUsb = "";
    public const string GlyphLock = "";
    public const string GlyphUnknown = "";
    public const string GlyphWarning = "";
    public const string GlyphSettings = "";
    public const string GlyphSync = "";

    public static readonly Color Green = Color.FromArgb(16, 137, 62);
    public static readonly Color Orange = Color.FromArgb(202, 80, 16);
    public static readonly Color Red = Color.FromArgb(196, 43, 28);
    public static readonly Color Gray = Color.FromArgb(110, 110, 110);
    public static readonly Color Blue = Color.FromArgb(0, 103, 192);

    static string? fontName;

    public static string GlyphFont
    {
        get
        {
            if (fontName != null) return fontName;
            using var fonts = new InstalledFontCollection();
            fontName = fonts.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            return fontName;
        }
    }

    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public static int TraySize
    {
        get
        {
            try { return Math.Max(16, (int)Math.Round(16 * GetDpiForSystem() / 96.0)); }
            catch { return 16; }
        }
    }

    public static (Color color, string glyph) Look(PrinterState s) => s switch
    {
        PrinterState.Network => (Green, GlyphPrint),
        PrinterState.UsbHere => (Orange, GlyphUsb),
        PrinterState.UsbElsewhere => (Red, GlyphLock),
        PrinterState.CupsDown => (Red, GlyphWarning),
        PrinterState.NotConfigured => (Gray, GlyphSettings),
        _ => (Gray, GlyphUnknown)
    };

    public static Icon StateIcon(PrinterState s, int size)
    {
        var (color, glyph) = Look(s);
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            float pad = Math.Max(0.5f, size / 32f);
            using (var b = new SolidBrush(color))
                g.FillEllipse(b, pad, pad, size - 2 * pad, size - 2 * pad);
            using var font = new Font(GlyphFont, size * 0.60f, GraphicsUnit.Pixel);
            using var white = new SolidBrush(Color.White);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(glyph, font, white, new RectangleF(0, size * 0.03f, size, size), fmt);
        }
        return ToIcon(bmp);
    }

    /// <summary>One frame of the "working" spinner: accent disc with an orbiting arc.</summary>
    public static Icon BusyIcon(int frame, int frames, int size)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float pad = Math.Max(0.5f, size / 32f);
            using (var b = new SolidBrush(Blue))
                g.FillEllipse(b, pad, pad, size - 2 * pad, size - 2 * pad);
            float w = Math.Max(2f, size / 8f);
            float inset = w / 2 + size / 6f;
            using var pen = new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, inset, inset, size - 2 * inset, size - 2 * inset, 360f * frame / frames, 100f);
        }
        return ToIcon(bmp);
    }

    static Icon ToIcon(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(h).Clone(); }
        finally { DestroyIcon(h); }
    }
}
