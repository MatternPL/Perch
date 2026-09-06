using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Perch.Models;

namespace Perch.Services;

/// <summary>Loads and saves %APPDATA%\Perch\config.json. Writes are debounced and atomic.</summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly System.Timers.Timer _debounce;

    public string Directory { get; }
    public string FilePath { get; }
    public AppConfig Config { get; private set; } = new();

    public ConfigService()
    {
        Directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Perch");
        FilePath = Path.Combine(Directory, "config.json");

        _debounce = new System.Timers.Timer(600) { AutoReset = false };
        _debounce.Elapsed += (_, _) => SaveNow();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded is not null)
                {
                    Config = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read config, starting fresh: {ex.Message}");
            TryBackupCorruptFile();
        }

        Config = new AppConfig();
    }

    /// <summary>Queue a save. Call this freely from UI events.</summary>
    public void Save()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void SaveNow()
    {
        lock (_gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Config, JsonOptions));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not write config: {ex.Message}");
            }
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Move(FilePath, FilePath + ".bad", overwrite: true);
        }
        catch
        {
            // Nothing useful to do; a fresh config will overwrite it anyway.
        }
    }
}
