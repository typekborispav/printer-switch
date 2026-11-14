// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Windows;
using PrinterSwitch.Core;
using PrinterSwitch.Ui;

namespace PrinterSwitch;

public partial class App : Application
{
    // Same name is used by the installer (AppMutex) to ask the user to close the app.
    const string MutexName = "PrinterSwitch.Instance";
    const string ShowEventName = "PrinterSwitch.Show";

    Mutex? mutex;
    EventWaitHandle? showEvent;
    TrayController? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Hardware rendering combined with the Fluent backdrop leaves windows blank
        // white on some GPU drivers (seen on an RTX 5080). A handful of small
        // windows gain nothing from the GPU, so software rendering it is.
        if (Environment.GetEnvironmentVariable("PRINTERSWITCH_GPU") != "1")
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        bool background = e.Args.Contains("--background");
        bool setup = e.Args.Contains("--setup");

        mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Already running: poke the running copy so it shows itself.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }

        var settings = Settings.Load();
        if (!settings.Configured && settings.Language.Length == 0)
        {
            // First start after installation: use the language picked in the installer.
            // Program Files install writes it to HKLM; a per-user install to HKCU.
            foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
            {
                using var k = hive.OpenSubKey(@"Software\PrinterSwitch");
                if (k?.GetValue("InstallerLanguage") is string lang && L.Codes.Contains(lang))
                {
                    settings.Language = lang;
                    break;
                }
            }
        }
        L.Init(settings.Language);
        Log.Write($"--- start {typeof(App).Assembly.GetName().Version} ---");

        DispatcherUnhandledException += (_, ev) =>
        {
            Log.Write("unhandled: " + ev.Exception);
            ev.Handled = true;
        };

        tray = new TrayController(settings);

        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var t = new Thread(() =>
        {
            while (showEvent.WaitOne())
                Dispatcher.BeginInvoke(() => tray?.ShowMain());
        }) { IsBackground = true };
        t.Start();

        if (setup || !settings.Configured)
            tray.ShowWizard();
        else if (!background)
            tray.ShowFlyout();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
