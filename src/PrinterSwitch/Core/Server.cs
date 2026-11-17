// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace PrinterSwitch.Core;

public sealed class ServerStatus
{
    public bool Reachable { get; init; }
    public string Error { get; init; } = "";
    public bool CupsActive { get; init; }
    public bool Shared { get; init; }
    public string BusId { get; init; } = "";
    /// <summary>Version of /usr/local/sbin/printswitch; 0 = not installed.</summary>
    public int ScriptVersion { get; init; }
    public bool UsbipInstalled { get; init; }
    public List<string> Queues { get; init; } = [];
}

public sealed record UsbDevice(string BusId, string VidPid, bool IsPrinter, string Description)
{
    public override string ToString()
        => (Description.Length > 0 ? Description : VidPid) + "  (" + VidPid + ", " + BusId + ")";
}

public sealed record Optimization(string Id, bool Applied, bool Available);

/// <summary>Facts about the server for the setup wizard.</summary>
public sealed class ServerInfo
{
    public string Model { get; init; } = "";
    public string Os { get; init; } = "";
    public bool IsRaspberryPi { get; init; }
    public bool HasApt { get; init; }
    public bool CupsInstalled { get; init; }
    public bool JobRunning { get; init; }
    public List<string> Queues { get; init; } = [];
    public List<Optimization> Optimizations { get; init; } = [];
}

public sealed record ShareResult(bool Ok, string BusId, string VidPid, string Output);

/// <summary>
/// Everything that happens on the print server. Keeps a single SSH connection open
/// and reuses it for polling and for the USB/IP tunnel, so the (often tiny) server
/// is not paying for a new SSH handshake every time.
/// </summary>
public sealed partial class Server : IDisposable
{
    /// <summary>Oldest helper version the app works with. The bundled script may be newer
    /// (v3 only tightens input checks); raising this makes users re-run "Prepare" with sudo.</summary>
    public const int ScriptVersion = 2;
    const string ScriptPath = "/usr/local/sbin/printswitch";
    const string StateFile = "/run/printserver-usbip.busid";

    readonly object gate = new();
    Settings settings;
    SshClient? client;

    public Server(Settings settings) => this.settings = settings;

    public void Reconfigure(Settings s)
    {
        lock (gate)
        {
            settings = s;
            DropConnection();
        }
    }

    /// <summary>The live connection, opened (again) if needed.</summary>
    public SshClient Client
    {
        get
        {
            lock (gate)
            {
                if (client is { IsConnected: true }) return client;
                DropConnection();
                client = Ssh.Connect(settings);
                return client;
            }
        }
    }

    void DropConnection()
    {
        try { client?.Dispose(); } catch { }
        client = null;
    }

    public void Dispose()
    {
        lock (gate) DropConnection();
    }

    CommandResult Run(string command, int timeoutSeconds, string? stdin = null)
    {
        try
        {
            return Ssh.Run(Client, command, TimeSpan.FromSeconds(timeoutSeconds), stdin);
        }
        catch (Exception e) when (e is Renci.SshNet.Common.SshConnectionException or ObjectDisposedException)
        {
            // Connection went stale (sleep, Wi-Fi roam...). One retry on a fresh one.
            lock (gate) DropConnection();
            return Ssh.Run(Client, command, TimeSpan.FromSeconds(timeoutSeconds), stdin);
        }
    }

    // ------------------------------------------------------------- status ---

    // Works with or without the helper installed, so the app shows the truth
    // even on a server prepared by the original scripts.
    const string StatusCommand =
        "if [ -x " + ScriptPath + " ]; then " + ScriptPath + " status; else " +
        "echo version=0; echo cups=$(systemctl is-active cups.service 2>/dev/null); " +
        "if [ -f " + StateFile + " ]; then echo shared=yes; echo busid=$(cat " + StateFile + "); else echo shared=no; fi; " +
        "if command -v usbip >/dev/null || [ -x /usr/sbin/usbip ]; then echo usbip=yes; else echo usbip=no; fi; " +
        """lpstat -v 2>/dev/null | sed -n 's/^device for \([^:]*\): *\(.*\)$/queue=\1\t\2/p'; fi""";

    public ServerStatus GetStatus()
    {
        CommandResult r;
        try
        {
            r = Run(StatusCommand, 25);
        }
        catch (Exception e)
        {
            return new ServerStatus { Reachable = false, Error = Ssh.Describe(e) };
        }
        return ParseStatus(r.Output, r.Ok ? "" : r.All);
    }

