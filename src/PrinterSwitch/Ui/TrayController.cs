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
    SettingsWindow? settingsWindow;
    string settingsPage = "general";
    bool returnToSettings;
    PrinterState lastState = PrinterState.Unknown;
    DateTime flyoutHiddenAt;
    bool exiting;

    public Switcher Switcher => switcher;
    public Settings Settings => settings;

    /// <summary>Newer release found on GitHub, or null.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }
    public bool CheckingUpdates { get; private set; }
    /// <summary>The result of an earlier run is not kept, so a check counts only once made in this one.</summary>
    public bool UpdatesCheckedThisRun { get; private set; }
    public string UpdateError { get; private set; } = "";
    readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromSeconds(90) };
    Action? balloonAction;
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
        icon.BalloonTipClicked += (_, _) => (balloonAction ?? ShowFlyout)();

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

        // First look shortly after start, then hourly; GitHub is asked at most once a day.
        updateTimer.Tick += async (_, _) =>
        {
            updateTimer.Interval = TimeSpan.FromHours(1);
            if (settings.AutoUpdate && (!UpdatesCheckedThisRun || (DateTime.Now - settings.LastUpdateCheck).TotalHours >= 20))
                await CheckUpdatesAsync(manual: false);
        };
        updateTimer.Start();
    }

    // ---------------------------------------------------------- updates ---

    public async Task CheckUpdatesAsync(bool manual)
    {
        if (CheckingUpdates) return;
        CheckingUpdates = true;
        settingsWindow?.RefreshUpdates();
        try
        {
            AvailableUpdate = await Updater.CheckAsync();
            UpdateError = "";
            UpdatesCheckedThisRun = true;
            settings.LastUpdateCheck = DateTime.Now;
            try { settings.Save(); } catch { }
            Log.Write("update check: " + (AvailableUpdate?.Version.ToString() ?? "up to date"));
        }
        catch (Exception e)
        {
            UpdateError = L.T("upd.checkfailed");
            Log.Write("update check failed: " + e.Message);
        }
        finally
        {
            CheckingUpdates = false;
        }
        if (AvailableUpdate is { } u && !manual && settings.NotifiedUpdate != u.Version.ToString())
        {
            settings.NotifiedUpdate = u.Version.ToString();
            try { settings.Save(); } catch { }
            Notify(L.F("upd.notify", u.Version.ToString(3)), WinForms.ToolTipIcon.Info, () => ShowSettings("updates"));
        }
        UpdateUi();
        settingsWindow?.RefreshUpdates();
    }

    /// <summary>Downloads, verifies and starts the installer, then quits. Returns an error text or null.</summary>
    public async Task<string?> InstallUpdateAsync(IProgress<double> progress)
    {
        if (AvailableUpdate is not { } u) return null;
        if (!Updater.CanInstall(u)) return L.T("upd.manual");
        if (switcher.Busy) return switcher.BusyText;
        if (switcher.State == PrinterState.UsbHere)
        {
            if (!Dialogs.Confirm(L.T("upd.install"), L.T("upd.lent"), L.T("action.return"))) return "";
            var r = await switcher.ReturnToNetworkAsync();
            if (!r.Ok) return r.Message;
        }
        VerifiedInstaller installer;
        try { installer = await Updater.DownloadAsync(u, progress); }
        catch (Exception e)
        {
            Log.Write("update download failed: " + e);
            return L.F("upd.failed", e is System.IO.InvalidDataException ? e.Message : Ssh.Describe(e));
        }

        // The installer refuses to run while this instance holds its mutex.
        var app = (App)Application.Current;
        app.ReleaseInstance();
        bool started;
        using (installer) started = Updater.RunInstaller(installer);
        if (!started)
        {
            app.ReclaimInstance();
            return L.T("upd.declined");
        }
        Log.Write("update installer started; exiting");
        exiting = true;
        Application.Current.Shutdown();
        return null;
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
        settingsWindow?.RefreshStatus();
    }

    public static string StateTitle(PrinterState s) => L.T("state." + s.ToString().ToLowerInvariant() + ".t");

    public string StateDetail(PrinterState s)
        => s == PrinterState.Unreachable ? switcher.Detail : L.T("state." + s.ToString().ToLowerInvariant() + ".d");

    public void Notify(string text, WinForms.ToolTipIcon kind = WinForms.ToolTipIcon.Info, Action? onClick = null)
    {
        balloonAction = onClick;
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

    /// <param name="step">-1 = welcome page on first run, server page otherwise.</param>
    public void ShowWizard(int step = -1) => OpenWizard(settings.Clone(), step);

    public void ShowSettings(string? page = null)
    {
        flyout?.Hide();
        if (wizard != null) { wizard.Activate(); return; }
        if (settingsWindow != null)
        {
            if (page != null) settingsWindow.Select(page);
            if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
            settingsWindow.Activate();
            return;
        }
        var w = new SettingsWindow(this, page ?? settingsPage);
        settingsWindow = w;
        w.Closed += (_, _) =>
        {
            settingsPage = w.CurrentPage;
            if (settingsWindow == w) settingsWindow = null;
        };
        w.Show();
        w.Activate();
    }

    /// <summary>Saves settings changed in place (not the connection) and applies them.</summary>
    public void SaveSettings()
    {
        try { settings.Save(); }
        catch (Exception e) { Log.Write("settings could not be saved: " + e.Message); }
        refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(15, settings.RefreshSeconds));
        UpdateUi();
    }

    /// <summary>Replaces the settings with ones that change how the server is reached.</summary>
    public void ApplyConnection(Settings s)
    {
        settings = s;
        switcher.Reconfigure(settings);
        SaveSettings();
        SchedulePromotion();
        _ = RefreshAsync();
    }

    void OpenWizard(Settings draft, int step)
    {
        flyout?.Hide();
        if (wizard != null) { wizard.Activate(); return; }
        // Settings and the wizard edit the same things: one at a time.
        if (settingsWindow != null)
        {
            var w = settingsWindow;
            settingsPage = w.CurrentPage;
            settingsWindow = null;
            returnToSettings = true;
            w.Close();
        }
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
            bool back = returnToSettings;
            returnToSettings = false;
            if (current.Result is { } result)
            {
                ApplyConnection(result);
                wizard = null;
                ApplyLanguage(settings.Language);
                if (back) ShowSettings(settingsPage);
                else ShowFlyout();
            }
            else
            {
                wizard = null;
                if (back) ShowSettings(settingsPage);
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
        // Same for the settings window: reopen it in place.
        if (settingsWindow is { } old)
        {
            settingsWindow = null;
            ShowSettings(old.CurrentPage);
            if (settingsWindow is { } now && old.WindowState == WindowState.Normal)
            {
                now.Left = old.Left; now.Top = old.Top; now.Width = old.Width; now.Height = old.Height;
            }
            old.Close();
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
        menu.Items.Add(Item(L.T("action.settings"), IconArt.GlyphSettings, () => ShowSettings()));
        menu.Items.Add(Item(L.T("action.log"), "", OpenLog));
        menu.Items.Add(Item(L.T("action.about"), "", () => ShowSettings("about")));
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
        updateTimer.Stop();
        icon.Visible = false;
        icon.Dispose();
        switcher.Dispose();
    }
}
