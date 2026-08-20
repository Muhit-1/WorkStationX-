using System.IO;

namespace WorkStationX.Infrastructure;

/// <summary>
/// All writable state lives under %APPDATA%\WorkStationX.
/// Never write next to the .exe — once installed to Program Files that directory
/// is read-only and the app crashes on first write.
/// </summary>
public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WorkStationX");

    public static string DatabaseFile => Path.Combine(RootDirectory, "app.db");

    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(RootDirectory, "logs");

    public static string LogFile => Path.Combine(LogDirectory, "workstationx-.log");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
