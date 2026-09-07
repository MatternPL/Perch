using System.Text.Json.Serialization;

namespace Perch.Models;

public sealed class AppConfig
{
    public int Version { get; set; } = 2;
    public GeneralSettings General { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public List<WindowRule> Rules { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool RulesEnabled { get; set; } = true;
    public bool ShowTrayNotifications { get; set; } = true;
}

public sealed class HotkeySettings
{
    public string TogglePinForeground { get; set; } = "Ctrl+Alt+P";
    public string UnpinAll { get; set; } = "Ctrl+Alt+U";
}

public enum TargetWindowState
{
    Maximized,
    Normal,
    Minimized
}

public sealed class WindowRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;

    /// <summary>Friendly label shown in the list. Falls back to the process name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Process name without ".exe", e.g. "Discord". Case-insensitive.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Optional extra filter: window title must contain this text.</summary>
    public string TitleContains { get; set; } = string.Empty;

    /// <summary>Stable monitor id, e.g. "\\\\.\\DISPLAY2". Preferred over the index.</summary>
    public string MonitorDeviceName { get; set; } = string.Empty;

    /// <summary>Fallback when the device name no longer exists (monitor unplugged, ports swapped).</summary>
    public int MonitorIndex { get; set; } = 1;

    public TargetWindowState State { get; set; } = TargetWindowState.Maximized;

    public bool AlwaysOnTop { get; set; }

    /// <summary>Apply once per window handle instead of on every show event.</summary>
    public bool ApplyOnce { get; set; } = true;

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? (string.IsNullOrWhiteSpace(ProcessName) ? "Untitled rule" : ProcessName)
        : Name;
}
