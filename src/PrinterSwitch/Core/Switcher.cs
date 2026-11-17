// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using Renci.SshNet;

namespace PrinterSwitch.Core;

public enum PrinterState
{
    Unknown,       // not measured yet
    NotConfigured, // setup wizard not finished
    Unreachable,   // server does not answer
    Network,       // CUPS has it, everybody can print
    CupsDown,      // nobody lent it, but CUPS is not running
    UsbHere,       // attached to this PC
    UsbElsewhere   // lent to another computer
}

public sealed record OpResult(bool Ok, string Message);

/// <summary>
/// Knows what state the printer is in and moves it between "network" and
/// "USB on this PC". Holds the SSH tunnel that USB/IP runs through.
/// </summary>
public sealed class Switcher : IDisposable
{
    readonly SemaphoreSlim op = new(1, 1);
    readonly System.Threading.Timer watchdog;
    Settings settings;
    Server server;

    ForwardedPortLocal? forward;
    SshClient? forwardClient;
    string lastVidPid = "";

    public PrinterState State { get; private set; } = PrinterState.Unknown;
    public bool Busy { get; private set; }
    public string BusyText { get; private set; } = "";
    public string Detail { get; private set; } = "";
    public ServerStatus? LastStatus { get; private set; }

    /// <summary>Raised on any change; may come from a worker thread.</summary>
    public event Action? Changed;

    public Switcher(Settings s)
    {
        settings = s;
        server = new Server(s);
        lastVidPid = s.LastVidPid;
        if (!s.HasServer) State = PrinterState.NotConfigured;
        watchdog = new System.Threading.Timer(_ => KeepTunnel(), null, 10_000, 10_000);
    }

    public Settings Settings => settings;

    public void Reconfigure(Settings s)
    {
        settings = s;
        server.Reconfigure(s);
        State = s.HasServer ? PrinterState.Unknown : PrinterState.NotConfigured;
        Raise();
    }

    void Raise() => Changed?.Invoke();

    void SetBusy(string text)
    {
        Busy = text.Length > 0;
        BusyText = text;
        Raise();
    }

    // -------------------------------------------------------------- refresh ---

    public async Task RefreshAsync()
    {
        if (!settings.HasServer) { State = PrinterState.NotConfigured; Raise(); return; }
        if (!await op.WaitAsync(0)) return; // an operation is running; it refreshes at the end
        try
        {
            await Task.Run(Measure);
        }
        finally
        {
            op.Release();
            Raise();
        }
    }

    void Measure()
    {
        var st = server.GetStatus();
        LastStatus = st;
        if (!st.Reachable)
        {
            State = PrinterState.Unreachable;
            Detail = st.Error;
            Log.Write("status: server unreachable (" + st.Error + ")");
            return;
        }
        Detail = "";

        string? exe = Usbip.FindExe();
        var ours = exe == null ? [] : Usbip.OurPorts(exe, settings.LocalPort, lastVidPid);

        if (st.Shared)
        {
            if (ours.Count > 0)
            {
                State = PrinterState.UsbHere;
                // App was restarted while the printer was attached: bring the tunnel
                // back so usbip-win2 can reconnect on its own.
                EnsureTunnel();
            }
            else
            {
                State = PrinterState.UsbElsewhere;
            }
        }
        else
        {
            if (ours.Count > 0 && exe != null)
            {
                // Someone gave the printer back from elsewhere; don't leave a dead device here.
                Log.Write("printer was returned elsewhere, cleaning up the local attachment");
                Usbip.Detach(exe, ours);
            }
            CloseTunnel();
            State = st.CupsActive ? PrinterState.Network : PrinterState.CupsDown;
        }
    }

    // --------------------------------------------------------------- tunnel ---

    void EnsureTunnel()
    {
        lock (this)
        {
            var client = server.Client;
            if (forward != null && forward.IsStarted && ReferenceEquals(client, forwardClient)) return;
            CloseTunnelLocked();
            forward = new ForwardedPortLocal("127.0.0.1", (uint)settings.LocalPort, "127.0.0.1", 3240);
            forward.Exception += (_, e) => Log.Write("tunnel: " + e.Exception.Message);
            client.AddForwardedPort(forward);
            forward.Start();
            forwardClient = client;
            Log.Write($"tunnel open 127.0.0.1:{settings.LocalPort} -> server:3240");
        }
    }

    void CloseTunnel()
    {
        lock (this) CloseTunnelLocked();
    }

    void CloseTunnelLocked()
    {
        if (forward == null) return;
        try { forward.Stop(); } catch { }
        try { forwardClient?.RemoveForwardedPort(forward); } catch { }
        try { forward.Dispose(); } catch { }
        forward = null;
        forwardClient = null;
        Log.Write("tunnel closed");
    }

    // Reopens the tunnel after sleep or a network hiccup while the printer is attached here.
    void KeepTunnel()
    {
        if (State != PrinterState.UsbHere || Busy) return;
        try { EnsureTunnel(); }
        catch (Exception e) { Log.Write("tunnel watchdog: " + e.Message); }
    }

