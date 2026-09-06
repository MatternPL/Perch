using System.Windows.Threading;
using Perch.Interop;

namespace Perch.Services;

/// <summary>
/// Keeps a set of foreign windows above everything else.
///
/// Windows will happily drop a window out of the topmost band when a full-screen
/// app takes over, so pinned windows are re-asserted on a slow timer instead of
/// being set once and forgotten.
/// </summary>
public sealed class PinService : IDisposable
{
    private readonly HashSet<IntPtr> _pinned = new();
    private readonly DispatcherTimer _keepAlive;

    public event Action? Changed;

    public PinService()
    {
        _keepAlive = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _keepAlive.Tick += (_, _) => Reassert();
    }

    public IReadOnlyCollection<IntPtr> Pinned => _pinned;

    public bool IsPinned(IntPtr hWnd) => _pinned.Contains(hWnd);

    public int Count => _pinned.Count;

    public bool TogglePin(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !Native.IsWindow(hWnd)) return false;

        if (_pinned.Contains(hWnd))
        {
            Unpin(hWnd);
            return false;
        }

        Pin(hWnd);
        return true;
    }

    public void Pin(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !Native.IsWindow(hWnd)) return;

        SetTopmost(hWnd, true);
        _pinned.Add(hWnd);

        if (!_keepAlive.IsEnabled) _keepAlive.Start();
        Log.Info($"Pinned '{Native.GetWindowTitle(hWnd)}'");
        Changed?.Invoke();
    }

    public void Unpin(IntPtr hWnd)
    {
        if (Native.IsWindow(hWnd)) SetTopmost(hWnd, false);
        _pinned.Remove(hWnd);

        if (_pinned.Count == 0) _keepAlive.Stop();
        Changed?.Invoke();
    }

    public void UnpinAll()
    {
        foreach (var hWnd in _pinned.ToList())
        {
            if (Native.IsWindow(hWnd)) SetTopmost(hWnd, false);
        }

        _pinned.Clear();
        _keepAlive.Stop();
        Changed?.Invoke();
    }

    /// <summary>Pins whatever the user is looking at. Returns the new state, or null if there was nothing to pin.</summary>
    public bool? ToggleForeground()
    {
        var hWnd = Native.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;

        // Never let Perch pin its own settings window by accident.
        if (Native.GetProcessId(hWnd) == (uint)Environment.ProcessId) return null;
        if (!WindowInfo.IsUserWindow(hWnd)) return null;

        return TogglePin(hWnd);
    }

    public static void SetTopmost(IntPtr hWnd, bool topmost)
    {
        Native.SetWindowPos(
            hWnd,
            topmost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST,
            0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private void Reassert()
    {
        var dead = new List<IntPtr>();

        foreach (var hWnd in _pinned)
        {
            if (!Native.IsWindow(hWnd))
            {
                dead.Add(hWnd);
                continue;
            }

            if ((Native.GetExStyle(hWnd) & Native.WS_EX_TOPMOST) == 0)
                SetTopmost(hWnd, true);
        }

        if (dead.Count == 0) return;

        foreach (var hWnd in dead) _pinned.Remove(hWnd);
        if (_pinned.Count == 0) _keepAlive.Stop();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _keepAlive.Stop();
        UnpinAll();
    }
}
