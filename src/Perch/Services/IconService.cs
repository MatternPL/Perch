using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Perch.Services;

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

            source.Freeze();
            return source;
        }
        catch (Exception ex)
        {
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
