using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Models;

namespace WorkStationX.Services;

public interface IChromeProfileService
{
    string? ChromeExecutablePath { get; }
    bool IsChromeInstalled { get; }
    IReadOnlyList<ChromeProfile> Discover();
    Task<int> SyncToDatabaseAsync(CancellationToken ct = default);
}

/// <summary>
/// Finds Chrome's profiles by reading its own "Local State" file, so the user picks
/// "Muhit" from a list instead of having to work out that it lives in "Profile 1".
///
/// The mapping is not guessable - directory names are assigned in creation order and
/// have no relation to the display name - which is why hand-mapping was the original
/// plan and why reading the file is worth the effort.
/// </summary>
public class ChromeProfileService : IChromeProfileService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ChromeProfileService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        ChromeExecutablePath = FindChrome();
    }

    public string? ChromeExecutablePath { get; }

    public bool IsChromeInstalled => ChromeExecutablePath is not null;

    private static string LocalStatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google", "Chrome", "User Data", "Local State");

    private static string? FindChrome()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public IReadOnlyList<ChromeProfile> Discover()
    {
        if (!File.Exists(LocalStatePath))
        {
            Log.Information("Chrome Local State not found; no profiles to import");
            return Array.Empty<ChromeProfile>();
        }

        try
        {
            // Chrome may be writing this file; open shared rather than locking it out.
            using var stream = new FileStream(
                LocalStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("profile", out var profile) ||
                !profile.TryGetProperty("info_cache", out var cache))
            {
                return Array.Empty<ChromeProfile>();
            }

            var lastUsed = profile.TryGetProperty("last_used", out var lu)
                ? lu.GetString()
                : "Default";

            var found = new List<ChromeProfile>();
            foreach (var entry in cache.EnumerateObject())
            {
                var directory = entry.Name;

                // "name" is what the user sees in Chrome's profile switcher.
                // gaia_name is the Google account name; fall back to it, then the folder.
                var friendly =
                    Text(entry.Value, "name")
                    ?? Text(entry.Value, "gaia_name")
                    ?? directory;

                found.Add(new ChromeProfile
                {
                    FriendlyName = friendly,
                    ProfileDirectory = directory,
                    IsDefault = string.Equals(directory, lastUsed, StringComparison.OrdinalIgnoreCase)
                });
            }

            return found
                .OrderByDescending(p => p.ProfileDirectory == "Default")
                .ThenBy(p => p.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not read Chrome profiles");
            return Array.Empty<ChromeProfile>();
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() is { Length: > 0 } s ? s : null
            : null;

    /// <summary>
    /// Upserts discovered profiles. Renames in Chrome flow through; profiles deleted in
    /// Chrome are left alone because workspace items may still reference them.
    /// </summary>
    public async Task<int> SyncToDatabaseAsync(CancellationToken ct = default)
    {
        var discovered = Discover();
        if (discovered.Count == 0)
        {
            return 0;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.ChromeProfiles.ToListAsync(ct);
        var changed = 0;

        foreach (var profile in discovered)
        {
            var match = existing.FirstOrDefault(e =>
                e.ProfileDirectory == profile.ProfileDirectory);

            if (match is null)
            {
                db.ChromeProfiles.Add(profile);
                changed++;
            }
            else if (match.FriendlyName != profile.FriendlyName || match.IsDefault != profile.IsDefault)
            {
                match.FriendlyName = profile.FriendlyName;
                match.IsDefault = profile.IsDefault;
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
            Log.Information("Synced {Count} Chrome profile change(s)", changed);
        }

        return changed;
    }
}
