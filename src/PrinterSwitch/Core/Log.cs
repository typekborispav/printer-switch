// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PrinterSwitch.Core;

/// <summary>Plain text log in %LOCALAPPDATA%\PrinterSwitch\log.txt, trimmed when it grows.</summary>
public static partial class Log
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrinterSwitch");

    public static string FilePath { get; } = Path.Combine(Folder, "log.txt");

    static readonly object Lock = new();
    const long MaxBytes = 512 * 1024;

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();

    public static string StripAnsi(string text) => Ansi().Replace(text, "");

    public static void Write(string text)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    string old = FilePath + ".1";
                    File.Move(FilePath, old, overwrite: true);
                }
                var sb = new StringBuilder();
                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                foreach (string line in StripAnsi(text).TrimEnd().Split('\n'))
                    sb.Append(stamp).Append("  ").Append(line.TrimEnd('\r')).Append(Environment.NewLine);
                File.AppendAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }
}
