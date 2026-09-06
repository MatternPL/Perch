using System.Text.Json.Serialization;

namespace Perch.Models;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public GeneralSettings General { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public List<WindowRule> Rules { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool RulesEnabled { get; set; } = true;
    /// <summary>Re-apply a rule every time the window is shown, not just the first time.</summary>
    public bool ShowTrayNotifications { get; set; } = true;
}

public sealed class OverlaySettings
{
    public string Url { get; set; } = "https://www.youtube.com";

    /// <summary>Null until the overlay has been placed once. Not NaN — System.Text.Json refuses to write that.</summary>
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 640;
    public double Height { get; set; } = 380;

    /// <summary>0.20 - 1.00</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>Mouse events pass straight through to the game underneath.</summary>
    public bool ClickThrough { get; set; }

    /// <summary>Never take keyboard focus, so clicking it cannot minimise a borderless game.</summary>
    public bool NoActivate { get; set; }

    /// <summary>Re-assert topmost on a timer; some games grab the top of the z-order on their own.</summary>
    public bool AggressiveTopmost { get; set; } = true;

    /// <summary>Hide the toolbar until the mouse is over the overlay.</summary>
    public bool AutoHideToolbar { get; set; } = true;

    public bool MuteOnHide { get; set; }

    public List<string> Bookmarks { get; set; } = new()
    {
        "https://www.youtube.com",
        "https://www.twitch.tv"
    };
}

public sealed class HotkeySettings
{
    public string TogglePinForeground { get; set; } = "Ctrl+Alt+P";
    public string ToggleOverlay { get; set; } = "Ctrl+Alt+O";
    public string ToggleClickThrough { get; set; } = "Ctrl+Alt+C";
    public string OpacityUp { get; set; } = "Ctrl+Alt+Up";
    public string OpacityDown { get; set; } = "Ctrl+Alt+Down";
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
