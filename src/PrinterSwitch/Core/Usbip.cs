// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PrinterSwitch.Core;

/// <summary>The usbip-win2 client (https://github.com/vadimgrn/usbip-win2).</summary>
public static partial class Usbip
{
    // Pinned on purpose: 0.9.7.8 can corrupt memory and blue-screen (the author says
    // so in its release notes) and 0.9.8.0 fixes exactly that. 0.9.8.1 is a large
    // refactoring that the author himself warns may contain regressions.
    public const string RecommendedVersion = "0.9.8.0";
    public const string DownloadUrl =
        "https://github.com/vadimgrn/usbip-win2/releases/download/v.0.9.8.0/USBip-0.9.8.0-x64.exe";
    const string DownloadSha256 = "81f426741f7ee2ed991febe24a22daca8400b6ae2f171054e3fb404897e15d39";
    public const string ReleasesUrl = "https://github.com/vadimgrn/usbip-win2/releases";

    static readonly string[] KnownBad = ["0.9.7.8"];

    public static string? FindExe()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (string p in new[] { Path.Combine(pf, "USBip", "usbip.exe"), Path.Combine(pf, "usbip-win2", "usbip.exe") })
            if (File.Exists(p)) return p;
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                string p = Path.Combine(dir.Trim(), "usbip.exe");
                if (dir.Length > 0 && File.Exists(p)) return p;
            }
            catch { }
        }
        return null;
    }

    public static string? Version(string exe)
    {
        try { return FileVersionInfo.GetVersionInfo(exe).ProductVersion?.Trim(); }
        catch { return null; }
    }

    public static bool IsKnownBad(string? version) => version != null && KnownBad.Contains(version);

    static CommandResult Run(string exe, string args, int timeoutSeconds = 30)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var p = Process.Start(psi)!;
        var so = p.StandardOutput.ReadToEndAsync();
        var se = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutSeconds * 1000))
        {
            try { p.Kill(true); } catch { }
            return new CommandResult(-1, so.IsCompleted ? so.Result : "", "timed out");
        }
        return new CommandResult(p.ExitCode, so.Result, se.Result);
    }

    // ------------------------------------------------------------- ports ---

    public sealed record ImportedPort(int Number, string Text);

    [GeneratedRegex(@"^\s*Port\s+(\d+)\s*:", RegexOptions.Multiline)]
    private static partial Regex PortHeader();

    public static List<ImportedPort> Ports(string exe)
    {
        var r = Run(exe, "port", 15);
        return ParsePorts(r.Output + "\n" + r.Error);
    }

    public static List<ImportedPort> ParsePorts(string text)
    {
        var list = new List<ImportedPort>();
        var matches = PortHeader().Matches(text);
        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            string block = text[start..end];
            if (block.Contains("<Port Available>", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new ImportedPort(int.Parse(matches[i].Groups[1].Value), block));
        }
        return list;
    }

    /// <summary>Ports that belong to us: imported through our local tunnel, or our printer's IDs.</summary>
    public static List<ImportedPort> OurPorts(string exe, int localPort, string vidPid)
    {
        return Ports(exe).Where(p =>
            p.Text.Contains("127.0.0.1:" + localPort) ||
            p.Text.Contains("localhost:" + localPort) ||
            (vidPid.Length > 0 && p.Text.Contains(vidPid, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    // ------------------------------------------------------ attach/detach ---

    public static CommandResult Attach(string exe, int localPort, string busId)
    {
        var r = Run(exe, $"--tcp-port {localPort} attach --remote 127.0.0.1 --bus-id {busId}", 60);
        if (!r.Ok)
        {
            // Very old builds used --busid.
            var r2 = Run(exe, $"--tcp-port {localPort} attach --remote=127.0.0.1 --busid={busId}", 60);
            if (r2.Ok) return r2;
        }
        return r;
    }

    public static void Detach(string exe, IEnumerable<ImportedPort> ports)
    {
        // usbip-win2 keeps retrying attachments in the background; stop that first,
        // otherwise the device comes straight back.
        Run(exe, "attach --stop-all", 15);
        foreach (var p in ports)
        {
            var r = Run(exe, $"detach --port {p.Number}", 20);
            Log.Write($"usbip detach port {p.Number}: exit {r.Code} {r.All}");
        }
    }

    // ----------------------------------------------------------- install ---

    /// <summary>Downloads the pinned installer, checks its hash and runs it. Returns the exit code.</summary>
    public static async Task<int> DownloadAndInstall(IProgress<double> progress, CancellationToken ct)
    {
        string dir = Path.Combine(Path.GetTempPath(), "PrinterSwitch");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, Path.GetFileName(new Uri(DownloadUrl).LocalPath));

        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PrinterSwitch/1.0");
            using var resp = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(file);
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        string hash;
        await using (var fs = File.OpenRead(file))
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(fs, ct));
        if (hash != DownloadSha256)
        {
            File.Delete(file);
            throw new InvalidOperationException(L.T("usbip.badhash"));
        }

        // The installer elevates itself (UAC prompt).
        using var p = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true })!;
        await p.WaitForExitAsync(ct);
        return p.ExitCode;
    }
}
