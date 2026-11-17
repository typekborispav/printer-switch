// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using PrinterSwitch.Core;
using WinForms = System.Windows.Forms;

namespace PrinterSwitch.Ui;

/// <summary>Native Windows task dialogs.</summary>
public static class Dialogs
{
    static WinForms.TaskDialogPage Page(string heading, string text, WinForms.TaskDialogIcon icon) => new()
    {
        Caption = L.T("app.name"),
        Heading = heading,
        Text = text,
        Icon = icon,
        AllowCancel = true,
        SizeToContent = true
    };

    public static bool Confirm(string heading, string text, string yesText, bool warning = false)
    {
        var yes = new WinForms.TaskDialogButton(yesText);
        var page = Page(heading, text, warning ? WinForms.TaskDialogIcon.Warning : WinForms.TaskDialogIcon.Information);
        page.Buttons.Add(yes);
        page.Buttons.Add(new WinForms.TaskDialogButton(L.T("action.cancel")));
        page.DefaultButton = yes;
        return WinForms.TaskDialog.ShowDialog(page) == yes;
    }

    /// <summary>true = yes, false = no, null = cancel.</summary>
    public static bool? YesNoCancel(string heading, string text)
    {
        var yes = new WinForms.TaskDialogButton(L.T("action.yes"));
        var no = new WinForms.TaskDialogButton(L.T("action.no"));
        var page = Page(heading, text, WinForms.TaskDialogIcon.Warning);
        page.Buttons.Add(yes);
        page.Buttons.Add(no);
        page.Buttons.Add(WinForms.TaskDialogButton.Cancel);
        page.DefaultButton = yes;
        var r = WinForms.TaskDialog.ShowDialog(page);
        return r == yes ? true : r == no ? false : null;
    }

    public static void Error(string heading, string text)
    {
        var page = Page(heading, text, WinForms.TaskDialogIcon.Error);
        page.Buttons.Add(WinForms.TaskDialogButton.OK);
        var log = new WinForms.TaskDialogButton(L.T("action.log"));
        log.Click += (_, _) => TrayController.OpenLog();
        page.Buttons.Add(log);
        WinForms.TaskDialog.ShowDialog(page);
    }

    public const string RepoUrl = "https://github.com/typekborispav/printer-switch";

    public static void Info(string heading, string text)
    {
        var page = Page(heading, text, WinForms.TaskDialogIcon.Information);
        page.Buttons.Add(WinForms.TaskDialogButton.OK);
        WinForms.TaskDialog.ShowDialog(page);
    }
}
