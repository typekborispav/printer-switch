// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrinterSwitch.Core;

/// <summary>User settings, stored as JSON in %APPDATA%\PrinterSwitch.</summary>
public sealed class Settings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "";

    /// <summary>Private key used for SSH. Empty = not set up yet.</summary>
    public string KeyPath { get; set; } = "";

    /// <summary>SHA-256 fingerprint of the server's host key, trusted on first use.</summary>
    public string HostKeyFingerprint { get; set; } = "";

    /// <summary>Bus id or vendor:product of the printer; empty = first USB printer.</summary>
    public string Device { get; set; } = "";

    /// <summary>vendor:product reported by the server the last time the printer was lent.</summary>
    public string LastVidPid { get; set; } = "";

    /// <summary>Description of the printer as last seen, for display only.</summary>
    public string DeviceName { get; set; } = "";

    /// <summary>CUPS queue used when adding the network printer to Windows.</summary>
    public string Queue { get; set; } = "";

    /// <summary>Local end of the SSH tunnel that carries USB/IP.</summary>
    public int LocalPort { get; set; } = 3240;

    /// <summary>"", "cs" or "en". Empty follows Windows.</summary>
    public string Language { get; set; } = "";

    public int RefreshSeconds { get; set; } = 45;

    /// <summary>Pull the tray icon out from behind the chevron once Windows has registered it.</summary>
    public bool PromoteTrayPending { get; set; }

    /// <summary>True once the setup wizard finished at least once.</summary>
    public bool Configured { get; set; }

    [JsonIgnore]
    public bool HasServer => Host.Length > 0 && User.Length > 0 && KeyPath.Length > 0;

    // ------------------------------------------------------------------

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrinterSwitch");

    static string FilePath => Path.Combine(Folder, "settings.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch (Exception e)
        {
            Log.Write("settings could not be read: " + e.Message);
        }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }

    public Settings Clone() => (Settings)MemberwiseClone();
}
