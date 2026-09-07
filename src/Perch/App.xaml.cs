using System.Threading;
using System.Windows;
using Perch.Interop;
using Perch.Services;
using Perch.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Perch;

public partial class App : Application
{
    private const string InstanceMutexName = @"Global\Perch.SingleInstance";
    private const string ShowWindowEventName = @"Global\Perch.ShowWindow";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private bool _ownsInstanceMutex;

    public static ConfigService Config { get; private set; } = null!;
    public static PinService Pins { get; private set; } = null!;
    public static WindowRuleService Rules { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;
    public static TrayIcon Tray { get; private set; } = null!;

    public static OverlayWindow? Overlay { get; set; }
    public static MainWindow? MainView { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!ClaimSingleInstance())
        {
            _showWindowEvent?.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled exception", args.Exception);
            MessageBox.Show(
                $"Perch hit an unexpected error:\n\n{args.Exception.Message}\n\nDetails were written to:\n{Log.Path}",
                "Perch", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        Config = new ConfigService();
        Config.Load();

        // Keep the registry entry honest if Perch has been installed, moved or renamed.
        StartupService.Sync(Config.Config.General.StartWithWindows);

        Pins = new PinService();
        Hotkeys = new HotkeyService();
        Rules = new WindowRuleService(Config);

        RegisterHotkeys();

        if (Config.Config.General.RulesEnabled)
            Rules.Start();

        Tray = new TrayIcon();
        Tray.Initialize();

        var startMinimized = Config.Config.General.StartMinimized ||
                             e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        if (!startMinimized)
            ShowMainWindow();

        Log.Info($"Perch started (minimized: {startMinimized}).");
    }

    public static void RegisterHotkeys()
    {
        var keys = Config.Config.Hotkeys;

        Hotkeys.Register("pin", keys.TogglePinForeground, () =>
        {
            var result = Pins.ToggleForeground();
            if (result is null)
            {
                Tray.Notify("Nothing to pin", "Focus a normal application window first.");
                return;
            }

            var title = Native.GetWindowTitle(Native.GetForegroundWindow());
            Tray.Notify(result.Value ? "Pinned on top" : "Unpinned", Trim(title));
        });

        Hotkeys.Register("overlay", keys.ToggleOverlay, ToggleOverlay);

        Hotkeys.Register("clickthrough", keys.ToggleClickThrough, () =>
        {
            if (Overlay is null) return;
            Overlay.SetClickThrough(!Config.Config.Overlay.ClickThrough);
        });

    }

    private static string Trim(string text) =>
        text.Length > 60 ? text[..57] + "..." : text;

    public static void ToggleOverlay()
    {
        if (Overlay is { IsLoaded: true })
        {
            Overlay.Close();
            return;
        }

        ShowOverlay();
    }

    public static void ShowOverlay(string? url = null)
    {
        if (Overlay is { IsLoaded: true })
        {
            if (url is not null) Overlay.Navigate(url);
            Overlay.Activate();
            return;
        }

        Overlay = new OverlayWindow();
        Overlay.Closed += (_, _) => Overlay = null;
        Overlay.Show();

        if (url is not null) Overlay.Navigate(url);
    }

    public static void ShowMainWindow()
    {
        if (MainView is null || !MainView.IsLoaded)
        {
            MainView = new MainWindow();
            MainView.Closed += (_, _) => MainView = null;
        }

        MainView.Show();
        if (MainView.WindowState == WindowState.Minimized)
            MainView.WindowState = WindowState.Normal;

        MainView.Activate();
        MainView.Focus();
    }

    public static void ExitApp()
    {
        Config.SaveNow();
        Current.Shutdown();
    }

    private bool ClaimSingleInstance()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isNew);
        _ownsInstanceMutex = isNew;

        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);

        if (!isNew) return false;

        var thread = new Thread(() =>
        {
            while (_showWindowEvent!.WaitOne())
            {
                Dispatcher.Invoke(ShowMainWindow);
            }
        })
        {
            IsBackground = true,
            Name = "Perch.InstanceListener"
        };
        thread.Start();

        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Tray?.Dispose();
            Hotkeys?.Dispose();
            Rules?.Dispose();
            Pins?.Dispose();
            Config?.SaveNow();

            // A second launch never owned the mutex — it only signalled the running
            // instance and quit. Releasing it from here would throw every time.
            if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"Shutdown cleanup: {ex.Message}");
        }

        base.OnExit(e);
    }
}
