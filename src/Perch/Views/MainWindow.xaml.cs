using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Perch.Interop;
using Perch.Models;
using Perch.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Perch.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<WindowRow> _windows = new();
    private readonly ObservableCollection<RuleRow> _rules = new();
    private readonly DispatcherTimer _windowListRefresh;
    private bool _loading = true;

    private static AppConfig Config => App.Config.Config;

    public MainWindow()
    {
        InitializeComponent();

        WindowList.ItemsSource = _windows;
        RuleList.ItemsSource = _rules;

        _windowListRefresh = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _windowListRefresh.Tick += (_, _) =>
        {
            if (PagePinned.Visibility == Visibility.Visible) RefreshWindows();
        };

        TitleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) return;
            DragMove();
        };

        App.Pins.Changed += OnPinsChanged;
        App.Hotkeys.RegistrationFailed += OnHotkeyFailed;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionLabel.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        ConfigPathLabel.Text = App.Config.FilePath;

        LoadSettingsIntoUi();
        RefreshRules();
        RefreshWindows();
        _windowListRefresh.Start();

        _loading = false;
        Status(App.Rules.IsRunning ? "Placement rules are active." : "Placement rules are paused.");
    }

    private void LoadSettingsIntoUi()
    {
        _loading = true;

        var overlay = Config.Overlay;
        OverlayUrl.Text = overlay.Url;
        ChkAggressive.IsChecked = overlay.AggressiveTopmost;
        ChkNoActivate.IsChecked = overlay.NoActivate;
        ChkClickThrough.IsChecked = overlay.ClickThrough;
        ChkAutoHide.IsChecked = overlay.AutoHideToolbar;
        OpacityControl.Value = overlay.Opacity;
        OpacityLabel.Text = $"{overlay.Opacity * 100:0}%";
        BookmarkList.ItemsSource = overlay.Bookmarks;

        ChkRulesEnabled.IsChecked = Config.General.RulesEnabled;
        ChkStartWithWindows.IsChecked = Config.General.StartWithWindows;
        ChkStartMinimized.IsChecked = Config.General.StartMinimized;
        ChkNotifications.IsChecked = Config.General.ShowTrayNotifications;

        HkPin.Text = Config.Hotkeys.TogglePinForeground;
        HkOverlay.Text = Config.Hotkeys.ToggleOverlay;
        HkClickThrough.Text = Config.Hotkeys.ToggleClickThrough;
        HkOpacityUp.Text = Config.Hotkeys.OpacityUp;
        HkOpacityDown.Text = Config.Hotkeys.OpacityDown;

        PinHint.Text = $"{Config.Hotkeys.TogglePinForeground} pins the focused window";
        OverlayHint.Text = $"{Config.Hotkeys.ToggleOverlay} toggles the overlay";
        PinHotkeyLabel.Text = $"Or press {Config.Hotkeys.TogglePinForeground} while the window is in front.";

        _loading = false;
    }

    // ==================== Navigation ====================

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PageOverlay is null) return; // fires once during InitializeComponent

        var tag = (sender as RadioButton)?.Tag as string;

        PageOverlay.Visibility = tag == "Overlay" ? Visibility.Visible : Visibility.Collapsed;
        PagePinned.Visibility = tag == "Pinned" ? Visibility.Visible : Visibility.Collapsed;
        PageRules.Visibility = tag == "Rules" ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "Pinned") RefreshWindows();
        if (tag == "Rules") RefreshRules();
    }

    // ==================== Overlay page ====================

    private void OpenOverlay_Click(object sender, RoutedEventArgs e)
    {
        App.ShowOverlay(OverlayUrl.Text);
        Status("Overlay opened.");
    }

    private void OverlayUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        App.ShowOverlay(OverlayUrl.Text);
        e.Handled = true;
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string url) return;
        OverlayUrl.Text = url;
        App.ShowOverlay(url);
    }

    private void OverlayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var overlay = Config.Overlay;
        overlay.AggressiveTopmost = ChkAggressive.IsChecked == true;
        overlay.NoActivate = ChkNoActivate.IsChecked == true;
        overlay.AutoHideToolbar = ChkAutoHide.IsChecked == true;

        var clickThrough = ChkClickThrough.IsChecked == true;

        if (App.Overlay is { IsLoaded: true } live)
        {
            live.SetAggressiveTopmost(overlay.AggressiveTopmost);
            live.SetNoActivate(overlay.NoActivate);
            if (clickThrough != overlay.ClickThrough) live.SetClickThrough(clickThrough);
        }

        overlay.ClickThrough = clickThrough;
        App.Config.Save();
    }

    private void OpacityControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel is null) return;

        OpacityLabel.Text = $"{e.NewValue * 100:0}%";
        if (_loading) return;

        Config.Overlay.Opacity = e.NewValue;
        App.Config.Save();

        if (App.Overlay is { IsLoaded: true } live)
            live.SetOpacity(e.NewValue);
    }

    // ==================== Pinned page ====================

    private void RefreshWindows()
    {
        var selectedHandle = (WindowList.SelectedItem as WindowRow)?.Handle;

        _windows.Clear();
        foreach (var window in WindowInfo.EnumerateUserWindows())
            _windows.Add(new WindowRow(window, App.Pins.IsPinned(window.Handle)));

        // Pinned windows first: those are the ones the user is managing right now.
        var ordered = _windows.OrderByDescending(w => w.IsPinned).ToList();
        _windows.Clear();
        foreach (var row in ordered) _windows.Add(row);

        if (selectedHandle is not null)
            WindowList.SelectedItem = _windows.FirstOrDefault(w => w.Handle == selectedHandle);

        PinnedCount.Text = App.Pins.Count switch
        {
            0 => "No windows pinned",
            1 => "1 window pinned",
            var n => $"{n} windows pinned"
        };
    }

    private void OnPinsChanged() => Dispatcher.BeginInvoke(RefreshWindows);

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not WindowRow row) return;

        App.Pins.TogglePin(row.Handle);
        RefreshWindows();
    }

    private void RefreshWindows_Click(object sender, RoutedEventArgs e) => RefreshWindows();

    private void UnpinAll_Click(object sender, RoutedEventArgs e)
    {
        App.Pins.UnpinAll();
        RefreshWindows();
        Status("All windows unpinned.");
    }

    // ==================== Rules page ====================

    private void RefreshRules()
    {
        _rules.Clear();
        foreach (var rule in Config.Rules) _rules.Add(new RuleRow(rule));

        NoRulesHint.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NewRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new WindowRule();
        var editor = new RuleEditorWindow(rule, isNew: true) { Owner = this };

        if (editor.ShowDialog() != true) return;

        Config.Rules.Add(rule);
        App.Config.Save();
        RefreshRules();
        Status($"Rule for {rule.DisplayName} created.");
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RuleRow row) return;
        EditRule(row);
    }

    private void RuleList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RuleList.SelectedItem is RuleRow row) EditRule(row);
    }

    private void EditRule(RuleRow row)
    {
        var editor = new RuleEditorWindow(row.Rule, isNew: false) { Owner = this };
        if (editor.ShowDialog() != true) return;

        App.Config.Save();
        RefreshRules();
        Status($"Rule for {row.Rule.DisplayName} updated.");
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RuleRow row) return;

        var answer = MessageBox.Show(
            $"Delete the rule for {row.Rule.DisplayName}?",
            "Perch", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        Config.Rules.RemoveAll(r => r.Id == row.Rule.Id);
        App.Config.Save();
        RefreshRules();
        Status("Rule deleted.");
    }

    private void RuleEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if ((sender as CheckBox)?.Tag is not RuleRow) return;

        App.Config.Save();
    }

    private void ApplyRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RuleRow row) return;

        App.Rules.ApplyRuleNow(row.Rule);
        Status($"Applied {row.Rule.DisplayName} to any open window.");
    }

    private void ApplyAll_Click(object sender, RoutedEventArgs e)
    {
        var count = App.Rules.ApplyToExistingWindows();
        Status(count == 0 ? "No open window matched a rule." : $"Applied rules to {count} window(s).");
    }

    private void RulesEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var on = ChkRulesEnabled.IsChecked == true;
        Config.General.RulesEnabled = on;
        App.Config.Save();

        if (on) App.Rules.Start();
        else App.Rules.Stop();

        Status(on ? "Placement rules are active." : "Placement rules are paused.");
    }

    // ==================== Settings page ====================

    private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            ShowHotkeyWarning("A shortcut needs at least one of Ctrl, Alt, Shift or Win.");
            return;
        }

        var gesture = Hotkey.Format(modifiers, key);
        box.Text = gesture;

        var name = (string)box.Tag;
        switch (name)
        {
            case "pin": Config.Hotkeys.TogglePinForeground = gesture; break;
            case "overlay": Config.Hotkeys.ToggleOverlay = gesture; break;
            case "clickthrough": Config.Hotkeys.ToggleClickThrough = gesture; break;
            case "opacityup": Config.Hotkeys.OpacityUp = gesture; break;
            case "opacitydown": Config.Hotkeys.OpacityDown = gesture; break;
        }

        App.Config.Save();
        HotkeyWarning.Visibility = Visibility.Collapsed;
        App.RegisterHotkeys();

        PinHint.Text = $"{Config.Hotkeys.TogglePinForeground} pins the focused window";
        OverlayHint.Text = $"{Config.Hotkeys.ToggleOverlay} toggles the overlay";
        PinHotkeyLabel.Text = $"Or press {Config.Hotkeys.TogglePinForeground} while the window is in front.";
        Status($"Shortcut set to {gesture}.");
    }

    private void OnHotkeyFailed(string name, string gesture) =>
        Dispatcher.BeginInvoke(() =>
            ShowHotkeyWarning($"{gesture} is already claimed by another application — pick a different combination."));

    private void ShowHotkeyWarning(string message)
    {
        HotkeyWarning.Text = message;
        HotkeyWarning.Visibility = Visibility.Visible;
    }

    private void General_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var general = Config.General;
        general.StartMinimized = ChkStartMinimized.IsChecked == true;
        general.ShowTrayNotifications = ChkNotifications.IsChecked == true;

        var startup = ChkStartWithWindows.IsChecked == true;
        if (startup != general.StartWithWindows)
        {
            general.StartWithWindows = startup;
            StartupService.SetEnabled(startup);
            Status(startup ? "Perch will start with Windows." : "Perch will no longer start with Windows.");
        }

        App.Config.Save();
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e) => OpenPath(App.Config.Directory);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Log.Path))
        {
            Status("No log file yet.");
            return;
        }

        OpenPath(Log.Path);
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open {path}: {ex.Message}");
        }
    }

    // ==================== Window chrome ====================

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseToTray_Click(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _windowListRefresh.Stop();
        App.Pins.Changed -= OnPinsChanged;
        App.Hotkeys.RegistrationFailed -= OnHotkeyFailed;
        App.Config.SaveNow();
    }

    private void Status(string message) => StatusText.Text = message;
}

