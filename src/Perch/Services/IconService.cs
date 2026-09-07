using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Perch.Services;

/// <summary>
/// Pulls the real icon out of a running application's executable, so the window list
/// shows Discord's icon next to Discord rather than a row of identical glyphs.
///
/// Icons are cached by executable path: the list refreshes every few seconds and
/// extracting an icon per row per refresh would be wasteful.
/// </summary>
public static class IconService
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static ImageSource? ForProcess(uint processId)
    {
        var path = ExecutablePath(processId);
        return path is null ? null : ForFile(path);
    }

    public static ImageSource? ForFile(string path)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;

            var image = Extract(path);
            Cache[path] = image;
            return image;
        }
    }

    private static ImageSource? Extract(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();   // shared across the UI thread's bindings
            return source;
        }
        catch (Exception ex)
        {
            // Elevated or protected processes simply do not hand theirs over.
            Log.Warn($"No icon for {path}: {ex.Message}");
            return null;
        }
    }

    private static string? ExecutablePath(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
