using Microsoft.Win32;

namespace Perch.Services;

/// <summary>
/// "Start with Windows" via the per-user Run key. No admin rights, no scheduled task,
/// and the user can see and remove it from Task Manager's Startup tab.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Perch";

    public static bool IsEnabled() => CurrentCommand() is not null;

    /// <summary>What the Run key says today, or null when there is no entry.</summary>
    public static string? CurrentCommand()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) as string;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read startup entry: {ex.Message}");
            return null;
        }
    }

    private static string? ExpectedCommand()
    {
        var exe = Environment.ProcessPath;
        return string.IsNullOrEmpty(exe) ? null : $"\"{exe}\" --minimized";
    }

    /// <summary>
    /// Bring the registry in line with the saved setting.
    ///
    /// This has to compare the whole command, not just whether an entry exists: after
    /// the installer moves Perch — or the user drags the portable copy elsewhere — the
    /// old entry still points at an executable that is no longer there, and Windows
    /// silently starts nothing.
    /// </summary>
    public static void Sync(bool enabled)
    {
        var current = CurrentCommand();

        if (!enabled)
        {
            if (current is not null) SetEnabled(false);
            return;
        }

        var expected = ExpectedCommand();
        if (expected is null) return;

        if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
        {
            SetEnabled(true);
            Log.Info($"Startup entry points at {Environment.ProcessPath}");
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var command = ExpectedCommand();
                if (command is null) return;
                key.SetValue(ValueName, command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not update startup entry: {ex.Message}");
        }
    }
}
