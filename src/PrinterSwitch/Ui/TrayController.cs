// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PrinterSwitch.Core;
using WinForms = System.Windows.Forms;

namespace PrinterSwitch.Ui;

/// <summary>Owns the notification area icon and ties the switcher to the UI.</summary>
public sealed class TrayController : IDisposable
{
    readonly WinForms.NotifyIcon icon = new();
    readonly DispatcherTimer refreshTimer = new();
    readonly DispatcherTimer spinTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    readonly Dictionary<PrinterState, System.Drawing.Icon> stateIcons = new();
    readonly List<System.Drawing.Icon> busyIcons = new();
    const int SpinFrames = 12;
    int spinFrame;

    Settings settings;
    readonly Switcher switcher;
    FlyoutWindow? flyout;
    WizardWindow? wizard;
    PrinterState lastState = PrinterState.Unknown;
    DateTime flyoutHiddenAt;
    bool exiting;

    public Switcher Switcher => switcher;
    public DateTime LastCheck { get; private set; }

    public TrayController(Settings s)
    {
        settings = s;
        WinForms.Application.EnableVisualStyles(); // modern task dialogs

        int size = IconArt.TraySize;
        foreach (PrinterState st in Enum.GetValues<PrinterState>())
            stateIcons[st] = IconArt.StateIcon(st, size);
        for (int i = 0; i < SpinFrames; i++)
            busyIcons.Add(IconArt.BusyIcon(i, SpinFrames, size));

        switcher = new Switcher(s);
        switcher.Changed += () => Application.Current.Dispatcher.BeginInvoke(UpdateUi);

        icon.Icon = stateIcons[switcher.State];
        icon.Text = L.T("app.name");
        icon.Visible = true;
        icon.MouseUp += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) ToggleFlyout();
            else if (e.Button == WinForms.MouseButtons.Right) ShowMenu();
        };
        icon.BalloonTipClicked += (_, _) => ShowFlyout();

        spinTimer.Tick += (_, _) =>
        {
            spinFrame = (spinFrame + 1) % SpinFrames;
            icon.Icon = busyIcons[spinFrame];
        };

        refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(15, s.RefreshSeconds));
        refreshTimer.Tick += async (_, _) => await RefreshAsync();
        refreshTimer.Start();

        _ = RefreshAsync();
        SchedulePromotion();
    }

    public async Task RefreshAsync()
    {
        await switcher.RefreshAsync();
        LastCheck = DateTime.Now;
        UpdateUi();
    }

    // Windows creates the icon's registry entry only after it has been shown once.
    void SchedulePromotion()
    {
        if (!settings.PromoteTrayPending) return;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (WindowsSystem.PromoteTrayIcon())
            {
                settings.PromoteTrayPending = false;
                try { settings.Save(); } catch { }
            }
        };
        t.Start();
    }

    void UpdateUi()
    {
        var st = switcher.State;
        if (switcher.Busy)
        {
            if (!spinTimer.IsEnabled) { spinFrame = 0; spinTimer.Start(); }
        }
        else
        {
            spinTimer.Stop();
            icon.Icon = stateIcons[st];
        }

        string text = L.T("app.name") + "\n" + (switcher.Busy ? switcher.BusyText : StateTitle(st));
        icon.Text = text.Length > 127 ? text[..127] : text;

        if (st != lastState && !switcher.Busy)
        {
            if (st == PrinterState.UsbElsewhere && lastState is PrinterState.Network or PrinterState.UsbHere)
                Notify(L.T("notify.taken"), WinForms.ToolTipIcon.Info);
            lastState = st;
        }

        flyout?.Refresh();
    }

    public static string StateTitle(PrinterState s) => L.T("state." + s.ToString().ToLowerInvariant() + ".t");

    public string StateDetail(PrinterState s)
        => s == PrinterState.Unreachable ? switcher.Detail : L.T("state." + s.ToString().ToLowerInvariant() + ".d");

    public void Notify(string text, WinForms.ToolTipIcon kind = WinForms.ToolTipIcon.Info)
    {
        icon.BalloonTipTitle = L.T("app.name");
        icon.BalloonTipText = text;
        icon.BalloonTipIcon = kind;
        icon.ShowBalloonTip(6000);
    }

    // ---------------------------------------------------------- windows ---

    public void ShowMain()
    {
        if (!settings.Configured) ShowWizard();
        else ShowFlyout();
    }

    void ToggleFlyout()
    {
        if (flyout is { IsVisible: true }) { flyout.Hide(); return; }
        // A click on the icon first deactivates (and hides) the flyout; don't reopen it.
        if ((DateTime.Now - flyoutHiddenAt).TotalMilliseconds < 400) return;
        ShowFlyout();
    }

    public void ShowFlyout()
    {
        if (flyout == null)
        {
            flyout = new FlyoutWindow(this);
            flyout.Hidden += () => flyoutHiddenAt = DateTime.Now;
        }
        flyout.ShowNearTray();
        if (switcher.State != PrinterState.NotConfigured && (DateTime.Now - LastCheck).TotalSeconds > 10)
            _ = RefreshAsync();
    }

    public void ShowWizard() => OpenWizard(settings.Clone(), -1);

    void OpenWizard(Settings draft, int step)
    {
        flyout?.Hide();
        if (wizard != null) { wizard.Activate(); return; }
        wizard = new WizardWindow(draft, step);
        bool switching = false;
        wizard.LanguageSwitch += (d, st) =>
        {
            // Language changes apply at once: rebuild the windows in the new language.
            ApplyLanguage(d.Language);
            switching = true;
            var old = wizard;
            wizard = null;
            OpenWizard(d, st);
            old?.Close();
        };
        var current = wizard;
        current.Closed += (_, _) =>
        {
            if (switching) return;
            if (current.Result is { } result)
            {
                settings = result;
                settings.Save();
                switcher.Reconfigure(settings);
                refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(15, settings.RefreshSeconds));
                SchedulePromotion();
                _ = RefreshAsync();
                wizard = null;
                ApplyLanguage(settings.Language);
                ShowFlyout();
            }
            else
            {
                wizard = null;
            }
        };
        current.Show();
        current.Activate();
    }

    public void ApplyLanguage(string code)
    {
        if (settings.Language != code)
        {
            settings.Language = code;
            try { settings.Save(); } catch { }
        }
        L.Init(code);
        // The flyout is built with texts of the old language; make a new one.
        if (flyout != null)
        {
            flyout.Close();
            flyout = null;
        }
        UpdateUi();
    }

    void ShowMenu()
    {
        var st = switcher.State;
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = switcher.Busy ? switcher.BusyText : StateTitle(st), IsEnabled = false });
        menu.Items.Add(new Separator());
        if (st == PrinterState.Network)
            menu.Items.Add(Item(L.T("action.connect"), IconArt.GlyphUsb, async () => await ConnectAsync(), !switcher.Busy));
        if (st is PrinterState.UsbHere or PrinterState.UsbElsewhere or PrinterState.CupsDown)
            menu.Items.Add(Item(L.T("action.return"), IconArt.GlyphPrint, async () => await ReturnAsync(), !switcher.Busy));
        menu.Items.Add(Item(L.T("action.refresh"), IconArt.GlyphSync, async () => await RefreshAsync(), !switcher.Busy));
        menu.Items.Add(new Separator());
        if (settings.Host.Length > 0)
        {
            menu.Items.Add(Item(L.T("action.cups"), "", OpenCups));
            menu.Items.Add(Item(L.T("action.addprinter"), "", async () => await AddNetworkPrinterAsync(null), !switcher.Busy));
        }
        menu.Items.Add(Item(L.T("action.settings"), IconArt.GlyphSettings, ShowWizard));
        menu.Items.Add(Item(L.T("action.log"), "", OpenLog));
        menu.Items.Add(Item(L.T("action.about"), "", Dialogs.About));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(L.T("action.exit"), "", Exit));

        // A context menu needs a foreground window, otherwise it does not close
        // when the user clicks elsewhere.
        var host = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, Width = 0, Height = 0,
            AllowsTransparency = true, Background = null, Left = -10000, Top = -10000
        };
        host.Show();
        host.Activate();
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.Closed += (_, _) => host.Close();
        menu.IsOpen = true;
    }

    static MenuItem Item(string header, string glyph, Action click, bool enabled = true)
    {
        var mi = new MenuItem
        {
            Header = header,
            IsEnabled = enabled,
            Icon = new TextBlock { Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14 }
        };
        mi.Click += (_, _) => click();
        return mi;
    }

    // ---------------------------------------------------------- actions ---

    public async Task ConnectAsync()
    {
        if (switcher.Busy) return;
        if (!Dialogs.Confirm(L.T("action.connect"), L.T("confirm.connect"), L.T("action.connect"))) return;
        var r = await switcher.ConnectHereAsync();
        Report(r);
    }

    public async Task ReturnAsync()
    {
        if (switcher.Busy) return;
        if (switcher.State == PrinterState.UsbElsewhere &&
            !Dialogs.Confirm(L.T("action.return"), L.T("confirm.force"), L.T("action.forcereturn"), warning: true))
            return;
        var r = await switcher.ReturnToNetworkAsync();
        Report(r);
    }

    void Report(OpResult r)
    {
        if (r.Ok) Notify(r.Message);
        else Dialogs.Error(L.T("notify.failed"), r.Message);
    }

    public void OpenCups() => WindowsSystem.Open($"http://{settings.Host}:631/printers/");

    public static void OpenLog()
    {
        if (!System.IO.File.Exists(Log.FilePath)) Log.Write("log created");
        WindowsSystem.Open(Log.FilePath);
    }

    /// <summary>Adds the server's CUPS queue to Windows over IPP.</summary>
    public async Task AddNetworkPrinterAsync(Window? owner)
    {
        if (switcher.LastStatus == null) await RefreshAsync();
        string queue = settings.Queue.Length > 0 ? settings.Queue : switcher.LastStatus?.Queues.FirstOrDefault() ?? "";
        if (queue.Length == 0) { Dialogs.Error(L.T("action.addprinter"), L.T("printer.noqueue")); return; }
        string url = $"http://{settings.Host}:631/printers/{Uri.EscapeDataString(queue)}";
        await AddNetworkPrinterAsync(url, this);
    }

    public static async Task AddNetworkPrinterAsync(string url, TrayController? tray)
    {
        Log.Write("adding network printer " + url);
        int err = await Task.Run(() => WindowsSystem.AddNetworkPrinter(url));
        if (err == WindowsSystem.ErrorNotSupported)
        {
            if (!Dialogs.Confirm(L.T("action.addprinter"), L.T("printer.needfeature"), L.T("action.yes"))) return;
            bool on = await Task.Run(WindowsSystem.EnableInternetPrintingClient);
            if (on) err = await Task.Run(() => WindowsSystem.AddNetworkPrinter(url));
        }
        Log.Write("add printer result: " + err);
        if (err == 0)
        {
            if (tray != null) tray.Notify(L.T("printer.added"));
            else Dialogs.Info(L.T("action.addprinter"), L.T("printer.added"));
        }
        else
        {
            Dialogs.Error(L.T("action.addprinter"), L.F("printer.failed", err));
            WindowsSystem.Open("ms-settings:printers");
        }
    }

    public void Exit()
    {
        if (exiting) return;
        if (switcher.State == PrinterState.UsbHere)
        {
            var answer = Dialogs.YesNoCancel(L.T("action.exit"), L.T("confirm.exit"));
            if (answer == null) return;
            if (answer == true)
            {
                var r = switcher.ReturnToNetworkBlocking();
                if (!r.Ok) Dialogs.Error(L.T("notify.failed"), r.Message);
            }
        }
        exiting = true;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        refreshTimer.Stop();
        spinTimer.Stop();
        icon.Visible = false;
        icon.Dispose();
        switcher.Dispose();
    }
}
