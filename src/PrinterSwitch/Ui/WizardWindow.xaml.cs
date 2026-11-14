// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrinterSwitch.Core;
using Renci.SshNet.Common;

namespace PrinterSwitch.Ui;

public partial class WizardWindow : Window
{
    const int Steps = 6;
    readonly Settings s;
    readonly string originalHost;
    int step;
    ServerInfo? info;
    bool modeChosen;

    bool connected;
    bool helperReady;
    bool checkedServer;
    bool busy;
    ServerStatus? status;

    /// <summary>Set when the wizard is finished; null when cancelled.</summary>
    public Settings? Result { get; private set; }

    /// <summary>The user picked another language: reopen the wizard with these settings at this step.</summary>
    public event Action<Settings, int>? LanguageSwitch;

    sealed record Choice(string Value, string Text)
    {
        public override string ToString() => Text;
    }

    public WizardWindow(Settings settings, int startStep = -1)
    {
        s = settings;
        originalHost = s.Host;
        InitializeComponent();

        HostBox.Text = s.Host;
        UserBox.Text = s.User;
        PortBox.Text = s.Port.ToString();
        HostBox.TextChanged += (_, _) => InvalidateConnection();
        UserBox.TextChanged += (_, _) => InvalidateConnection();
        PortBox.TextChanged += (_, _) => InvalidateConnection();

        AutostartBox.IsChecked = !s.Configured || WindowsSystem.AutostartEnabled;
        PromoteBox.IsChecked = !s.Configured;

        foreach (var box in new[] { LanguageBox, WelcomeLanguageBox })
        {
            box.Items.Add(new Choice("", L.T("wiz.lang.auto")));
            foreach (var (code, name) in L.Languages) box.Items.Add(new Choice(code, name));
            box.SelectedItem = box.Items.Cast<Choice>().FirstOrDefault(c => c.Value == s.Language) ?? box.Items[0];
            box.SelectionChanged += (_, _) =>
            {
                string code = ((Choice)box.SelectedItem).Value;
                if (code == s.Language) return;
                s.Language = code;
                LanguageSwitch?.Invoke(s, step);
            };
        }

        DedicatedBox.Checked += (_, _) => UpdateOptBoxes();
        DedicatedBox.Unchecked += (_, _) => UpdateOptBoxes();

        // Reopened from the tray: skip the welcome page.
        Show(startStep >= 0 ? startStep : s.Configured ? 1 : 0);
    }

    // ------------------------------------------------------------ paging ---

