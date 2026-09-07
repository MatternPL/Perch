using System.Threading;
using System.Windows;
using System.Windows.Media;
using Perch.Interop;
using Perch.Services;
using Perch.Views;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace Perch;

public partial class App : Application
{
    private const string InstanceMutexName = @"Global\Perch.SingleInstance";
    private const string ShowWindowEventName = @"Global\Perch.ShowWindow";

    /// <summary>
    /// Perch's own accent rather than the system one, and the same blue Brisk uses:
    /// these are the same person's tools and they should read as a set. It also has to
    /// be set here, not just in the resource dictionary, because WPF-UI derives the
    /// toggle and primary-button colours from this one value.
    /// </summary>
    public static readonly Color AccentColor = Color.FromRgb(0x4C, 0x8D, 0xFF);

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private bool _ownsInstanceMutex;

    public static ConfigService Config { get; private set; } = null!;
    public static PinService Pins { get; private set; } = null!;
    public static WindowRuleService Rules { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;
    public static TrayIcon Tray { get; private set; } = null!;

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

        ApplicationAccentColorManager.Apply(AccentColor, ApplicationTheme.Dark);

        Config = new ConfigService();
        Config.Load();

        // Keep the registry entry honest if Perch has been installed, moved or renamed.
        StartupService.Sync(Config.Config.General.StartWithWindows);

        Pins = new PinService();
        Hotkeys = new HotkeyService();
        Rules = new WindowRuleService(Config);

        RegisterHotkeys();

        if (Config.Config.General.RulesEnabled)
        {
            Rules.Start();

            // Whatever opened before Perch got here still has to end up in the right
            // place — at sign-in that is most of what the rules are for.
            Rules.CatchUpAfterStart();
        }

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

        Hotkeys.Register("unpinall", keys.UnpinAll, () =>
        {
            if (Pins.Count == 0)
            {
                Tray.Notify("Nothing pinned", "No window is being held on top.");
                return;
            }

            var count = Pins.Count;
            Pins.UnpinAll();
            Tray.Notify("Unpinned", $"{count} window(s) back to normal stacking.");
        });
    }

    private static string Trim(string text) =>
        text.Length > 60 ? text[..57] + "..." : text;

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
