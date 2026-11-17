// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PrinterSwitch.Core;

/// <summary>Bits of Windows the app touches: devices, printers, autostart, tray icon placement.</summary>
public static class WindowsSystem
{
    public static string ExePath => Environment.ProcessPath ?? "";

    // ------------------------------------------------------------ devices ---

    /// <summary>Waits until Windows has a working device with the given vendor:product.</summary>
    public static string? WaitForUsbDevice(string vidPid, TimeSpan timeout, CancellationToken ct = default)
    {
        var parts = vidPid.Split(':');
        if (parts.Length != 2) return null;
        string id = $"USB\\\\VID_{parts[0].ToUpperInvariant()}&PID_{parts[1].ToUpperInvariant()}%";
        string query = $"SELECT Name, Status FROM Win32_PnPEntity WHERE DeviceID LIKE '{id}'";
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until && !ct.IsCancellationRequested)
        {
            try
            {
                using var s = new ManagementObjectSearcher(query);
                foreach (var o in s.Get())
                {
                    if ((o["Status"] as string) == "OK")
                        return o["Name"] as string ?? vidPid;
                }
            }
            catch (Exception e)
            {
                Log.Write("WMI: " + e.Message);
            }
            Thread.Sleep(1500);
        }
        return null;
    }

    public static bool LocalPortInUse(int port)
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);

    // ----------------------------------------------------------- printers ---

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool AddPrinterConnection(string pName);

    public const int ErrorNotSupported = 50;

    /// <summary>Adds an IPP printer connection. Returns 0 or a Win32 error code.</summary>
    public static int AddNetworkPrinter(string url)
        => AddPrinterConnection(url) ? 0 : Marshal.GetLastWin32Error();

    /// <summary>Turns on the optional feature "Internet Printing Client" (UAC prompt).</summary>
    public static bool EnableInternetPrintingClient()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("dism.exe",
                "/online /enable-feature /featurename:Printing-Foundation-InternetPrinting-Client /all /norestart")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Minimized
            })!;
            p.WaitForExit();
            // 3010 = success, restart required
            return p.ExitCode is 0 or 3010;
        }
        catch (Exception e)
        {
            Log.Write("enabling Internet Printing Client failed: " + e.Message);
            return false;
        }
    }

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) { Log.Write("open " + target + ": " + e.Message); }
    }

    // ---------------------------------------------------------- autostart ---

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "PrinterSwitch";

    public static bool AutostartEnabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunValue) is string;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue(RunValue, $"\"{ExePath}\" --background");
            else k.DeleteValue(RunValue, false);
        }
    }

    // ------------------------------------------------------ tray promotion ---

    /// <summary>
    /// Windows 11 hides every new notification icon behind the chevron. Its entry in
    /// NotifyIconSettings only appears after the icon was shown once; IsPromoted = 1
    /// pulls it onto the taskbar. Only set when absent, so a user who hid the icon
    /// again keeps their choice. Returns true when the entry was found.
    /// </summary>
    public static bool PromoteTrayIcon()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (root == null) return false;
            foreach (string name in root.GetSubKeyNames())
            {
                using var k = root.OpenSubKey(name, writable: true);
                if (k?.GetValue("ExecutablePath") is string p && IsThisExe(p))
                {
                    if (k.GetValue("IsPromoted") == null) k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Log.Write("tray promotion: " + e.Message);
        }
        return false;
    }

    /// <summary>Is the icon on the taskbar? null = Windows has not registered it yet.</summary>
    public static bool? TrayIconPromoted()
    {
        try
        {
            using var k = OpenTrayEntry(false);
            return k == null ? null : k.GetValue("IsPromoted") is int v && v != 0;
        }
        catch { return null; }
    }

    /// <summary>Puts the icon on the taskbar or back behind the chevron. False = not registered yet.</summary>
    public static bool SetTrayIconPromoted(bool on)
    {
        try
        {
            using var k = OpenTrayEntry(true);
            if (k == null) return false;
            k.SetValue("IsPromoted", on ? 1 : 0, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception e)
        {
            Log.Write("tray promotion: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// Windows writes paths under known folders with the folder's GUID instead of the
    /// folder, e.g. "{6D809377-…}\Printer Switch\PrinterSwitch.exe" for Program Files.
    /// </summary>
    static bool IsThisExe(string stored)
    {
        string path = stored;
        if (stored.StartsWith('{') && stored.IndexOf('}') is int end and > 0 &&
            Guid.TryParse(stored[..(end + 1)], out var folder) &&
            SHGetKnownFolderPath(folder, 0, IntPtr.Zero, out string? root) == 0 && root != null)
            path = root + stored[(end + 1)..];
        return string.Equals(Path.GetFullPath(path), Path.GetFullPath(ExePath), StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token,
                                           [MarshalAs(UnmanagedType.LPWStr)] out string? path);

    static RegistryKey? OpenTrayEntry(bool writable)
    {
        using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
        if (root == null) return null;
        foreach (string name in root.GetSubKeyNames())
        {
            var k = root.OpenSubKey(name, writable);
            if (k?.GetValue("ExecutablePath") is string p && IsThisExe(p))
                return k;
            k?.Dispose();
        }
        return null;
    }

    // -------------------------------------------------------------- theme ---

    public static bool AppsUseDarkTheme()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }
}
