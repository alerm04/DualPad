using System.Text.Json;
using System.Text.Json.Serialization;
using DualPad.Core.Mapping;

namespace DualPad.Core.Profiles;

/// <summary>Application-wide preferences.</summary>
public sealed record AppSettings
{
    public string ActiveProfile { get; init; } = "Xbox";
    public bool StartWithWindows { get; init; }
    public bool StartMinimized { get; init; }
    public bool MinimizeToTray { get; init; } = true;
    public bool HideControllerWithHidHide { get; init; }
    /// <summary>Device instance ids already handed to HidHide, so we only ask for elevation once per controller.</summary>
    public List<string> HiddenDevices { get; init; } = [];
    public bool Notifications { get; init; } = true;
    /// <summary>Turn off Bluetooth controllers when DualPad exits.</summary>
    public bool PowerOffOnExit { get; init; } = true;
    /// <summary>Run DualPad at high process priority so input is handled promptly under load.</summary>
    public bool HighPriority { get; init; } = true;
    /// <summary>"System", "Dark" or "Light".</summary>
    public string Theme { get; init; } = "System";
    /// <summary>UI language code such as "en" or "pt-BR"; empty follows Windows.</summary>
    public string Language { get; init; } = "";
}

/// <summary>Stores profiles as JSON files under %AppData%\DualPad.</summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _profilesDir;
    private readonly string _settingsPath;

    public ProfileStore(string? root = null)
    {
        root ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DualPad");
        _profilesDir = Path.Combine(root, "Profiles");
        _settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(_profilesDir);
    }

    public string ProfilesDirectory => _profilesDir;

    public IReadOnlyList<Profile> LoadAll()
    {
        var profiles = new List<Profile>();
        foreach (var file in Directory.EnumerateFiles(_profilesDir, "*.json"))
        {
            var profile = TryRead(file);
            if (profile is not null) profiles.Add(profile);
        }

        if (profiles.Count == 0)
        {
            var standard = Profile.CreateDefault();
            Save(standard);
            profiles.Add(standard);
        }

        return profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void Save(Profile profile)
    {
        var path = PathFor(profile.Name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, Json));
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    public void Rename(string oldName, string newName)
    {
        var profile = TryRead(PathFor(oldName));
        if (profile is null) return;
        Save(profile with { Name = newName });
        if (!string.Equals(PathFor(oldName), PathFor(newName), StringComparison.OrdinalIgnoreCase)) Delete(oldName);
    }

    public Profile? Import(string file)
    {
        var profile = TryRead(file);
        if (profile is null) return null;
        Save(profile);
        return profile;
    }

    public void Export(Profile profile, string file) => File.WriteAllText(file, JsonSerializer.Serialize(profile, Json));

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), Json) ?? new AppSettings();
        }
        catch (JsonException)
        {
        }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings) =>
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, Json));

    public static string Serialize(Profile profile) => JsonSerializer.Serialize(profile, Json);
    public static Profile? Deserialize(string json) => Normalize(JsonSerializer.Deserialize<Profile>(json, Json));

    private string PathFor(string name)
    {
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (safe.Length == 0) safe = "Profile";
        return Path.Combine(_profilesDir, safe + ".json");
    }

    private static Profile? TryRead(string file)
    {
        try
        {
            return Deserialize(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Fills controls missing from older files with the Xbox defaults.</summary>
    private static Profile? Normalize(Profile? profile)
    {
        if (profile is null) return null;
        var buttons = Profile.XboxLayout();
        foreach (var (button, action) in profile.Buttons) buttons[button] = action;
        return profile with { Buttons = buttons };
    }
}
