// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PrinterSwitch.Core;

namespace PrinterSwitch.Ui;

/// <summary>The panel that opens above the notification area, like Windows' own quick settings.</summary>
public partial class FlyoutWindow : Window
{
    readonly TrayController tray;
    public event Action? Hidden;

    public FlyoutWindow(TrayController tray)
    {
        this.tray = tray;
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyBackdrop();
        Deactivated += (_, _) => HideFlyout();
        SizeChanged += (_, _) => { if (IsVisible) Place(); };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) HideFlyout(); };
        Refresh();
    }

    void HideFlyout()
    {
        if (!IsVisible) return;
        Hide();
        Hidden?.Invoke();
    }

    public void ShowNearTray()
    {
        Refresh();
        Show();
        UpdateLayout();
        Place();
        Activate();
        MainButton.Focus();
    }

    // Anchored to the corner next to the clock; re-run whenever the height changes.
    void Place()
    {
        var wa = SystemParameters.WorkArea;
        const double gap = 12;
        Left = wa.Right - ActualWidth - gap;
        // Taskbar at the top leaves the work area starting lower.
        Top = wa.Top > 0 ? wa.Top + gap : wa.Bottom - ActualHeight - gap;
    }

    public void Refresh()
    {
        var sw = tray.Switcher;
        var s = sw.Settings;
        var st = sw.State;

        var (color, glyph) = IconArt.Look(st);
        Badge.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        BadgeGlyph.Text = glyph;

        string device = s.DeviceName.Length > 0 ? s.DeviceName : "";
        Subtitle.Text = s.Host.Length == 0 ? "" : device.Length > 0 ? device + " · " + s.Host : s.Host;

        if (sw.Busy)
        {
            Badge.Background = new SolidColorBrush(Color.FromRgb(IconArt.Blue.R, IconArt.Blue.G, IconArt.Blue.B));
            BadgeGlyph.Text = IconArt.GlyphSync;
            StateTitle.Text = sw.BusyText;
            StateDetail.Text = "";
            BusyBar.Visibility = Visibility.Visible;
        }
        else
        {
            StateTitle.Text = TrayController.StateTitle(st);
            StateDetail.Text = tray.StateDetail(st);
            BusyBar.Visibility = Visibility.Collapsed;
        }
        StateDetail.Visibility = StateDetail.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        MainButton.Visibility = Visibility.Visible;
        MainButton.IsEnabled = !sw.Busy;
        switch (st)
        {
            case PrinterState.Network:
                MainText.Text = L.T("action.connect"); MainGlyph.Text = IconArt.GlyphUsb; break;
            case PrinterState.UsbHere:
            case PrinterState.CupsDown:
                MainText.Text = L.T("action.return"); MainGlyph.Text = IconArt.GlyphPrint; break;
            case PrinterState.UsbElsewhere:
                MainText.Text = L.T("action.forcereturn"); MainGlyph.Text = IconArt.GlyphPrint; break;
            case PrinterState.NotConfigured:
                MainText.Text = L.T("action.setup"); MainGlyph.Text = IconArt.GlyphSettings; break;
            case PrinterState.Unreachable:
                MainText.Text = L.T("action.refresh"); MainGlyph.Text = ""; break;
            default:
                MainButton.Visibility = Visibility.Collapsed; break;
        }

        UpdateLink.Visibility = tray.AvailableUpdate != null ? Visibility.Visible : Visibility.Collapsed;
        if (tray.AvailableUpdate is { } u) UpdateLinkText.Text = L.F("upd.flyout", u.Version.ToString(3));

        bool hasServer = s.Host.Length > 0;
        CupsLink.Visibility = PrinterLink.Visibility = hasServer ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !sw.Busy && st != PrinterState.NotConfigured;
        LastCheck.Text = tray.LastCheck == default ? "" : L.F("flyout.lastcheck", tray.LastCheck);
        LastCheck.Visibility = LastCheck.Text.Length > 0 && !sw.Busy ? Visibility.Visible : Visibility.Collapsed;
    }

    async void Main_Click(object sender, RoutedEventArgs e)
    {
        switch (tray.Switcher.State)
        {
            case PrinterState.Network:
                HideFlyout(); await tray.ConnectAsync(); break;
            case PrinterState.UsbHere:
            case PrinterState.UsbElsewhere:
            case PrinterState.CupsDown:
                HideFlyout(); await tray.ReturnAsync(); break;
            case PrinterState.NotConfigured:
                tray.ShowWizard(); break;
            default:
                await tray.RefreshAsync(); break;
        }
    }

    void Settings_Click(object sender, RoutedEventArgs e) { HideFlyout(); tray.ShowSettings(); }
    void Update_Click(object sender, RoutedEventArgs e) { HideFlyout(); tray.ShowSettings("updates"); }
    void Cups_Click(object sender, RoutedEventArgs e) { HideFlyout(); tray.OpenCups(); }
    async void Printer_Click(object sender, RoutedEventArgs e) { HideFlyout(); await tray.AddNetworkPrinterAsync(null); }
    void Log_Click(object sender, RoutedEventArgs e) { HideFlyout(); TrayController.OpenLog(); }
    async void Refresh_Click(object sender, RoutedEventArgs e) => await tray.RefreshAsync();

    // ------------------------------------------------------------ backdrop ---

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    const int DWMSBT_TRANSIENTWINDOW = 3; // acrylic, like system flyouts

    void ApplyBackdrop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = WindowsSystem.AppsUseDarkTheme() ? 1 : 0;
        int round = 2;
        int backdrop = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        bool ok = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, 4) == 0
                  && Environment.OSVersion.Version.Build >= 22621;
        if (ok)
        {
            Background = Brushes.Transparent;
            if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } ct })
                ct.BackgroundColor = Colors.Transparent;
        }
        else
        {
            SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        }
    }
}
