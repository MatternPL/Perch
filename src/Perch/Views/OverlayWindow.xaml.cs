using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Perch.Interop;
using Perch.Services;
using MessageBox = System.Windows.MessageBox;

namespace Perch.Views;

/// <summary>
/// The always-on-top viewer. A frameless WebView2 window that sits above a game
/// and gets out of the way on demand.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _topmostGuard;
    private readonly DispatcherTimer _hoverWatch;
    private IntPtr _handle;
    private bool _ready;
    private bool _toolbarVisible = true;

    private static Models.OverlaySettings Settings => App.Config.Config.Overlay;

    public OverlayWindow()
    {
        InitializeComponent();

        RestoreGeometry();

        // Some games claim the top of the z-order for themselves; re-assert on a slow beat.
        _topmostGuard = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _topmostGuard.Tick += (_, _) => KeepOnTop();

        // WebView2 hosts a child HWND, so WPF never sees the mouse enter the page.
        // Poll the cursor instead to decide whether the toolbar should be showing.
        _hoverWatch = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _hoverWatch.Tick += (_, _) => UpdateToolbarVisibility();

        Loaded += OnLoaded;
        Closing += OnClosing;
        DragHandle.MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void RestoreGeometry()
    {
        Width = Math.Max(MinWidth, Settings.Width);
        Height = Math.Max(MinHeight, Settings.Height);

        if (Settings.Left is not { } left || Settings.Top is not { } top)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        EnsureOnScreen();
    }

    /// <summary>Pull the overlay back into view if the monitor it lived on is gone.</summary>
    private void EnsureOnScreen()
    {
        var monitors = MonitorService.GetMonitors();
        if (monitors.Count == 0) return;

        var visible = monitors.Any(m =>
            Left + Width > m.Bounds.Left && Left < m.Bounds.Right &&
            Top + Height > m.Bounds.Top && Top < m.Bounds.Bottom);

        if (visible) return;

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        Left = primary.WorkArea.Left + 60;
        Top = primary.WorkArea.Top + 60;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;

        // Out of Alt+Tab, and layered so opacity works without AllowsTransparency
        // (which WebView2 cannot render into).
        Native.ToggleExStyle(_handle, Native.WS_EX_TOOLWINDOW, true);
        Native.ToggleExStyle(_handle, Native.WS_EX_APPWINDOW, false);
        Native.ToggleExStyle(_handle, Native.WS_EX_LAYERED, true);

        OpacitySlider.Value = Settings.Opacity;
        ApplyOpacity(Settings.Opacity);
        ApplyNoActivate(Settings.NoActivate);
        ApplyClickThrough(Settings.ClickThrough);

        KeepOnTop();
        if (Settings.AggressiveTopmost) _topmostGuard.Start();
        _hoverWatch.Start();

        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Perch", "WebView2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await Web.EnsureCoreWebView2Async(env);

            var core = Web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsSwipeNavigationEnabled = false;

            // Keep everything inside the overlay instead of spawning browser windows.
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                core.Navigate(args.Uri);
            };

            core.SourceChanged += (_, _) => AddressBox.Text = core.Source;
            core.DocumentTitleChanged += (_, _) => Title = $"Perch — {core.DocumentTitle}";

            _ready = true;
            LoadingOverlay.Visibility = Visibility.Collapsed;

            Navigate(Settings.Url);
        }
        catch (Exception ex)
        {
            Log.Error("WebView2 failed to start", ex);
            LoadingText.Text = "WebView2 Runtime is missing. Install it from microsoft.com, then reopen the overlay.";
            MessageBox.Show(
                "Perch needs the Microsoft Edge WebView2 Runtime for the built-in viewer.\n\n" +
                "You can still pin any existing window with the pin hotkey.\n\n" +
                $"Details: {ex.Message}",
                "Perch", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url.Trim();

        Settings.Url = url;
        App.Config.Save();

        if (_ready) Web.CoreWebView2.Navigate(url);
        else AddressBox.Text = url;
    }

    // ---- Always on top --------------------------------------------------

    private void KeepOnTop()
    {
        if (_handle == IntPtr.Zero) return;

        Native.SetWindowPos(_handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    public void SetAggressiveTopmost(bool on)
    {
        Settings.AggressiveTopmost = on;
        App.Config.Save();

        if (on)
        {
            KeepOnTop();
            _topmostGuard.Start();
        }
        else
        {
            _topmostGuard.Stop();
        }
    }

    // ---- Opacity --------------------------------------------------------

    private void ApplyOpacity(double value)
    {
        if (_handle == IntPtr.Zero) return;

        var alpha = (byte)Math.Clamp(value * 255.0, 51, 255);
        Native.SetLayeredWindowAttributes(_handle, 0, alpha, Native.LWA_ALPHA);
    }

    public double CurrentOpacity => OpacitySlider.Value;

    public void NudgeOpacity(double delta) => SetOpacity(OpacitySlider.Value + delta);

    public void SetOpacity(double value) =>
        OpacitySlider.Value = Math.Clamp(value, OpacitySlider.Minimum, OpacitySlider.Maximum);

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ApplyOpacity(e.NewValue);
        Settings.Opacity = e.NewValue;
        App.Config.Save();
    }

    // ---- Click-through --------------------------------------------------

    public void SetClickThrough(bool on)
    {
        ApplyClickThrough(on);
        Settings.ClickThrough = on;
        App.Config.Save();

        if (on)
        {
            App.Tray.Notify(
                "Overlay is click-through",
                $"Press {App.Config.Config.Hotkeys.ToggleClickThrough} to get the mouse back.");
        }
    }

    private void ApplyClickThrough(bool on)
    {
        if (_handle == IntPtr.Zero) return;

        Native.ToggleExStyle(_handle, Native.WS_EX_TRANSPARENT, on);
        ClickThroughButton.Foreground = on
            ? (System.Windows.Media.Brush)FindResource("Accent")
            : (System.Windows.Media.Brush)FindResource("TextMuted");
    }

    public void SetNoActivate(bool on)
    {
        ApplyNoActivate(on);
        Settings.NoActivate = on;
        App.Config.Save();
    }

    private void ApplyNoActivate(bool on)
    {
        if (_handle == IntPtr.Zero) return;
        Native.ToggleExStyle(_handle, Native.WS_EX_NOACTIVATE, on);
    }

    // ---- Toolbar auto-hide ----------------------------------------------

    private void UpdateToolbarVisibility()
    {
        if (!Settings.AutoHideToolbar)
        {
            if (!_toolbarVisible) FadeToolbar(true);
            return;
        }

        if (!Native.GetCursorPos(out var cursor)) return;
        if (!Native.GetWindowRect(_handle, out var rect)) return;

        var inside = cursor.X >= rect.Left && cursor.X <= rect.Right &&
                     cursor.Y >= rect.Top && cursor.Y <= rect.Bottom;

        if (inside != _toolbarVisible) FadeToolbar(inside);
    }

    private void FadeToolbar(bool show)
    {
        _toolbarVisible = show;

        var animation = new DoubleAnimation
        {
            To = show ? 1.0 : 0.0,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Toolbar.BeginAnimation(OpacityProperty, animation);
        Toolbar.IsHitTestVisible = show;
    }

    // ---- Toolbar actions ------------------------------------------------

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_ready && Web.CoreWebView2.CanGoBack) Web.CoreWebView2.GoBack();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_ready) Web.CoreWebView2.Reload();
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        var muted = !Web.CoreWebView2.IsMuted;
        Web.CoreWebView2.IsMuted = muted;
        MuteButton.Content = muted ? "\uE74F" : "\uE767";
        MuteButton.Foreground = muted
            ? (System.Windows.Media.Brush)FindResource("Accent")
            : (System.Windows.Media.Brush)FindResource("TextMuted");
    }

    private void ClickThrough_Click(object sender, RoutedEventArgs e) => SetClickThrough(!Settings.ClickThrough);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Navigate(AddressBox.Text);
        e.Handled = true;
    }

    // ---- Lifetime -------------------------------------------------------

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _topmostGuard.Stop();
        _hoverWatch.Stop();

        if (WindowState == WindowState.Normal)
        {
            Settings.Left = Left;
            Settings.Top = Top;
            Settings.Width = Width;
            Settings.Height = Height;
        }

        App.Config.SaveNow();

        try
        {
            Web.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"Disposing WebView2: {ex.Message}");
        }
    }
}
