using System.Text.Json;

namespace Still.Core;

public sealed class SettingsStore(string directory)
{
    public string FilePath => Path.Combine(directory, "settings.json");
    public string? Notice { get; private set; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public Preferences Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return Preferences.CreateDefault();
            if (new FileInfo(FilePath).Length > 64 * 1024) throw new JsonException("Settings too large.");
            var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath), Options)
                ?? throw new JsonException("Empty settings.");
            p.Normalize();
            return p;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Notice = "Your saved preferences could not be read. Defaults are active.";
            try { File.Copy(FilePath, FilePath + ".damaged", true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return Preferences.CreateDefault();
        }
    }

    public bool Save(Preferences preferences)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, Options));
            File.Move(temporary, FilePath, true);
            Notice = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice = "Changes work for this session, but could not be saved. Check access to your app-data folder.";
            return false;
        }
    }
}
