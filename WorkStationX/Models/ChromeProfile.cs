namespace WorkStationX.Models;

/// <summary>
/// A Chrome profile, discovered from
/// %LOCALAPPDATA%\Google\Chrome\User Data\Local State -> profile.info_cache
/// so the user picks from a list instead of hand-mapping "Profile 2".
/// </summary>
public class ChromeProfile
{
    public int Id { get; set; }

    /// <summary>Human-readable name as shown in Chrome, e.g. "Work Google".</summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>Directory name passed to --profile-directory, e.g. "Profile 2".</summary>
    public string ProfileDirectory { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