    // ---------------------------------------------------------- operations ---

    public async Task<OpResult> ConnectHereAsync()
    {
        await op.WaitAsync();
        try
        {
            SetBusy(L.T("busy.connecting"));
            var r = await Task.Run(ConnectHere);
            Log.Write(r.Ok ? "connected: " + r.Message : "connect failed: " + r.Message);
            return r;
        }
        finally
        {
            try { await Task.Run(Measure); } catch (Exception e) { Log.Write(e.ToString()); }
            SetBusy("");
            op.Release();
        }
    }

    OpResult ConnectHere()
    {
        Log.Write("=== lending the printer to this PC ===");
        string? exe = Usbip.FindExe();
        if (exe == null) return new OpResult(false, L.T("err.nousbip"));

        try
        {
            if (!server.HelperReady()) return new OpResult(false, L.T("err.notprepared"));
        }
        catch (Exception e)
        {
            return new OpResult(false, Ssh.Describe(e));
        }

        if (forward == null && WindowsSystem.LocalPortInUse(settings.LocalPort))
            return new OpResult(false, L.F("err.portbusy", settings.LocalPort));

        var share = server.Share(settings.Device);
        if (!share.Ok)
        {
            if (share.Output.Contains("PRINTSWITCH_ERR nodevice")) return new OpResult(false, L.T("err.nodevice"));
            if (share.Output.Contains("PRINTSWITCH_ERR nomodule")) return new OpResult(false, L.T("err.nomodule"));
            return new OpResult(false, L.T("err.share") + "\n\n" + LastLines(share.Output));
        }
        lastVidPid = share.VidPid;
        if (settings.LastVidPid != share.VidPid)
        {
            settings.LastVidPid = share.VidPid;
            try { settings.Save(); } catch { }
        }

        try
        {
            EnsureTunnel();
            var a = Usbip.Attach(exe, settings.LocalPort, share.BusId);
            Log.Write("usbip attach: exit " + a.Code + "\n" + a.All);
            if (!a.Ok) throw new InvalidOperationException(L.T("err.attach") + "\n\n" + LastLines(a.All));

            string? name = WindowsSystem.WaitForUsbDevice(share.VidPid, TimeSpan.FromSeconds(45));
            return name != null
                ? new OpResult(true, L.F("ok.attached", name))
                : new OpResult(true, L.T("ok.attachednodevice"));
        }
        catch (Exception e)
        {
            // Never leave the printer taken away from everybody.
            Log.Write("rolling back: " + e.Message);
            try { Usbip.Detach(exe, Usbip.OurPorts(exe, settings.LocalPort, share.VidPid)); } catch { }
            CloseTunnel();
            try { server.Unshare(); } catch (Exception e2) { Log.Write("rollback unshare: " + e2.Message); }
            return new OpResult(false, e is InvalidOperationException ? e.Message : Ssh.Describe(e));
        }
    }

    public async Task<OpResult> ReturnToNetworkAsync()
    {
        await op.WaitAsync();
        try
        {
            SetBusy(L.T("busy.returning"));
            var r = await Task.Run(ReturnToNetwork);
            Log.Write(r.Ok ? "returned to network" : "return failed: " + r.Message);
            return r;
        }
        finally
        {
            try { await Task.Run(Measure); } catch (Exception e) { Log.Write(e.ToString()); }
            SetBusy("");
            op.Release();
        }
    }

    /// <summary>Synchronous variant for shutdown.</summary>
    public OpResult ReturnToNetworkBlocking() => ReturnToNetwork();

    OpResult ReturnToNetwork()
    {
        Log.Write("=== giving the printer back to the network ===");
        string? exe = Usbip.FindExe();
        if (exe != null)
        {
            var ours = Usbip.OurPorts(exe, settings.LocalPort, lastVidPid);
            if (ours.Count > 0) Usbip.Detach(exe, ours);
        }
        CloseTunnel();
        try
        {
            var r = server.Unshare();
            return r.Ok ? new OpResult(true, L.T("ok.returned"))
                        : new OpResult(false, L.T("err.unshare") + "\n\n" + LastLines(r.All));
        }
        catch (Exception e)
        {
            return new OpResult(false, Ssh.Describe(e));
        }
    }

    static string LastLines(string text, int n = 6)
    {
        var lines = Log.StripAnsi(text).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("\n", lines.Skip(Math.Max(0, lines.Length - n)));
    }

    public string QueueUrl()
    {
        string q = settings.Queue;
        if (q.Length == 0) q = LastStatus?.Queues.FirstOrDefault() ?? "";
        return $"http://{settings.Host}:631/printers/{Uri.EscapeDataString(q)}";
    }

    public void Dispose()
    {
        watchdog.Dispose();
        CloseTunnel();
        server.Dispose();
    }
}
