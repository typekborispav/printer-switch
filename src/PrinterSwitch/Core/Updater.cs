// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace PrinterSwitch.Core;

public sealed record UpdateInfo(Version Version, string PageUrl, string InstallerUrl, string InstallerName, long Size,
                                string Sha256, string SignatureUrl);

/// <summary>
/// A downloaded installer that passed every check, held open so that nothing can
/// swap the file between the check and the start (the folder is writable by any
/// program running as the user, and the installer is started elevated).
/// </summary>
public sealed class VerifiedInstaller(string path, FileStream lockHandle) : IDisposable
{
    public string Path { get; } = path;
    public void Dispose() => lockHandle.Dispose();
}

/// <summary>
/// Updates from GitHub Releases: asks the API for the latest release, downloads its
/// installer and the installer's signature, and runs it silently only when the
/// signature was made with the author's release key (tools\release-key.ps1). That key
/// never leaves the author's PC, so taking over the GitHub account or its build is
/// not enough to push a fake update. Nothing about the user is sent, only a generic
/// User-Agent.
/// </summary>
public static class Updater
{
    public const string Repo = "typekborispav/printer-switch";
    const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
    const string DownloadPrefix = "https://github.com/" + Repo + "/releases/download/";

    /// <summary>Public half of the release key (ECDSA P-256, SubjectPublicKeyInfo).</summary>
    const string SigningKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAES3m2ZJhLbhWyXvGR6AJFdpBUk5teeAaNYFBWnan03FRVncWfQoh5sjIzzc3X30r9zInLPr6M5fJ2nrmIa9D3fg==";

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
        var assets = root.GetProperty("assets").EnumerateArray().ToList();
        static string UrlOf(JsonElement a) => a.GetProperty("browser_download_url").GetString() ?? "";
        foreach (var a in assets)
        {
            string name = a.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith("PrinterSwitch-Setup-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            string digest = a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String ? dg.GetString() ?? "" : "";
            string sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : "";
            string sigUrl = assets.Where(s => s.GetProperty("name").GetString() == name + ".sig").Select(UrlOf).FirstOrDefault() ?? "";
            return new UpdateInfo(latest, page, UrlOf(a), name, a.GetProperty("size").GetInt64(), sha, sigUrl);
        }
        // A release without an installer: show the page only.
        return new UpdateInfo(latest, page, "", "", 0, "", "");
    }

    /// <summary>True when the update can be installed from the app (installer with a signature).</summary>
    public static bool CanInstall(UpdateInfo u) =>
        u.InstallerUrl.StartsWith(DownloadPrefix, StringComparison.Ordinal) &&
        u.SignatureUrl.StartsWith(DownloadPrefix, StringComparison.Ordinal) &&
        u.InstallerName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>The exact text a release is signed over (tools\release-common.ps1, Get-SignedText).</summary>
    public static string SignedText(Version version, string fileName, string sha256)
        => $"printer-switch release\nversion={version.ToString(3)}\nfile={fileName}\nsha256={sha256.ToLowerInvariant()}\n";

    public static bool SignatureValid(Version version, string fileName, string sha256, byte[] signature)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(SigningKey), out _);
        return ecdsa.VerifyData(System.Text.Encoding.UTF8.GetBytes(SignedText(version, fileName, sha256)), signature,
                                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// Downloads the installer and checks its signature (and GitHub's digest, if any).
    /// The returned object keeps the file locked; dispose it once the installer runs.
    /// </summary>
    public static async Task<VerifiedInstaller> DownloadAsync(UpdateInfo u, IProgress<double> progress, CancellationToken ct = default)
    {
        if (!CanInstall(u)) throw new InvalidOperationException("update cannot be verified");
        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.GetFiles(Folder))
            try { File.Delete(old); } catch { }

        byte[] signature;
        using (var resp = await Http.GetAsync(u.SignatureUrl, ct))
        {
            resp.EnsureSuccessStatusCode();
            string text = await resp.Content.ReadAsStringAsync(ct);
            try { signature = Convert.FromBase64String(text.Length < 1024 ? text.Trim() : ""); }
            catch (FormatException) { signature = []; }
        }

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
            if (u.Sha256.Length > 0 && actual != u.Sha256)
            {
                dst.Close();
                File.Delete(tmp);
                throw new InvalidDataException(L.T("upd.badhash"));
            }
        }
        File.Move(tmp, path, overwrite: true);

        // From here on the file is open with no write or delete sharing, and the check
        // runs on what is read through that very handle: what was checked is what runs.
        var lockHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            string locked = Convert.ToHexString(await SHA256.HashDataAsync(lockHandle, ct)).ToLowerInvariant();
            if (!SignatureValid(u.Version, u.InstallerName, locked, signature))
                throw new InvalidDataException(L.T("upd.badsig"));
        }
        catch
        {
            lockHandle.Dispose();
            try { File.Delete(path); } catch { }
            throw;
        }
        Log.Write($"update {u.Version} downloaded, signature verified: {path}");
        return new VerifiedInstaller(path, lockHandle);
    }

    /// <summary>
    /// Starts the installer silently (one UAC prompt). The installer starts the new
    /// version again once done. Returns false when the user declined the prompt.
    /// </summary>
    public static bool RunInstaller(VerifiedInstaller installer)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installer.Path, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
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
