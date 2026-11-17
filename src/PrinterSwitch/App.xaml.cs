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
        bool openSettings = e.Args.Contains("--settings");
        // Started again by the installer of an update.
        bool updated = e.Args.Contains("--updated");

        mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Already running: poke the running copy so it shows itself.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }

        var settings = Settings.Load();
        // Empty = follow the Windows display language (the default); the user can
        // pick another one in the setup wizard.
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

        if (updated)
            tray.Notify(L.F("upd.done", Updater.Current.ToString(3)));
        else if (setup || !settings.Configured)
            tray.ShowWizard();
        else if (openSettings)
            tray.ShowSettings();
        else if (!background)
            tray.ShowFlyout();
    }

    /// <summary>Lets an update installer run: it waits for this mutex to be gone.</summary>
    public void ReleaseInstance()
    {
        try { mutex?.ReleaseMutex(); } catch { }
        mutex?.Dispose();
        mutex = null;
    }

    public void ReclaimInstance() => mutex ??= new Mutex(true, MutexName, out _);

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
