// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Boris Pavelka

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PrinterSwitch.Ui;

/// <summary>
/// One row of the settings page, as in Windows 11 Settings: icon, title, description
/// and a control on the right. With <see cref="IsClickable"/> the whole row is a button.
/// </summary>
public class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(""));
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingsCard), new PropertyMetadata(""));
    public static readonly DependencyProperty IsClickableProperty =
        DependencyProperty.Register(nameof(IsClickable), typeof(bool), typeof(SettingsCard),
            new PropertyMetadata(false, (d, e) => ((SettingsCard)d).Focusable = (bool)e.NewValue));

    public static readonly DependencyProperty IsExternalProperty =
        DependencyProperty.Register(nameof(IsExternal), typeof(bool), typeof(SettingsCard), new PropertyMetadata(false));

    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SettingsCard));

    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    /// <summary>Clicking opens something outside the app: arrow out instead of the chevron.</summary>
    public bool IsExternal { get => (bool)GetValue(IsExternalProperty); set => SetValue(IsExternalProperty, value); }
    public bool IsClickable { get => (bool)GetValue(IsClickableProperty); set => SetValue(IsClickableProperty, value); }

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public SettingsCard() => Focusable = false;

    bool pressed;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsClickable || e.Handled) return;
        pressed = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!pressed) return;
        pressed = false;
        ReleaseMouseCapture();
        if (IsMouseOver) RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsClickable && e.Key is Key.Enter or Key.Space)
        {
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
            e.Handled = true;
        }
    }
}