    public static ServerStatus ParseStatus(string text, string error = "")
    {
        var kv = new Dictionary<string, string>();
        var queues = new List<(string Name, string Uri)>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int i = line.IndexOf('=');
            if (i <= 0) continue;
            string k = line[..i], v = line[(i + 1)..];
            if (k == "queue")
            {
                var parts = v.Split('\t');
                if (parts[0].Length > 0) queues.Add((parts[0], parts.Length > 1 ? parts[1] : ""));
            }
            else kv[k] = v;
        }
        // Queues printing straight to a USB device first; implicit network queues last.
        queues.Sort((a, b) => Rank(a.Uri).CompareTo(Rank(b.Uri)));
        static int Rank(string uri) => uri.StartsWith("usb:") ? 0 : uri.Length == 0 ? 1 : 2;
        if (!kv.ContainsKey("shared"))
            return new ServerStatus { Reachable = false, Error = error.Length > 0 ? error : L.T("err.badreply") };

        return new ServerStatus
        {
            Reachable = true,
            CupsActive = kv.GetValueOrDefault("cups") == "active",
            Shared = kv.GetValueOrDefault("shared") == "yes",
            BusId = kv.GetValueOrDefault("busid", ""),
            ScriptVersion = int.TryParse(kv.GetValueOrDefault("version"), out int v2) ? v2 : 0,
            UsbipInstalled = kv.GetValueOrDefault("usbip") == "yes",
            Queues = queues.Select(q => q.Name).Distinct().ToList()
        };
    }

    // ------------------------------------------------------------ devices ---

    public static string ScriptText()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("printswitch")!;
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd().Replace("\r\n", "\n");
    }

    public List<UsbDevice> GetDevices()
    {
        // Runs the bundled script straight from stdin, nothing has to be installed.
        var r = Run("bash -s devices", 20, ScriptText());
        var list = new List<UsbDevice>();
        foreach (string line in r.Output.Split('\n'))
        {
            var f = line.TrimEnd('\r').Split('\t');
            if (f.Length >= 5 && f[0] == "device")
                list.Add(new UsbDevice(f[1], f[2], f[3] == "1", f[4]));
        }
        return list;
    }

    public ServerInfo GetInfo()
    {
        var r = Run("bash -s info", 30, ScriptText());
        var kv = new Dictionary<string, string>();
        var queues = new List<string>();
        var opts = new List<Optimization>();
        foreach (string raw in r.Output.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("opt\t"))
            {
                var f = line.Split('\t');
                if (f.Length >= 4) opts.Add(new Optimization(f[1], f[2] == "1", f[3] == "1"));
                continue;
            }
            int i = line.IndexOf('=');
            if (i <= 0) continue;
            string k = line[..i], v = line[(i + 1)..];
            if (k == "queue") queues.Add(v.Split('\t')[0]);
            else kv[k] = v.Trim();
        }
        return new ServerInfo
        {
            Model = kv.GetValueOrDefault("model", ""),
            Os = kv.GetValueOrDefault("os", ""),
            IsRaspberryPi = kv.GetValueOrDefault("pi") == "yes",
            HasApt = kv.GetValueOrDefault("apt") == "yes",
            CupsInstalled = kv.GetValueOrDefault("cups_installed") == "yes",
            JobRunning = kv.GetValueOrDefault("job") == "active",
            Queues = queues,
            Optimizations = opts
        };
    }

    // --------------------------------------------------------------- jobs ---

    /// <summary>
    /// Runs a long helper command (apt, CUPS setup, optimisations) as a detached
    /// job on the server and follows its log. Survives dropped connections: the
    /// job keeps running on the server and polling simply resumes.
    /// Returns the job's exit code and its whole output.
    /// </summary>
    public async Task<(int Code, string Output)> RunJobAsync(string args, Action<string> onLine, CancellationToken ct)
    {
        var start = await Task.Run(() => Run("sudo -n " + ScriptPath + " job " + args, 60), ct);
        if (!start.Ok) return (start.Code, start.All);

        var all = new System.Text.StringBuilder();
        int seen = 0, failures = 0;
        var until = DateTime.UtcNow.AddMinutes(60);
        while (DateTime.UtcNow < until)
        {
            await Task.Delay(2000, ct);
            CommandResult r;
            try
            {
                r = await Task.Run(() => Run($"tail -n +{seen + 1} /var/log/printswitch-job.log", 20), ct);
                failures = 0;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Wi-Fi hiccup or the server is busy; the job itself goes on.
                if (++failures > 30) return (-1, all + "\n" + Ssh.Describe(e));
                continue;
            }
            foreach (string raw in r.Output.Split('\n'))
            {
                if (raw.Length == 0) continue;
                string line = Log.StripAnsi(raw.TrimEnd('\r'));
                seen++;
                all.AppendLine(line);
                onLine(line);
                var m = System.Text.RegularExpressions.Regex.Match(line, @"^PRINTSWITCH_JOB_DONE rc=(\d+)");
                if (m.Success)
                {
                    Log.Write("job " + args + ": exit " + m.Groups[1].Value + "\n" + all);
                    return (int.Parse(m.Groups[1].Value), all.ToString());
                }
            }
        }
        return (-1, all + "\n(timed out)");
    }

    public CommandResult Reboot()
    {
        var r = Run("sudo -n " + ScriptPath + " reboot", 30);
        lock (gate) DropConnection();
        return r;
    }

    // ------------------------------------------------------------ prepare ---

    /// <summary>Can we run the helper through sudo without a password?</summary>
    public bool HelperReady()
    {
        var r = Run("sudo -n " + ScriptPath + " version", 15);
        return r.Ok && int.TryParse(r.Output.Trim(), out int v) && v >= ScriptVersion;
    }

    public bool SudoNeedsPassword() => !Run("sudo -n true", 15).Ok;

    /// <summary>
    /// Installs the helper to /usr/local/sbin, a sudoers rule allowing exactly that
    /// command without a password, and the usbip package. May take minutes (apt).
    /// </summary>
    public CommandResult Prepare(string? sudoPassword)
    {
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ScriptText()));
        string sudo = sudoPassword == null ? "sudo -n" : "sudo -S -p ''";
        string inner =
            "install -m 0755 -o root -g root \"$1\" " + ScriptPath + " && " +
            "printf '%s ALL=(root) NOPASSWD: " + ScriptPath + "\\n' \"$2\" > /etc/sudoers.d/printswitch.new && " +
            "chmod 0440 /etc/sudoers.d/printswitch.new && " +
            "visudo -cf /etc/sudoers.d/printswitch.new >/dev/null && " +
            "mv /etc/sudoers.d/printswitch.new /etc/sudoers.d/printswitch && " +
            ScriptPath + " install";
        string cmd =
            "t=$(mktemp) && echo '" + b64 + "' | base64 -d > \"$t\" && " +
            sudo + " sh -c '" + inner.Replace("'", "'\\''") + "' sh \"$t\" \"$(id -un)\"; " +
            "rc=$?; rm -f \"$t\"; exit $rc";
        var r = Run(cmd, 600, sudoPassword == null ? null : sudoPassword + "\n");
        Log.Write("prepare server: exit " + r.Code + "\n" + r.All);
        return r;
    }

    // -------------------------------------------------------------- share ---

    [GeneratedRegex(@"^[0-9A-Za-z:.\-]*$")]
    private static partial Regex SafeSpec();

    [GeneratedRegex(@"busid=(\S+)\s+vidpid=(\S*)")]
    private static partial Regex ShareReply();

    public ShareResult Share(string device)
    {
        if (!SafeSpec().IsMatch(device)) device = "";
        var r = Run("sudo -n " + ScriptPath + " share " + device, 300);
        Log.Write("share: exit " + r.Code + "\n" + r.All);
        var m = ShareReply().Match(r.Output);
        if (!r.Ok || !m.Success) return new ShareResult(false, "", "", r.All);
        return new ShareResult(true, m.Groups[1].Value, m.Groups[2].Value, r.All);
    }

    public CommandResult Unshare()
    {
        var r = Run("sudo -n " + ScriptPath + " unshare", 120);
        Log.Write("unshare: exit " + r.Code + "\n" + r.All);
        return r;
    }

    // -------------------------------------------------------- authorizing ---

    /// <summary>Logs in with a password once and adds our public key to authorized_keys.</summary>
    public static void AuthorizeKey(string host, int port, string user, string password,
                                    string publicKeyLine, string trustedFingerprint, out string fingerprint)
    {
        using var c = Ssh.Connect(host, port, user, new PasswordAuthenticationMethod(user, password),
                                  trustedFingerprint, out fingerprint);
        var r = Ssh.Run(c,
            "read -r k; umask 077; mkdir -p ~/.ssh && touch ~/.ssh/authorized_keys && " +
            "(grep -qxF \"$k\" ~/.ssh/authorized_keys || printf '%s\\n' \"$k\" >> ~/.ssh/authorized_keys)",
            TimeSpan.FromSeconds(20), publicKeyLine + "\n");
        if (!r.Ok) throw new InvalidOperationException(r.All);
    }
}
