using Perch.Interop;

namespace Perch.Services;

public sealed record MonitorTarget(
    int Index,
    string DeviceName,
    Native.RECT Bounds,
    Native.RECT WorkArea,
    bool IsPrimary)
{
    public string Label =>
        $"Monitor {Index} — {Bounds.Width} × {Bounds.Height}{(IsPrimary ? " (primary)" : "")}";

    public override string ToString() => Label;
}

/// <summary>Enumerates physical displays and resolves a rule's saved monitor back to a real one.</summary>
public static class MonitorService
{
    public static List<MonitorTarget> GetMonitors()
    {
        var raw = new List<(string Device, Native.RECT Bounds, Native.RECT Work, bool Primary)>();

        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref Native.RECT _, IntPtr _) =>
        {
            var mi = new Native.MONITORINFOEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>()
            };

            if (Native.GetMonitorInfo(hMonitor, ref mi))
                raw.Add((mi.szDevice, mi.rcMonitor, mi.rcWork, (mi.dwFlags & 1) != 0));

            return true;
        }, IntPtr.Zero);

        // Number them the way Windows' Display settings does: left to right, then top to bottom.
        return raw
            .OrderBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .Select((m, i) => new MonitorTarget(i + 1, m.Device, m.Bounds, m.Work, m.Primary))
            .ToList();
    }

    /// <summary>
    /// Match on the device name first — it survives windows being moved around — and
    /// fall back to the position index when a display has been unplugged or re-cabled.
    /// </summary>
    public static MonitorTarget? Resolve(string deviceName, int index)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            var byDevice = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (byDevice is not null) return byDevice;
        }

        return monitors.FirstOrDefault(m => m.Index == index) ?? monitors[0];
    }

    public static MonitorTarget? FromWindow(IntPtr hWnd)
    {
        var handle = Native.MonitorFromWindow(hWnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        if (handle == IntPtr.Zero) return null;

        var mi = new Native.MONITORINFOEX
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>()
        };
        if (!Native.GetMonitorInfo(handle, ref mi)) return null;

        return GetMonitors().FirstOrDefault(m =>
            string.Equals(m.DeviceName, mi.szDevice, StringComparison.OrdinalIgnoreCase));
    }
}
