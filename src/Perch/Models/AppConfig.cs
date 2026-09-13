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

    public string Name { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public string TitleContains { get; set; } = string.Empty;

    public string MonitorDeviceName { get; set; } = string.Empty;

    public int MonitorIndex { get; set; } = 1;

    public TargetWindowState State { get; set; } = TargetWindowState.Maximized;

    public bool AlwaysOnTop { get; set; }

    public bool ApplyOnce { get; set; } = true;

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? (string.IsNullOrWhiteSpace(ProcessName) ? "Untitled rule" : ProcessName)
        : Name;
}
