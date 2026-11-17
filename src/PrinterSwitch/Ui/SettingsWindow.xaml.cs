// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PrinterSwitch.Core;
using Renci.SshNet.Common;

namespace PrinterSwitch.Ui;

/// <summary>
/// Ordinary settings, laid out like Windows 11 Settings. Simple choices apply at once;
/// the connection is only replaced after a successful test. The setup wizard is a
/// button on the first page.
/// </summary>
public partial class SettingsWindow : Window
{
    public static readonly string[] Pages = ["general", "connection", "printer", "pc", "updates", "about"];

    readonly TrayController tray;
    Settings S => tray.Settings;
    bool loading = true;
    bool busy;
    bool printerLoaded;

    // Connection draft: applied only once a connection with it worked.
    string draftKey = "";
    string draftFp = "";

    public string CurrentPage { get; private set; } = "general";

    sealed record Choice(string Value, string Text)
    {
        public override string ToString() => Text;
    }

    public SettingsWindow(TrayController tray, string? page = null)
    {
        this.tray = tray;
        InitializeComponent();

        // ---- general
        LanguageBox.Items.Add(new Choice("", L.T("wiz.lang.auto")));
        foreach (var (code, name) in L.Languages) LanguageBox.Items.Add(new Choice(code, name));
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<Choice>().FirstOrDefault(c => c.Value == S.Language) ?? LanguageBox.Items[0];
        LanguageBox.SelectionChanged += (_, _) => Language_Changed();

        AutostartSwitch.IsChecked = WindowsSystem.AutostartEnabled;
        AutostartSwitch.Checked += (_, _) => SetAutostart(true);
        AutostartSwitch.Unchecked += (_, _) => SetAutostart(false);

        PinSwitch.IsChecked = WindowsSystem.TrayIconPromoted() ?? S.PromoteTrayPending;
        PinSwitch.Checked += (_, _) => SetPinned(true);
        PinSwitch.Unchecked += (_, _) => SetPinned(false);

        int[] intervals = [15, 30, 45, 60, 120, 300];
        if (!intervals.Contains(S.RefreshSeconds) && S.RefreshSeconds >= 15)
            intervals = [.. intervals.Append(S.RefreshSeconds).Order()];
        foreach (int sec in intervals)
            IntervalBox.Items.Add(new Choice(sec.ToString(), sec < 60 ? $"{sec} s" : sec % 60 == 0 ? $"{sec / 60} min" : $"{sec} s"));
        IntervalBox.SelectedItem = IntervalBox.Items.Cast<Choice>().FirstOrDefault(c => c.Value == S.RefreshSeconds.ToString())
                                   ?? IntervalBox.Items[2];
        IntervalBox.SelectionChanged += (_, _) =>
        {
            if (loading || IntervalBox.SelectedItem is not Choice c) return;
            S.RefreshSeconds = int.Parse(c.Value);
            tray.SaveSettings();
        };

        // ---- connection
        LoadConnection();
        HostBox.TextChanged += (_, _) => UpdateDirty();
        PortBox.TextChanged += (_, _) => UpdateDirty();
        UserBox.TextChanged += (_, _) => UpdateDirty();

        // ---- printer
        FillDevices([], offline: true);
        FillQueues([]);
        DeviceBox.SelectionChanged += (_, _) => Device_Changed();
        QueueBox.SelectionChanged += (_, _) => Queue_Changed();

        // ---- this pc
        LocalPortBox.Text = S.LocalPort.ToString();
        LocalPortBox.LostFocus += (_, _) => ApplyLocalPort();
        LocalPortBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyLocalPort(); };
        FolderCard.Description = Core.Settings.Folder;

        // ---- about
        var v = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionText.Text = L.F("set.version", v?.ToString(3) ?? "?") + " · © 2026 Boris Pavelka";

