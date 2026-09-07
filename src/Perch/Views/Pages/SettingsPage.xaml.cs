using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Perch.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Perch.Views.Pages;

public partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();

        App.Hotkeys.RegistrationFailed += OnHotkeyFailed;
        Unloaded += (_, _) => App.Hotkeys.RegistrationFailed -= OnHotkeyFailed;

        Loaded += (_, _) =>
        {
            _loading = true;

            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            VersionLabel.Text = $"Perch {version}";
            ConfigPathLabel.Text = App.Config.FilePath;

            var keys = App.Config.Config.Hotkeys;
            HkPin.Text = keys.TogglePinForeground;
            HkOverlay.Text = keys.ToggleOverlay;
            HkClickThrough.Text = keys.ToggleClickThrough;
            HkOpacityUp.Text = keys.OpacityUp;
            HkOpacityDown.Text = keys.OpacityDown;

            var general = App.Config.Config.General;
            ChkStartWithWindows.IsChecked = general.StartWithWindows;
            ChkStartMinimized.IsChecked = general.StartMinimized;
            ChkNotifications.IsChecked = general.ShowTrayNotifications;

            _loading = false;
        };
    }

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

        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            Warn("A shortcut needs at least one of Ctrl, Alt, Shift or Win.");
            return;
        }

        var gesture = Hotkey.Format(Keyboard.Modifiers, key);
        box.Text = gesture;

        var keys = App.Config.Config.Hotkeys;
        switch ((string)box.Tag)
        {
            case "pin": keys.TogglePinForeground = gesture; break;
            case "overlay": keys.ToggleOverlay = gesture; break;
            case "clickthrough": keys.ToggleClickThrough = gesture; break;
            case "opacityup": keys.OpacityUp = gesture; break;
            case "opacitydown": keys.OpacityDown = gesture; break;
        }

        App.Config.Save();
        HotkeyWarning.IsOpen = false;
        App.RegisterHotkeys();

        MainWindow.Say("Shortcut saved", gesture);
    }

    private void OnHotkeyFailed(string name, string gesture) =>
        Dispatcher.BeginInvoke(() =>
            Warn($"{gesture} is already claimed by another application — pick a different combination."));

    private void Warn(string message)
    {
        HotkeyWarning.Message = message;
        HotkeyWarning.IsOpen = true;
    }

    private void General_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var general = App.Config.Config.General;
        general.StartMinimized = ChkStartMinimized.IsChecked == true;
        general.ShowTrayNotifications = ChkNotifications.IsChecked == true;

        var startup = ChkStartWithWindows.IsChecked == true;
        if (startup != general.StartWithWindows)
        {
            general.StartWithWindows = startup;
            StartupService.SetEnabled(startup);
            MainWindow.Say(
                startup ? "Added to startup" : "Removed from startup",
                startup ? "Perch will start when you sign in." : "Perch will no longer start on its own.");
        }

        App.Config.Save();
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e) => Open(App.Config.Directory);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Log.Path))
        {
            MainWindow.Say("No log yet", "Perch has not had anything worth writing down.");
            return;
        }

        Open(Log.Path);
    }

    private static void Open(string path)
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
}
