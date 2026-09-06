using System.Windows.Input;
using System.Windows.Interop;
using Perch.Interop;

namespace Perch.Services;

public sealed record Hotkey(uint Modifiers, uint VirtualKey)
{
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = new Hotkey(0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;

        uint mods = 0;
        Key key = Key.None;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= Native.MOD_CONTROL; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win" or "windows": mods |= Native.MOD_WIN; break;
                default:
                    if (!Enum.TryParse<Key>(raw, ignoreCase: true, out key)) return false;
                    break;
            }
        }

        if (key == Key.None || mods == 0) return false;

        hotkey = new Hotkey(mods | Native.MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(key));
        return true;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}

/// <summary>
/// Owns a message-only window and routes WM_HOTKEY to named actions.
/// Registration failures are reported rather than swallowed: a clash with another
/// app is the single most common reason a hotkey "does nothing".
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private readonly Dictionary<string, int> _idsByName = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 0xB000;
    private bool _disposed;

    /// <summary>Raised when a hotkey could not be claimed, usually because another app owns it.</summary>
    public event Action<string, string>? RegistrationFailed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("Perch.HotkeySink")
        {
            ParentWindow = Native.HWND_MESSAGE,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public IntPtr Handle => _source.Handle;

    public bool Register(string name, string? gesture, Action action)
    {
        Unregister(name);

        if (!Hotkey.TryParse(gesture, out var hotkey))
        {
            if (!string.IsNullOrWhiteSpace(gesture))
                RegistrationFailed?.Invoke(name, gesture!);
            return false;
        }

        var id = _nextId++;
        if (!Native.RegisterHotKey(_source.Handle, id, hotkey.Modifiers, hotkey.VirtualKey))
        {
            Log.Warn($"Hotkey '{gesture}' for {name} is already taken by another application.");
            RegistrationFailed?.Invoke(name, gesture!);
            return false;
        }

        _actions[id] = action;
        _idsByName[name] = id;
        return true;
    }

    public void Unregister(string name)
    {
        if (!_idsByName.TryGetValue(name, out var id)) return;
        Native.UnregisterHotKey(_source.Handle, id);
        _actions.Remove(id);
        _idsByName.Remove(name);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != Native.WM_HOTKEY) return IntPtr.Zero;

        if (_actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("Hotkey action failed", ex);
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _idsByName.Values)
            Native.UnregisterHotKey(_source.Handle, id);

        _idsByName.Clear();
        _actions.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