        AutoUpdateSwitch.IsChecked = S.AutoUpdate;
        AutoUpdateSwitch.Checked += (_, _) => { S.AutoUpdate = true; tray.SaveSettings(); };
        AutoUpdateSwitch.Unchecked += (_, _) => { S.AutoUpdate = false; tray.SaveSettings(); };

        loading = false;
        RefreshUpdates();
        UpdateDirty();
        RefreshStatus();
        Select(page ?? "general");
    }

    // ------------------------------------------------------------ paging ---

    public void Select(string page)
    {
        int i = Math.Max(0, Array.IndexOf(Pages, page));
        Nav.SelectedIndex = i;
    }

    void Nav_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not ListBoxItem item) return;
        CurrentPage = (string)item.Tag;
        var pages = new UIElement[] { PageGeneral, PageConnection, PagePrinter, PagePc, PageUpdates, PageAbout };
        int i = Array.IndexOf(Pages, CurrentPage);
        for (int p = 0; p < pages.Length; p++)
            pages[p].Visibility = p == i ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = CurrentPage switch
        {
            "general" => L.T("set.nav.general"),
            "connection" => L.T("set.nav.connection"),
            "printer" => L.T("set.nav.printer"),
            "pc" => L.T("set.nav.pc"),
            "updates" => L.T("set.nav.updates"),
            _ => L.T("action.about")
        };
        PageScroll.ScrollToTop();
        if (CurrentPage == "printer" && !printerLoaded) _ = LoadPrinterAsync();
        if (CurrentPage == "pc") ShowUsbipState();
        if (CurrentPage == "updates" && tray.LastUpdateCheckAge.TotalMinutes > 10) _ = tray.CheckUpdatesAsync(manual: true);
    }

    /// <summary>Called by the tray whenever the printer state changes.</summary>
    public void RefreshStatus()
    {
        var sw = tray.Switcher;
        var st = sw.State;
        var (color, glyph) = IconArt.Look(st);
        if (sw.Busy) (color, glyph) = (IconArt.Blue, IconArt.GlyphSync);
        Badge.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        BadgeGlyph.Text = glyph;
        string title = sw.Busy ? sw.BusyText : TrayController.StateTitle(st);
        StatusTitle.Text = title;
        NavState.Text = title;
        string sub = S.HasServer ? S.User + "@" + S.Host : tray.StateDetail(st);
        if (S.HasServer && tray.LastCheck != default && !sw.Busy) sub += " · " + L.F("flyout.lastcheck", tray.LastCheck);
        StatusSub.Text = sub;
        StatusRefresh.IsEnabled = !sw.Busy && S.HasServer;
        CupsCard.IsEnabled = S.Host.Length > 0;
    }

    // ----------------------------------------------------------- general ---

    void Wizard_Click(object sender, RoutedEventArgs e) => tray.ShowWizard(0);

    void Language_Changed()
    {
        if (loading || LanguageBox.SelectedItem is not Choice c || c.Value == S.Language) return;
        // Texts are read when windows are built: the tray rebuilds this window too.
        Dispatcher.BeginInvoke(() => tray.ApplyLanguage(c.Value));
    }

    void SetAutostart(bool on)
    {
        if (loading) return;
        try { WindowsSystem.AutostartEnabled = on; }
        catch (Exception ex)
        {
            Log.Write("autostart: " + ex.Message);
            Dialogs.Error(L.T("set.autostart.t"), ex.Message);
        }
    }

    void SetPinned(bool on)
    {
        if (loading) return;
        bool found = WindowsSystem.SetTrayIconPromoted(on);
        // Not registered yet: the tray does it as soon as Windows knows the icon.
        S.PromoteTrayPending = on && !found;
        tray.SaveSettings();
        PinCard.Description = found ? L.T("set.pin.d") : L.T("set.pin.na");
    }

    // -------------------------------------------------------- connection ---

    void LoadConnection()
    {
        bool was = loading;
        loading = true;
        HostBox.Text = S.Host;
        PortBox.Text = S.Port.ToString();
        UserBox.Text = S.User;
        draftKey = S.KeyPath;
        draftFp = S.HostKeyFingerprint;
        loading = was;
        UpdateDirty();
    }

    bool IsDirty() =>
        !string.Equals(HostBox.Text.Trim(), S.Host, StringComparison.OrdinalIgnoreCase) ||
        PortBox.Text.Trim() != S.Port.ToString() ||
        UserBox.Text.Trim() != S.User ||
        draftKey != S.KeyPath ||
        draftFp != S.HostKeyFingerprint;

    void UpdateDirty()
    {
        if (loading) return;
        bool dirty = IsDirty();
        SaveButton.Content = dirty ? L.T("set.conn.save") : L.T("set.conn.test");
        RevertButton.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = !busy;
        KeyCard.Description = draftKey.Length > 0 ? draftKey : L.T("set.key.none");
        FpCard.Description = draftFp.Length > 0 ? "SHA256:" + draftFp : L.T("set.fp.d0");
        ForgetButton.IsEnabled = draftFp.Length > 0;
    }

    void ShowConn(bool? ok, string text, string sub)
    {
        ConnResult.Visibility = Visibility.Visible;
        ConnGlyph.Text = ok switch { true => "", false => "", null => "" };
        ConnGlyph.Foreground = ok switch
        {
            true => new SolidColorBrush(Color.FromRgb(16, 137, 62)),
            false => new SolidColorBrush(Color.FromRgb(196, 43, 28)),
            null => (Brush)FindResource("TextFillColorSecondaryBrush")
        };
        ConnText.Text = text;
        ConnSub.Text = sub;
        ConnSub.Visibility = sub.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void Key_Click(object sender, RoutedEventArgs e)
    {
        string ssh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.T("set.key.t"),
            InitialDirectory = draftKey.Length > 0 ? Path.GetDirectoryName(draftKey) : Directory.Exists(ssh) ? ssh : null,
            Filter = L.T("set.key.filter") + "|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != true) return;
        draftKey = dlg.FileName;
        UpdateDirty();
    }

    void Forget_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(L.T("set.fp.t"), L.T("set.fp.forgetconfirm"), L.T("set.fp.forget"), warning: true)) return;
        draftFp = "";
        UpdateDirty();
    }

    void Revert_Click(object sender, RoutedEventArgs e)
    {
        LoadConnection();
        ConnResult.Visibility = Visibility.Collapsed;
    }

    async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string host = HostBox.Text.Trim(), user = UserBox.Text.Trim();
        if (host.Length == 0 || user.Length == 0) { ShowConn(false, L.T("wiz.server.missing"), ""); return; }
        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            ShowConn(false, L.T("set.port.invalid"), "");
            PortBox.Focus();
            return;
        }
        bool dirty = IsDirty();
        if (dirty && tray.Switcher.State == PrinterState.UsbHere) { ShowConn(false, L.T("set.conn.lent"), ""); return; }

        // Another server (or port) answers with another identity.
        bool sameServer = string.Equals(host, S.Host, StringComparison.OrdinalIgnoreCase) && port == S.Port;
        string trusted = sameServer ? draftFp : "";
        var keys = new List<string>();
        if (draftKey.Length > 0 && File.Exists(draftKey)) keys.Add(draftKey);
        else keys.AddRange(Ssh.CandidateKeys());

        busy = true;
        SaveButton.IsEnabled = false;
        ConnProgress.Visibility = Visibility.Visible;
        ShowConn(null, L.T("set.conn.testing"), "");
        try
        {
            var (key, fp, error) = await Task.Run(() => WizardWindow.TryKeys(host, port, user, keys, trusted));
            if (error is HostKeyMismatchException hk &&
                Dialogs.Confirm(L.T("set.fp.t"), L.T("err.hostkey") + "\n\nSHA256:" + hk.Actual, L.T("action.yes"), warning: true))
            {
                (key, fp, error) = await Task.Run(() => WizardWindow.TryKeys(host, port, user, keys, ""));
            }

            if (key != null)
            {
                if (dirty || key != S.KeyPath || fp != S.HostKeyFingerprint)
                {
                    var n = S.Clone();
                    n.Host = host;
                    n.Port = port;
                    n.User = user;
                    n.KeyPath = key;
                    n.HostKeyFingerprint = fp;
                    tray.ApplyConnection(n);
                    LoadConnection();
                    printerLoaded = false;
                }
                ShowConn(true, L.F("set.conn.ok", user + "@" + host), L.F("wiz.server.keyused", Path.GetFileName(key)));
            }
            else if (error is null or SshAuthenticationException)
            {
                ShowConn(false, L.T("err.auth"), L.T("set.conn.needkey"));
            }
            else
            {
                ShowConn(false, Ssh.Describe(error), error.Message);
            }
        }
        finally
        {
            busy = false;
            ConnProgress.Visibility = Visibility.Collapsed;
            UpdateDirty();
        }
    }

    async void StatusRefresh_Click(object sender, RoutedEventArgs e) => await tray.RefreshAsync();

    void Prepare_Click(object sender, RoutedEventArgs e) => tray.ShowWizard(S.HasServer ? 2 : 1);
    void Pi_Click(object sender, RoutedEventArgs e) => tray.ShowWizard(S.HasServer ? 3 : 1);
    void Cups_Click(object sender, RoutedEventArgs e) { if (S.Host.Length > 0) tray.OpenCups(); }

    // ----------------------------------------------------------- printer ---

    readonly Dictionary<string, string> deviceDescriptions = new();

    void Load_Click(object sender, RoutedEventArgs e) => _ = LoadPrinterAsync();

    async Task LoadPrinterAsync()
    {
        if (!S.HasServer) { LoadText.Text = L.T("state.notconfigured.t"); return; }
        printerLoaded = true;
        LoadButton.IsEnabled = false;
        LoadProgress.Visibility = Visibility.Visible;
        LoadText.Text = L.T("set.printer.loading");
        try
        {
            var snapshot = S.Clone();
            var (st, devices) = await Task.Run(() =>
            {
                using var server = new Server(snapshot);
                var st = server.GetStatus();
                return (st, st.Reachable ? server.GetDevices() : []);
            });
            if (!st.Reachable)
            {
                LoadText.Text = L.T("set.printer.offline");
                printerLoaded = false;
                return;
            }
            FillDevices(devices, offline: false);
            FillQueues(st.Queues);
            LoadText.Text = devices.Any(d => d.IsPrinter) ? S.Host : L.T("wiz.prep.nodevice");
        }
        catch (Exception ex)
        {
            Log.Write("settings: loading printers failed: " + ex.Message);
            LoadText.Text = L.T("set.printer.offline");
            printerLoaded = false;
        }
        finally
        {
            LoadProgress.Visibility = Visibility.Collapsed;
            LoadButton.IsEnabled = true;
        }
    }

    void FillDevices(List<UsbDevice> devices, bool offline)
    {
        bool was = loading;
        loading = true;
        DeviceBox.Items.Clear();
        deviceDescriptions.Clear();
        DeviceBox.Items.Add(new Choice("", L.T("wiz.prep.auto")));
        int select = 0;
        foreach (var d in devices.OrderByDescending(d => d.IsPrinter))
        {
            deviceDescriptions[d.VidPid] = d.Description;
            DeviceBox.Items.Add(new Choice(d.VidPid, d.ToString()));
            if (S.Device.Length > 0 && (d.VidPid == S.Device || d.BusId == S.Device)) select = DeviceBox.Items.Count - 1;
        }
        if (S.Device.Length > 0 && select == 0)
        {
            // Saved printer is switched off (or the server was not asked): keep the choice.
            string name = S.DeviceName.Length > 0 ? S.DeviceName + "  (" + S.Device + ")" : S.Device;
            deviceDescriptions[S.Device] = S.DeviceName;
            DeviceBox.Items.Add(new Choice(S.Device, name));
            select = DeviceBox.Items.Count - 1;
        }
        DeviceBox.SelectedIndex = select;
        loading = was;
    }

    void FillQueues(List<string> queues)
    {
        bool was = loading;
        loading = true;
        QueueBox.Items.Clear();
        int select = -1;
        foreach (string q in queues)
        {
            QueueBox.Items.Add(new Choice(q, q));
            if (q == S.Queue) select = QueueBox.Items.Count - 1;
        }
        if (S.Queue.Length > 0 && select < 0)
        {
            QueueBox.Items.Add(new Choice(S.Queue, S.Queue));
            select = QueueBox.Items.Count - 1;
        }
        QueueBox.SelectedIndex = select;
        QueueBox.IsEnabled = QueueBox.Items.Count > 0;
        loading = was;
    }

    void Device_Changed()
    {
        if (loading || DeviceBox.SelectedItem is not Choice d) return;
        S.Device = d.Value;
        S.DeviceName = d.Value.Length > 0 ? deviceDescriptions.GetValueOrDefault(d.Value, "") : "";
        tray.SaveSettings();
    }

    void Queue_Changed()
    {
        if (loading || QueueBox.SelectedItem is not Choice q) return;
        S.Queue = q.Value;
        tray.SaveSettings();
    }

    async void AddPrinter_Click(object sender, RoutedEventArgs e)
    {
        AddPrinterButton.IsEnabled = false;
        try { await tray.AddNetworkPrinterAsync(this); }
        finally { AddPrinterButton.IsEnabled = true; }
    }

    void WinPrinters_Click(object sender, RoutedEventArgs e) => WindowsSystem.Open("ms-settings:printers");

    // ----------------------------------------------------------- this pc ---

    void ShowUsbipState()
    {
        string? exe = Usbip.FindExe();
        string? ver = exe == null ? null : Usbip.Version(exe);
        InstallText.Visibility = InstallText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.Content = L.F("wiz.pc.install", Usbip.RecommendedVersion);
        if (exe == null)
        {
            UsbipCard.Description = L.T("wiz.pc.missing");
            InstallButton.Visibility = Visibility.Visible;
        }
        else if (Usbip.IsKnownBad(ver))
        {
            UsbipCard.Description = L.F("wiz.pc.installed", ver ?? "?") + "\n" + L.F("wiz.pc.bad", ver ?? "?", Usbip.RecommendedVersion);
            InstallButton.Visibility = Visibility.Visible;
        }
        else
        {
            UsbipCard.Description = L.F("wiz.pc.installed", ver ?? "?") + "\n" + exe;
            InstallButton.Visibility = Visibility.Collapsed;
        }
    }

    async void Install_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        InstallText.Visibility = Visibility.Visible;
        InstallProgress.Visibility = Visibility.Visible;
        InstallProgress.Value = 0;
        var progress = new Progress<double>(p =>
        {
            InstallProgress.Value = p;
            InstallText.Text = p < 1 ? L.F("wiz.pc.downloading", p) : L.T("wiz.pc.installing");
        });
        try
        {
            int code = await Usbip.DownloadAndInstall(progress, CancellationToken.None);
            Log.Write("usbip installer exit code " + code);
            InstallText.Text = code == 0 ? L.T("wiz.pc.restart") : L.F("wiz.pc.installfailed", code);
        }
        catch (Exception ex)
        {
            Log.Write("usbip install: " + ex);
            InstallText.Text = L.F("wiz.pc.installfailed", ex.Message);
        }
        finally
        {
            InstallProgress.Visibility = Visibility.Collapsed;
            InstallButton.IsEnabled = true;
            ShowUsbipState();
        }
    }

    void ApplyLocalPort()
    {
        if (!int.TryParse(LocalPortBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            LocalPortBox.Text = S.LocalPort.ToString();
            return;
        }
        if (port == S.LocalPort) return;
        if (tray.Switcher.State == PrinterState.UsbHere)
        {
            LocalPortBox.Text = S.LocalPort.ToString();
            Dialogs.Info(L.T("set.port.t"), L.T("set.conn.lent"));
            return;
        }
        var n = S.Clone();
        n.LocalPort = port;
        tray.ApplyConnection(n);
    }

    void Log_Click(object sender, RoutedEventArgs e) => TrayController.OpenLog();

    void Folder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Core.Settings.Folder);
        WindowsSystem.Open(Core.Settings.Folder);
    }

    // ----------------------------------------------------------- updates ---

    bool installing;

    /// <summary>Called by the tray whenever an update check starts or ends.</summary>
    public void RefreshUpdates()
    {
        var u = tray.AvailableUpdate;
        string installed = L.F("upd.installed", Updater.Current.ToString(3));
        string last = S.LastUpdateCheck == default ? L.T("upd.notchecked") : L.F("upd.lastcheck", S.LastUpdateCheck);
        Color c;
        string glyph, title;
        if (tray.CheckingUpdates) (c, glyph, title) = (Color.FromRgb(0, 103, 192), "\uE895", L.T("upd.checking"));
        else if (u != null) (c, glyph, title) = (Color.FromRgb(0, 103, 192), "\uE896", L.F("upd.available", u.Version.ToString(3)));
        else if (tray.UpdateError.Length > 0) (c, glyph, title) = (Color.FromRgb(202, 80, 16), "\uE7BA", tray.UpdateError);
        else if (S.LastUpdateCheck == default) (c, glyph, title) = (Color.FromRgb(110, 110, 110), "\uE895", L.T("upd.notchecked"));
        else (c, glyph, title) = (Color.FromRgb(16, 137, 62), "\uE73E", L.T("upd.uptodate"));
        UpdBadge.Background = new SolidColorBrush(c);
        UpdBadgeGlyph.Text = glyph;
        UpdTitle.Text = title;
        UpdSub.Text = installed + " · " + last;

        bool install = u != null;
        UpdButton.Content = install ? L.T("upd.install") : L.T("upd.check");
        if (install) UpdButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
        else UpdButton.ClearValue(StyleProperty);
        UpdButton.IsEnabled = !tray.CheckingUpdates && !installing;
        NotesCard.Visibility = u is { PageUrl.Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        if (u != null) NotesCard.Description = L.F("upd.notes.d", u.Version.ToString(3));
    }

    async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (tray.AvailableUpdate == null)
        {
            await tray.CheckUpdatesAsync(manual: true);
            return;
        }
        installing = true;
        RefreshUpdates();
        UpdText.Visibility = Visibility.Visible;
        UpdProgress.Visibility = Visibility.Visible;
        UpdProgress.Value = 0;
        UpdText.Text = L.F("upd.downloading", 0.0);
        var progress = new Progress<double>(p =>
        {
            UpdProgress.Value = p;
            UpdText.Text = p < 1 ? L.F("upd.downloading", p) : L.T("upd.starting");
        });
        string? error = await tray.InstallUpdateAsync(progress);
        // null = the installer is running and the app is closing.
        if (error == null) return;
        installing = false;
        UpdProgress.Visibility = Visibility.Collapsed;
        UpdText.Text = error;
        UpdText.Visibility = error.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshUpdates();
    }

    void Notes_Click(object sender, RoutedEventArgs e)
    {
        if (tray.AvailableUpdate is { PageUrl.Length: > 0 } u) WindowsSystem.Open(u.PageUrl);
    }

    void Releases_Click(object sender, RoutedEventArgs e) => WindowsSystem.Open(Dialogs.RepoUrl + "/releases");

    // ------------------------------------------------------------- about ---

    void GitHub_Click(object sender, RoutedEventArgs e) => WindowsSystem.Open(Dialogs.RepoUrl);

    void Notices_Click(object sender, RoutedEventArgs e)
    {
        string local = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");
        WindowsSystem.Open(File.Exists(local) ? local : Dialogs.RepoUrl + "/blob/main/THIRD-PARTY-NOTICES.md");
    }
}
