// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using Windows.Devices.Enumeration;

namespace PrinterSwitch.Core;

public sealed record FoundServer(string Host, string Address, string Printers)
{
    public override string ToString() => Host;
}

/// <summary>
/// Finds print servers on the local network through mDNS / DNS-SD. CUPS announces
/// every shared queue as _ipp._tcp, so a host that announces one is a good candidate.
/// Uses the DNS-SD support built into Windows; nothing extra is installed.
/// </summary>
public static class Discovery
{
    // Protocol id of DNS-SD association endpoints in Windows.
    const string DnssdProtocol = "{4526e8c1-8aac-4153-9b16-55e86ada0e54}";

    static readonly string[] Properties =
    [
        "System.Devices.Dnssd.HostName",
        "System.Devices.Dnssd.InstanceName",
        "System.Devices.Dnssd.ServiceName",
        "System.Devices.IpAddress",
    ];

    public static async Task<List<FoundServer>> FindAsync(TimeSpan timeout)
    {
        var found = new Dictionary<string, (string Address, SortedSet<string> Printers)>(StringComparer.OrdinalIgnoreCase);
        var watchers = new List<DeviceWatcher>();
        int pending = 0;
        var allDone = new TaskCompletionSource();

        void Add(DeviceInformation info)
        {
            string host = (info.Properties.GetValueOrDefault("System.Devices.Dnssd.HostName") as string ?? "").TrimEnd('.');
            if (host.Length == 0) return;
            string address = info.Properties.GetValueOrDefault("System.Devices.IpAddress") is string[] ips
                ? ips.FirstOrDefault(ip => !ip.Contains(':')) ?? ips.FirstOrDefault() ?? ""
                : "";
            string instance = info.Properties.GetValueOrDefault("System.Devices.Dnssd.InstanceName") as string ?? info.Name;
            // "Brother HL-L2402D @ RPI-PRINTER" -> "Brother HL-L2402D"
            int at = instance.LastIndexOf(" @ ", StringComparison.Ordinal);
            if (at > 0) instance = instance[..at];
            lock (found)
            {
                if (!found.TryGetValue(host, out var entry))
                    found[host] = entry = (address, new SortedSet<string>());
                else if (entry.Address.Length == 0 && address.Length > 0)
                    found[host] = entry = (address, entry.Printers);
                if (instance.Length > 0) entry.Printers.Add(instance);
            }
        }

        // A DeviceWatcher only raises Added when Updated and Removed have handlers
        // too. FindAllAsync is not an option: it waits for the whole enumeration,
        // which takes tens of seconds for DNS-SD.
        foreach (string service in new[] { "_ipp._tcp", "_ipps._tcp", "_printer._tcp" })
        {
            var w = DeviceInformation.CreateWatcher(
                $"System.Devices.AepService.ProtocolId:=\"{DnssdProtocol}\" AND " +
                $"System.Devices.Dnssd.Domain:=\"local\" AND System.Devices.Dnssd.ServiceName:=\"{service}\"",
                Properties, DeviceInformationKind.AssociationEndpointService);
            w.Added += (_, info) => { try { Add(info); } catch (Exception e) { Log.Write("discovery: " + e.Message); } };
            w.Updated += (_, _) => { };
            w.Removed += (_, _) => { };
            w.EnumerationCompleted += (_, _) => { if (Interlocked.Decrement(ref pending) == 0) allDone.TrySetResult(); };
            w.Stopped += (_, _) => { };
            watchers.Add(w);
        }
        pending = watchers.Count;
        foreach (var w in watchers) w.Start();

        await Task.WhenAny(allDone.Task, Task.Delay(timeout));
        foreach (var w in watchers)
        {
            try { if (w.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) w.Stop(); }
            catch { }
        }

        lock (found)
        {
            return found
                .Select(f => new FoundServer(f.Key, f.Value.Address, string.Join(", ", f.Value.Printers)))
                .OrderBy(f => f.Host)
                .ToList();
        }
    }
}
