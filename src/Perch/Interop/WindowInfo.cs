using System.Diagnostics;
using System.IO;

namespace Perch.Interop;

/// <summary>A top-level window as Perch sees it.</summary>
public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public uint ProcessId { get; init; }

    public string Display => string.IsNullOrWhiteSpace(Title) ? ProcessName : Title;

    public static WindowInfo? From(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !Native.IsWindow(hWnd)) return null;

        return new WindowInfo
        {
            Handle = hWnd,
            Title = Native.GetWindowTitle(hWnd),
            ClassName = Native.GetWindowClass(hWnd),
            ProcessId = Native.GetProcessId(hWnd),
            ProcessName = ProcessNameOf(hWnd)
        };
    }

    private static string ProcessNameOf(IntPtr hWnd)
    {
        try
        {
            var pid = Native.GetProcessId(hWnd);
            if (pid == 0) return string.Empty;
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Windows the user could plausibly want to pin or place: visible, titled, top-level,
    /// not a tool window, not a cloaked UWP ghost.
    /// </summary>
    public static bool IsUserWindow(IntPtr hWnd)
    {
        if (!Native.IsWindow(hWnd) || !Native.IsWindowVisible(hWnd)) return false;
        if (Native.GetAncestor(hWnd, Native.GA_ROOT) != hWnd) return false;
        if (Native.GetWindowTextLengthW(hWnd) == 0) return false;
        if (Native.IsCloaked(hWnd)) return false;

        var ex = Native.GetExStyle(hWnd);
        if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return false;

        var cls = Native.GetWindowClass(hWnd);
        return cls is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow");
    }

    public static List<WindowInfo> EnumerateUserWindows()
    {
        var result = new List<WindowInfo>();
        var self = Environment.ProcessId;

        Native.EnumWindows((hWnd, _) =>
        {
            if (IsUserWindow(hWnd) && Native.GetProcessId(hWnd) != (uint)self)
            {
                var info = From(hWnd);
                if (info is not null) result.Add(info);
            }
            return true;
        }, IntPtr.Zero);

        return result
            .OrderBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Best-effort executable path, used when adding a rule from a running app.</summary>
    public string? TryGetExecutablePath()
    {
        try
        {
            using var p = Process.GetProcessById((int)ProcessId);
            return p.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public string? TryGetExecutableFileName()
    {
        var path = TryGetExecutablePath();
        return path is null ? null : Path.GetFileName(path);
    }
}