// ==================== Row view models ====================

public sealed class WindowRow
{
    private readonly WindowInfo _info;

    public WindowRow(WindowInfo info, bool isPinned)
    {
        _info = info;
        IsPinned = isPinned;
    }

    public IntPtr Handle => _info.Handle;
    public bool IsPinned { get; }
    public string Title => string.IsNullOrWhiteSpace(_info.Title) ? _info.ProcessName : _info.Title;

    public string Subtitle
    {
        get
        {
            var monitor = MonitorService.FromWindow(_info.Handle);
            var where = monitor is null ? "" : $" · Monitor {monitor.Index}";
            return $"{_info.ProcessName}.exe{where}{(IsPinned ? " · pinned on top" : "")}";
        }
    }

    public string ActionLabel => IsPinned ? "Unpin" : "Pin on top";
}

public sealed class RuleRow
{
    public RuleRow(WindowRule rule) => Rule = rule;

    public WindowRule Rule { get; }

    public string Headline => Rule.DisplayName;

    public string Detail
    {
        get
        {
            var monitor = MonitorService.Resolve(Rule.MonitorDeviceName, Rule.MonitorIndex);
            var where = monitor is null ? $"Monitor {Rule.MonitorIndex}" : monitor.Label;

            var state = Rule.State switch
            {
                TargetWindowState.Maximized => "maximised",
                TargetWindowState.Minimized => "minimised",
                _ => "windowed"
            };

            var title = string.IsNullOrWhiteSpace(Rule.TitleContains)
                ? ""
                : $" · title contains \"{Rule.TitleContains}\"";

            var top = Rule.AlwaysOnTop ? " · always on top" : "";

            return $"{Rule.ProcessName}.exe → {where}, {state}{title}{top}";
        }
    }
}
