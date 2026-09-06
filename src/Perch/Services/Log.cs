using System.IO;

namespace Perch.Services;

/// <summary>
/// Small append-only log next to the config. Perch touches other processes' windows,
/// so when something does not stick it helps to have a record.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string Path => _path ??= System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Perch", "perch.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) => Write("ERROR", $"{message} :: {ex}");

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path)!;
                Directory.CreateDirectory(dir);

                var file = new FileInfo(Path);
                if (file.Exists && file.Length > 512 * 1024)
                    File.Move(Path, Path + ".1", overwrite: true);

                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }
}
