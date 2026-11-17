// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace PrinterSwitch.Core;

public sealed record UpdateInfo(Version Version, string PageUrl, string InstallerUrl, string InstallerName, long Size, string Sha256);

/// <summary>
/// Updates from GitHub Releases: asks the API for the latest release, downloads its
/// installer, checks it against the SHA-256 digest GitHub publishes for every release
/// asset and runs it silently. Nothing about the user is sent, only a generic User-Agent.
/// </summary>
public static class Updater
{
    public const string Repo = "typekborispav/printer-switch";
    const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
    const string DownloadPrefix = "https://github.com/" + Repo + "/releases/download/";

    public static Version Current { get; } = Trim(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0));

    static Version Trim(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("PrinterSwitch/" + Current.ToString(3));
        return h;
    }

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrinterSwitch", "updates");

    /// <summary>The latest release if it is newer than this build, otherwise null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Api);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        using var resp = await Http.SendAsync(req, cts.Token);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
        var root = doc.RootElement;

        if (root.TryGetProperty("draft", out var d) && d.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var p) && p.GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
        latest = Trim(latest);
        if (latest <= Current) return null;

        string page = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            string name = a.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith("PrinterSwitch-Setup-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            string url = a.GetProperty("browser_download_url").GetString() ?? "";
            string digest = a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String ? dg.GetString() ?? "" : "";
            string sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : "";
            return new UpdateInfo(latest, page, url, name, a.GetProperty("size").GetInt64(), sha);
        }
        // A release without an installer yet (CI still building): show the page only.
        return new UpdateInfo(latest, page, "", "", 0, "");
    }

    /// <summary>True when the update can be installed from the app (installer with a published digest).</summary>
    public static bool CanInstall(UpdateInfo u) =>
        u.InstallerUrl.StartsWith(DownloadPrefix, StringComparison.Ordinal) && u.Sha256.Length == 64 &&
        u.InstallerName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>Downloads the installer and verifies its SHA-256. Returns the local path.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo u, IProgress<double> progress, CancellationToken ct = default)
    {
        if (!CanInstall(u)) throw new InvalidOperationException("update cannot be verified");
        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.GetFiles(Folder))
            try { File.Delete(old); } catch { }

        string path = Path.Combine(Folder, u.InstallerName);
        string tmp = path + ".part";
        using (var resp = await Http.GetAsync(u.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? u.Size;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(tmp);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                sha.AppendData(buf, 0, n);
                done += n;
                if (total > 0) progress.Report(Math.Min(1, (double)done / total));
            }
            string actual = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            if (actual != u.Sha256)
            {
                dst.Close();
                File.Delete(tmp);
                throw new InvalidDataException(L.T("upd.badhash"));
            }
        }
        File.Move(tmp, path, overwrite: true);
        Log.Write($"update {u.Version} downloaded and verified: {path}");
        return path;
    }

    /// <summary>
    /// Starts the installer silently (one UAC prompt). The installer starts the new
    /// version again once done. Returns false when the user declined the prompt.
    /// </summary>
    public static bool RunInstaller(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            Log.Write("update: UAC prompt declined");
            return false;
        }
    }
}