    void Show(int i)
    {
        step = i;
        var pages = new UIElement[] { Page0, Page1, Page2, PagePi, PagePc, PageDone };
        for (int p = 0; p < pages.Length; p++)
            pages[p].Visibility = p == i ? Visibility.Visible : Visibility.Collapsed;

        StepList.Items.Clear();
        for (int p = 0; p < Steps; p++)
        {
            bool done = p < i, current = p == i;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 14) };
            var dot = new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(current || done ? 0 : 1.5)
            };
            dot.SetResourceReference(Border.BorderBrushProperty, "TextFillColorTertiaryBrush");
            if (current || done) dot.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
            var label = new TextBlock
            {
                Text = done ? "" : (p + 1).ToString(),
                FontFamily = done ? (FontFamily)FindResource("GlyphFont") : FontFamily,
                FontSize = done ? 11 : 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (current || done) label.SetResourceReference(TextBlock.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
            else label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            dot.Child = label;
            row.Children.Add(dot);
            var text = new TextBlock
            {
                Text = L.T("wiz.step" + (p + 1)), FontSize = 14, Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center, FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal
            };
            if (!current && !done) text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            row.Children.Add(text);
            StepList.Items.Add(row);
        }

        BackButton.IsEnabled = i > 0;
        NextButton.Content = i == Steps - 1 ? L.T("wiz.finish") : L.T("wiz.next");
        UpdateButtons();

        if (i == 1 && !connected && s.HasServer && HostBox.Text == s.Host) _ = ConnectAsync(quiet: true);
        if (i == 2 && !checkedServer) _ = CheckServerAsync();
        if (i == 3) _ = ShowPiStateAsync();
        if (i == 4) ShowUsbipState();
        if (i == 1) HostBox.Focus();
        if (i == 1 && !discovered && HostBox.Text.Trim().Length == 0) _ = DiscoverAsync();
    }

    void UpdateButtons()
    {
        NextButton.IsEnabled = !busy && (step != 1 || connected);
        BackButton.IsEnabled = !busy && step > 0;
    }

    void SetBusy(bool b)
    {
        busy = b;
        UpdateButtons();
    }

    void Back_Click(object sender, RoutedEventArgs e) { if (step > 0) Show(step - 1); }

    void Next_Click(object sender, RoutedEventArgs e)
    {
        if (step == 1 && !connected) return;
        if (step == 2 && !helperReady &&
            !Dialogs.Confirm(L.T("wiz.prep.h"), L.T("wiz.prep.skipwarn"), L.T("wiz.next"), warning: true))
            return;
        if (step < Steps - 1) { Show(step + 1); return; }
        Finish();
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    void Finish()
    {
        if (DeviceBox.SelectedItem is Choice d)
        {
            s.Device = d.Value;
            s.DeviceName = d.Value.Length > 0 ? DeviceDescriptions.GetValueOrDefault(d.Value, "") : "";
        }
        if (QueueBox.SelectedItem is Choice q) s.Queue = q.Value;
        if (PromoteBox.IsChecked == true) s.PromoteTrayPending = true;
        s.Configured = true;
        try { WindowsSystem.AutostartEnabled = AutostartBox.IsChecked == true; }
        catch (Exception e) { Log.Write("autostart: " + e.Message); }
        Result = s;
        Close();
    }

    // ------------------------------------------------------------ server ---

    void InvalidateConnection()
    {
        if (!connected) return;
        connected = false;
        checkedServer = false;
        ServerResult.Visibility = Visibility.Collapsed;
        UpdateButtons();
    }

    bool ReadServerFields()
    {
        string host = HostBox.Text.Trim(), user = UserBox.Text.Trim();
        if (host.Length == 0 || user.Length == 0)
        {
            ShowServerResult(false, L.T("wiz.server.missing"), "");
            return false;
        }
        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port is < 1 or > 65535) port = 22;
        if (!string.Equals(host, s.Host, StringComparison.OrdinalIgnoreCase)) s.HostKeyFingerprint = "";
        s.Host = host;
        s.User = user;
        s.Port = port;
        return true;
    }

    void ShowServerResult(bool ok, string text, string sub)
    {
        ServerResult.Visibility = Visibility.Visible;
        ServerResultGlyph.Text = ok ? "" : "";
        ServerResultGlyph.Foreground = new SolidColorBrush(ok ? Color.FromRgb(16, 137, 62) : Color.FromRgb(196, 43, 28));
        ServerResultText.Text = text;
        ServerResultSub.Text = sub;
        ServerResultSub.Visibility = sub.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    async void Connect_Click(object sender, RoutedEventArgs e) => await ConnectAsync(quiet: false);

    async Task ConnectAsync(bool quiet)
    {
        if (busy || !ReadServerFields()) return;
        SetBusy(true);
        ConnectButton.IsEnabled = false;
        ConnectProgress.Visibility = Visibility.Visible;
        PasswordPanel.Visibility = Visibility.Collapsed;
        try
        {
            string host = s.Host, user = s.User, trusted = s.HostKeyFingerprint;
            int port = s.Port;
            var keys = new List<string>();
            if (s.KeyPath.Length > 0 && System.IO.File.Exists(s.KeyPath)) keys.Add(s.KeyPath);
            keys.AddRange(Ssh.CandidateKeys().Where(k => !keys.Contains(k)));

            (string? key, string fp, Exception? error) = await Task.Run(() => TryKeys(host, port, user, keys, trusted));

            if (error is HostKeyMismatchException hk && !quiet &&
                Dialogs.Confirm(L.T("wiz.server.h"), L.T("err.hostkey") + "\n\nSHA256:" + hk.Actual, L.T("action.yes"), warning: true))
            {
                s.HostKeyFingerprint = "";
                (key, fp, error) = await Task.Run(() => TryKeys(host, port, user, keys, ""));
            }

            if (key != null)
            {
                OnConnected(key, fp);
            }
            else if (error is null or SshAuthenticationException)
            {
                if (!quiet) ShowServerResult(false, L.T("err.auth"), "");
                PasswordPanel.Visibility = Visibility.Visible;
                PasswordBox.Focus();
            }
            else if (!quiet)
            {
                ShowServerResult(false, Ssh.Describe(error), error.Message);
            }
        }
        finally
        {
            ConnectButton.IsEnabled = true;
            ConnectProgress.Visibility = Visibility.Collapsed;
            SetBusy(false);
        }
    }

    /// <summary>Tries each key; stops on the first network-level error.</summary>
    static (string? key, string fp, Exception? error) TryKeys(string host, int port, string user, List<string> keys, string trusted)
    {
        Exception? last = null;
        foreach (string k in keys)
        {
            try
            {
                using var c = Ssh.Connect(host, port, user, Ssh.KeyAuth(user, k), trusted, out string fp);
                return (k, fp, null);
            }
            catch (SshAuthenticationException e) { last = e; }
            catch (Exception e) { return (null, "", e); }
        }
        return (null, "", last);
    }

    void OnConnected(string key, string fp)
    {
        connected = true;
        s.KeyPath = key;
        s.HostKeyFingerprint = fp;
        PasswordPanel.Visibility = Visibility.Collapsed;
        PasswordBox.Clear();
        ShowServerResult(true, L.F("wiz.server.ok", s.User + "@" + s.Host),
            L.F("wiz.server.keyused", System.IO.Path.GetFileName(key)) + "\n" + L.F("wiz.server.fp", fp));
        UpdateButtons();
    }

    async void SetupKey_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !ReadServerFields()) return;
        string password = PasswordBox.Password;
        if (password.Length == 0) { PasswordBox.Focus(); return; }
        SetBusy(true);
        ConnectProgress.Visibility = Visibility.Visible;
        try
        {
            string host = s.Host, user = s.User, trusted = s.HostKeyFingerprint;
            int port = s.Port;
            var (key, fp) = await Task.Run(() =>
            {
                string k = Ssh.EnsureOwnKey();
                Server.AuthorizeKey(host, port, user, password, Ssh.PublicKeyLineFor(k), trusted, out string f);
                using var c = Ssh.Connect(host, port, user, Ssh.KeyAuth(user, k), f, out _);
                return (k, f);
            });
            OnConnected(key, fp);
        }
        catch (Exception ex)
        {
            Log.Write("key setup failed: " + ex);
            ShowServerResult(false, Ssh.Describe(ex), ex is SshAuthenticationException ? "" : ex.Message);
        }
        finally
        {
            ConnectProgress.Visibility = Visibility.Collapsed;
            SetBusy(false);
        }
    }

    // --------------------------------------------------------- discovery ---

    bool discovered;

    async void Discover_Click(object sender, RoutedEventArgs e) => await DiscoverAsync();

    async Task DiscoverAsync()
    {
        discovered = true;
        DiscoverButton.IsEnabled = false;
        DiscoverProgress.Visibility = Visibility.Visible;
        DiscoverText.Text = L.T("wiz.find.searching");
        List<FoundServer> found;
        try
        {
            found = await Discovery.FindAsync(TimeSpan.FromSeconds(6));
        }
        catch (Exception ex)
        {
            Log.Write("discovery failed: " + ex);
            found = [];
        }
        DiscoverProgress.Visibility = Visibility.Collapsed;
        DiscoverButton.IsEnabled = true;
        FoundList.ItemsSource = found;
        FoundList.Visibility = found.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DiscoverText.Text = found.Count switch
        {
            0 => L.T("wiz.find.none"),
            _ => L.T("wiz.find.pick")
        };
    }

    void Found_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (FoundList.SelectedItem is not FoundServer f) return;
        HostBox.Text = f.Host;
        if (UserBox.Text.Trim().Length == 0) UserBox.Focus(); else ConnectButton.Focus();
    }

    // ----------------------------------------------------------- prepare ---

    readonly Dictionary<string, string> DeviceDescriptions = new();

    void SetCheck(TextBlock glyph, TextBlock state, bool? ok, string text)
    {
        glyph.Text = ok switch { true => "", false => "", null => "" };
        glyph.Foreground = ok switch
        {
            true => new SolidColorBrush(Color.FromRgb(16, 137, 62)),
            false => new SolidColorBrush(Color.FromRgb(196, 43, 28)),
            null => (Brush)FindResource("TextFillColorSecondaryBrush")
        };
        state.Text = text;
    }

    async Task CheckServerAsync()
    {
        SetBusy(true);
        PrepareButton.IsEnabled = false;
        PrepareProgress.Visibility = Visibility.Visible;
        string checking = L.T("wiz.prep.checking");
        SetCheck(CupsGlyph, CupsState, null, checking);
        SetCheck(HelperGlyph, HelperState, null, checking);
        SetCheck(UsbipGlyph, UsbipState, null, checking);
        try
        {
            var snapshot = s.Clone();
            var (st, devices, ready, needPw, inf) = await Task.Run(() =>
            {
                using var server = new Server(snapshot);
                var st = server.GetStatus();
                var devices = st.Reachable ? server.GetDevices() : [];
                bool ready = st.Reachable && server.HelperReady();
                bool needPw = st.Reachable && !ready && server.SudoNeedsPassword();
                ServerInfo? inf = st.Reachable ? server.GetInfo() : null;
                return (st, devices, ready, needPw, inf);
            });
            status = st;
            info = inf;
            if (inf != null)
            {
                ServerInfoText.Text = string.Join(" · ", new[] { inf.Model, inf.Os }.Where(x => x.Length > 0));
                if (!modeChosen)
                {
                    // Nothing to print with yet? Then offer the full setup.
                    bool full = !inf.CupsInstalled || inf.Queues.Count == 0;
                    ModeFull.IsChecked = full;
                    ModeExisting.IsChecked = !full;
                    modeChosen = true;
                }
                ModeFull.IsEnabled = inf.HasApt;
            }
            helperReady = ready;
            checkedServer = true;

            if (!st.Reachable)
            {
                SetCheck(CupsGlyph, CupsState, false, st.Error);
                SetCheck(HelperGlyph, HelperState, false, "");
                SetCheck(UsbipGlyph, UsbipState, false, "");
                return;
            }

            // CUPS is stopped on purpose while the printer is lent out.
            bool cupsOk = st.CupsActive || st.Shared;
            bool cupsMissing = info is { CupsInstalled: false };
            SetCheck(CupsGlyph, CupsState, cupsOk ? true : cupsMissing ? null : false,
                     cupsOk ? L.T("wiz.prep.ok") : cupsMissing ? L.T("wiz.prep.missing") : L.T("wiz.prep.notrunning"));
            SetCheck(HelperGlyph, HelperState, ready, ready ? L.T("wiz.prep.ok") : L.T("wiz.prep.missing"));
            SetCheck(UsbipGlyph, UsbipState, st.UsbipInstalled, st.UsbipInstalled ? L.T("wiz.prep.ok") : L.T("wiz.prep.missing"));
            SudoPanel.Visibility = needPw ? Visibility.Visible : Visibility.Collapsed;
            PrepareButton.Content = ready && st.UsbipInstalled ? L.T("wiz.prep.rerun") : L.T("wiz.prep.run");
            PrepareText.Text = ready && st.UsbipInstalled ? L.T("wiz.prep.done") : "";

            FillDevices(devices);
            FillQueues(st.Queues);
        }
        finally
        {
            PrepareButton.IsEnabled = true;
            PrepareProgress.Visibility = Visibility.Collapsed;
            SetBusy(false);
        }
    }

    void FillDevices(List<UsbDevice> devices)
    {
        DeviceBox.Items.Clear();
        DeviceDescriptions.Clear();
        DeviceBox.Items.Add(new Choice("", L.T("wiz.prep.auto")));
        int select = 0;
        foreach (var d in devices.OrderByDescending(d => d.IsPrinter))
        {
            DeviceDescriptions[d.VidPid] = d.Description;
            DeviceBox.Items.Add(new Choice(d.VidPid, d.ToString()));
            if (s.Device.Length > 0 && (d.VidPid == s.Device || d.BusId == s.Device)) select = DeviceBox.Items.Count - 1;
            else if (s.Device.Length == 0 && select == 0 && d.IsPrinter) select = DeviceBox.Items.Count - 1;
        }
        if (s.Device.Length > 0 && select == 0 && s.DeviceName.Length > 0)
        {
            // Saved printer is switched off right now; keep the choice.
            DeviceDescriptions[s.Device] = s.DeviceName;
            DeviceBox.Items.Add(new Choice(s.Device, s.DeviceName + "  (" + s.Device + ")"));
            select = DeviceBox.Items.Count - 1;
        }
        DeviceBox.SelectedIndex = select;
        NoDeviceText.Visibility = devices.Any(d => d.IsPrinter) ? Visibility.Collapsed : Visibility.Visible;
    }

    void FillQueues(List<string> queues)
    {
        QueueBox.Items.Clear();
        int select = 0;
        foreach (string q in queues)
        {
            QueueBox.Items.Add(new Choice(q, q));
            if (q == s.Queue) select = QueueBox.Items.Count - 1;
        }
        if (s.Queue.Length > 0 && !queues.Contains(s.Queue))
        {
            QueueBox.Items.Add(new Choice(s.Queue, s.Queue));
            select = QueueBox.Items.Count - 1;
        }
        if (QueueBox.Items.Count > 0) QueueBox.SelectedIndex = select;
    }

    async void Prepare_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string? pw = SudoPanel.Visibility == Visibility.Visible ? SudoBox.Password : null;
        if (pw is { Length: 0 }) { SudoBox.Focus(); return; }
        SetBusy(true);
        PrepareButton.IsEnabled = false;
        PrepareProgress.Visibility = Visibility.Visible;
        PrepareText.Text = L.T("wiz.prep.running");
        CommandResult r;
        bool full = ModeFull.IsChecked == true;
        PrepareLog.Clear();
        PrepareLog.Visibility = Visibility.Visible;
        try
        {
            var snapshot = s.Clone();
            using var server = new Server(snapshot);
            r = await Task.Run(() => server.Prepare(pw));
            AppendLog(PrepareLog, r.All);
            if (r.Ok && full)
            {
                string device = DeviceBox.SelectedItem is Choice dc ? dc.Value : "";
                var (code, output) = await server.RunJobAsync("setup-printserver " + device,
                    line => Dispatcher.BeginInvoke(() => AppendLog(PrepareLog, line)), CancellationToken.None);
                var q = System.Text.RegularExpressions.Regex.Match(output, @"PRINTSWITCH_QUEUE (\S+)");
                if (q.Success) s.Queue = q.Groups[1].Value;
                if (output.Contains("PRINTSWITCH_ERR nodevice"))
                    output = L.T("err.nodevice") + "\n" + output;
                r = new CommandResult(code, output, "");
            }
        }
        catch (Exception ex)
        {
            r = new CommandResult(-1, "", Ssh.Describe(ex));
        }
        SudoBox.Clear();
        PrepareProgress.Visibility = Visibility.Collapsed;
        PrepareButton.IsEnabled = true;
        SetBusy(false);
        await CheckServerAsync();
        if (!r.Ok)
        {
            string tail = string.Join("\n", Log.StripAnsi(r.All).Split('\n').TakeLast(6));
            PrepareText.Text = L.T("wiz.prep.failed") + "\n" + tail;
        }
    }

    // ------------------------------------------------------- raspberry pi ---

    static readonly string[] DedicatedOnly = ["services", "audio"];
    readonly Dictionary<string, CheckBox> optBoxes = new();

    static void AppendLog(TextBox box, string text)
    {
        if (text.Length == 0) return;
        box.AppendText(Log.StripAnsi(text).TrimEnd() + Environment.NewLine);
        box.ScrollToEnd();
    }

    async Task ShowPiStateAsync()
    {
        if (info == null && connected)
        {
            try
            {
                var snapshot = s.Clone();
                info = await Task.Run(() => { using var server = new Server(snapshot); return server.GetInfo(); });
            }
            catch (Exception ex) { Log.Write("server info: " + ex.Message); }
        }
        bool pi = info?.IsRaspberryPi == true;
        PiModelText.Text = info?.Model ?? "";
        PiIntro.Text = pi ? L.T("wiz.pi.p") : L.T("wiz.pi.notpi");
        PiPanel.Visibility = pi ? Visibility.Visible : Visibility.Collapsed;
        if (!pi || info == null) return;

        OptList.Children.Clear();
        optBoxes.Clear();
        foreach (var o in info.Optimizations)
        {
            var title = new TextBlock { Text = L.T("opt." + o.Id + ".t"), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var desc = new TextBlock { Text = L.T("opt." + o.Id + ".d"), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
            desc.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(desc);
            if (o.Applied || !o.Available)
            {
                var note = new TextBlock
                {
                    Text = o.Applied ? L.T("wiz.pi.applied") : L.T("wiz.pi.unavailable"),
                    FontSize = 12, Margin = new Thickness(0, 2, 0, 0),
                    Foreground = new SolidColorBrush(o.Applied ? Color.FromRgb(16, 137, 62) : Color.FromRgb(138, 138, 138))
                };
                panel.Children.Add(note);
            }
            var cb = new CheckBox { Content = panel, Margin = new Thickness(0, 6, 0, 6), Tag = o };
            optBoxes[o.Id] = cb;
            OptList.Children.Add(cb);
        }
        UpdateOptBoxes();
    }

    void UpdateOptBoxes()
    {
        bool dedicated = DedicatedBox.IsChecked == true;
        foreach (var (id, cb) in optBoxes)
        {
            var o = (Optimization)cb.Tag;
            bool needsDedicated = DedicatedOnly.Contains(id);
            cb.IsEnabled = o.Available && !o.Applied && (!needsDedicated || dedicated);
            // Proposed by default: everything safe; the rest once the Pi is dedicated.
            cb.IsChecked = o.Applied || (cb.IsEnabled && (!needsDedicated || dedicated));
        }
    }

    async void ApplyOpts_Click(object sender, RoutedEventArgs e)
    {
        var ids = optBoxes.Where(kv => kv.Value.IsEnabled && kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
        if (ids.Count == 0) { OptText.Text = L.T("wiz.pi.nothing"); return; }
        if (!helperReady) { OptText.Text = L.T("err.notprepared"); return; }
        SetBusy(true);
        ApplyOptsButton.IsEnabled = false;
        OptProgress.Visibility = Visibility.Visible;
        OptLog.Clear();
        OptLog.Visibility = Visibility.Visible;
        OptText.Text = L.T("wiz.pi.applying");
        try
        {
            var snapshot = s.Clone();
            using var server = new Server(snapshot);
            var (code, _) = await server.RunJobAsync("optimize " + string.Join(" ", ids),
                line => Dispatcher.BeginInvoke(() => AppendLog(OptLog, line)), CancellationToken.None);
            OptText.Text = code == 0 ? L.T("wiz.pi.done") : L.T("wiz.pi.failed");
            RebootButton.Visibility = code == 0 && ids.Any(i => i is "audio" or "wifi-powersave" or "services")
                ? Visibility.Visible : Visibility.Collapsed;
            info = await Task.Run(() => server.GetInfo());
        }
        catch (Exception ex)
        {
            OptText.Text = L.T("wiz.pi.failed") + " " + Ssh.Describe(ex);
        }
        finally
        {
            OptProgress.Visibility = Visibility.Collapsed;
            ApplyOptsButton.IsEnabled = true;
            SetBusy(false);
        }
        await ShowPiStateAsync();
    }

    async void Reboot_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(L.T("wiz.pi.reboot"), L.T("wiz.pi.rebootconfirm"), L.T("wiz.pi.reboot"))) return;
        var snapshot = s.Clone();
        var r = await Task.Run(() => { using var server = new Server(snapshot); return server.Reboot(); });
        OptText.Text = r.Ok ? L.T("wiz.pi.rebooting") : r.All;
        RebootButton.Visibility = Visibility.Collapsed;
    }

    // --------------------------------------------------------------- pc ---

    void ShowUsbipState()
    {
        string? exe = Usbip.FindExe();
        string? ver = exe == null ? null : Usbip.Version(exe);
        InstallButton.Content = L.F("wiz.pc.install", Usbip.RecommendedVersion);
        if (exe == null)
        {
            UsbipPcGlyph.Text = "";
            UsbipPcGlyph.Foreground = new SolidColorBrush(Color.FromRgb(202, 80, 16));
            UsbipPcText.Text = L.T("wiz.pc.missing");
            UsbipPcSub.Text = "";
            InstallButton.Visibility = Visibility.Visible;
        }
        else if (Usbip.IsKnownBad(ver))
        {
            UsbipPcGlyph.Text = "";
            UsbipPcGlyph.Foreground = new SolidColorBrush(Color.FromRgb(196, 43, 28));
            UsbipPcText.Text = L.F("wiz.pc.installed", ver ?? "?");
            UsbipPcSub.Text = L.F("wiz.pc.bad", ver ?? "?", Usbip.RecommendedVersion);
            InstallButton.Visibility = Visibility.Visible;
        }
        else
        {
            UsbipPcGlyph.Text = "";
            UsbipPcGlyph.Foreground = new SolidColorBrush(Color.FromRgb(16, 137, 62));
            UsbipPcText.Text = L.F("wiz.pc.installed", ver ?? "?");
            UsbipPcSub.Text = exe;
            InstallButton.Visibility = Visibility.Collapsed;
        }
        UsbipPcSub.Visibility = UsbipPcSub.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        InstallButton.IsEnabled = false;
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
            SetBusy(false);
            ShowUsbipState();
        }
    }

    // ------------------------------------------------------------- done ---

    async void AddPrinter_Click(object sender, RoutedEventArgs e)
    {
        string queue = QueueBox.SelectedItem is Choice q ? q.Value : status?.Queues.FirstOrDefault() ?? "";
        if (queue.Length == 0) { Dialogs.Error(L.T("action.addprinter"), L.T("printer.noqueue")); return; }
        AddPrinterButton.IsEnabled = false;
        try
        {
            await TrayController.AddNetworkPrinterAsync($"http://{s.Host}:631/printers/{Uri.EscapeDataString(queue)}", null);
        }
        finally
        {
            AddPrinterButton.IsEnabled = true;
        }
    }
}
