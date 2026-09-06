using System.Windows.Threading;
using Perch.Interop;
using Perch.Models;

namespace Perch.Services;

/// <summary>
/// Watches for windows appearing and moves the ones a rule matches to the monitor
/// and state the user asked for.
///
/// The tricky part is timing: most apps create their window, then resize and
/// reposition themselves a beat later while they restore their own saved layout.
/// Applying once on the show event loses that race, so every match is re-applied
/// on a short schedule until the app has settled.
/// </summary>
public sealed class WindowRuleService : IDisposable
{
    private static readonly int[] RetryDelaysMs = { 0, 250, 700, 1500, 2500 };

    private readonly ConfigService _config;
    private readonly Dispatcher _dispatcher;
    private readonly Native.WinEventProc _callback;   // kept alive for the lifetime of the hook
    private readonly Dictionary<IntPtr, DateTime> _recentlyHandled = new();

    private IntPtr _hook;
    private DateTime _lastPrune = DateTime.UtcNow;

    public event Action<WindowRule, WindowInfo>? RuleApplied;

    public WindowRuleService(ConfigService config)
    {
        _config = config;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _callback = OnWinEvent;
    }

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;

        _hook = Native.SetWinEventHook(
            Native.EVENT_OBJECT_SHOW,
            Native.EVENT_OBJECT_SHOW,
            IntPtr.Zero,
            _callback,
            0, 0,
            Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);

        if (_hook == IntPtr.Zero)
            Log.Error("SetWinEventHook failed; window rules will not fire.");
        else
            Log.Info("Window rules active.");
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        Native.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
        _recentlyHandled.Clear();
        Log.Info("Window rules stopped.");
    }

    /// <summary>Runs every enabled rule against the windows that are already open.</summary>
    public int ApplyToExistingWindows()
    {
        var applied = 0;

        foreach (var window in WindowInfo.EnumerateUserWindows())
        {
            var rule = FindMatch(window);
            if (rule is null) continue;

            ApplyWithRetries(rule, window);
            applied++;
        }

        return applied;
    }

    public void ApplyRuleNow(WindowRule rule)
    {
        foreach (var window in WindowInfo.EnumerateUserWindows())
        {
            if (Matches(rule, window))
                ApplyWithRetries(rule, window);
        }
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != Native.OBJID_WINDOW || idChild != 0) return;
        if (hWnd == IntPtr.Zero) return;
        if (!_config.Config.General.RulesEnabled) return;

        PruneHistory();

        if (!WindowInfo.IsUserWindow(hWnd)) return;

        var window = WindowInfo.From(hWnd);
        if (window is null) return;

        var rule = FindMatch(window);
        if (rule is null) return;

        if (_recentlyHandled.TryGetValue(hWnd, out var when))
        {
            // Already dealt with this handle: skip entirely for "apply once",
            // otherwise just avoid re-firing on our own repositioning.
            if (rule.ApplyOnce) return;
            if (DateTime.UtcNow - when < TimeSpan.FromSeconds(3)) return;
        }

        _recentlyHandled[hWnd] = DateTime.UtcNow;
        ApplyWithRetries(rule, window);
    }

    private WindowRule? FindMatch(WindowInfo window) =>
        _config.Config.Rules.FirstOrDefault(r => r.Enabled && Matches(r, window));

    private static bool Matches(WindowRule rule, WindowInfo window)
    {
        if (string.IsNullOrWhiteSpace(rule.ProcessName)) return false;

        var wanted = rule.ProcessName.Trim();
        if (wanted.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            wanted = wanted[..^4];

        if (!string.Equals(wanted, window.ProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(rule.TitleContains) &&
            window.Title.IndexOf(rule.TitleContains.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }

    private void ApplyWithRetries(WindowRule rule, WindowInfo window)
    {
        var monitor = MonitorService.Resolve(rule.MonitorDeviceName, rule.MonitorIndex);
        if (monitor is null)
        {
            Log.Warn($"Rule '{rule.DisplayName}': no monitor matched, skipping.");
            return;
        }

        foreach (var delay in RetryDelaysMs)
        {
            var ms = delay;
            _ = _dispatcher.BeginInvoke(async () =>
            {
                if (ms > 0) await Task.Delay(ms);
                if (!Native.IsWindow(window.Handle)) return;
                Apply(rule, window.Handle, monitor);
            }, DispatcherPriority.Background);
        }

        Log.Info($"Rule '{rule.DisplayName}' → {monitor.Label} for '{window.Display}'");
        RuleApplied?.Invoke(rule, window);
    }

    private static void Apply(WindowRule rule, IntPtr hWnd, MonitorTarget monitor)
    {
        try
        {
            if (rule.State == TargetWindowState.Minimized)
            {
                Native.ShowWindowAsync(hWnd, Native.SW_SHOWMINIMIZED);
                return;
            }

            // A maximised or minimised window cannot be moved between monitors,
            // so bring it back to a normal state first.
            if (Native.IsIconic(hWnd) || Native.IsZoomed(hWnd))
                Native.ShowWindow(hWnd, Native.SW_RESTORE);

            MoveToMonitor(hWnd, monitor);

            if (rule.State == TargetWindowState.Maximized)
                Native.ShowWindow(hWnd, Native.SW_SHOWMAXIMIZED);

            if (rule.AlwaysOnTop)
                PinService.SetTopmost(hWnd, true);
        }
        catch (Exception ex)
        {
            Log.Error($"Applying rule '{rule.DisplayName}' failed", ex);
        }
    }

    /// <summary>Centres the window on the target monitor, shrinking it if it does not fit.</summary>
    private static void MoveToMonitor(IntPtr hWnd, MonitorTarget monitor)
    {
        if (!Native.GetWindowRect(hWnd, out var rect)) return;

        var work = monitor.WorkArea;
        var width = Math.Min(rect.Width, work.Width);
        var height = Math.Min(rect.Height, work.Height);

        if (width <= 0 || height <= 0)
        {
            width = (int)(work.Width * 0.7);
            height = (int)(work.Height * 0.7);
        }

        var x = work.Left + (work.Width - width) / 2;
        var y = work.Top + (work.Height - height) / 2;

        Native.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void PruneHistory()
    {
        if (DateTime.UtcNow - _lastPrune < TimeSpan.FromMinutes(1)) return;
        _lastPrune = DateTime.UtcNow;

        var dead = _recentlyHandled.Keys.Where(h => !Native.IsWindow(h)).ToList();
        foreach (var h in dead) _recentlyHandled.Remove(h);
    }

    public void Dispose() => Stop();
}
