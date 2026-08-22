using System.IO;
using System.Text.Json;
using WorkStationX.Infrastructure;

namespace WorkStationX.Services;

/// <summary>User preferences that do not belong in the database.</summary>
public class UserSettings
{
    public string ThemeId { get; set; } = ThemeService.DefaultThemeId;

    /// <summary>Most recent picked colours, newest first.</summary>
    public List<string> RecentColors { get; set; } = new();

    /// <summary>Global shortcuts, keyed by action name.</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = new();
}

public interface ISettingsService
{
    UserSettings Current { get; }
    void Save();
}

/// <summary>
/// Reads and writes %APPDATA%\WorkStationX\settings.json. A corrupt or missing file
/// falls back to defaults rather than blocking startup - losing a preference is a far
/// better outcome than an app that will not open.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SettingsService() => Current = Load();

    public UserSettings Current { get; private set; }

    private static UserSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Serilog.Log.Warning(ex, "Could not read settings; using defaults");
        }

        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureCreated();
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Error(ex, "Could not save settings");
        }
    }
}
