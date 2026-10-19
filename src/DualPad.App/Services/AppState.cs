using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DualPad.Core;
using DualPad.Core.Mapping;
using DualPad.Core.Profiles;

namespace DualPad.App.Services;

/// <summary>Shared application state: profiles, settings and the controller service.</summary>
public sealed partial class AppState : ObservableObject, IDisposable
{
    public static AppState Instance { get; } = new();

    private readonly DispatcherTimer _saveTimer;
    private Profile? _pendingSave;

    public ProfileStore Store { get; } = new();
    public ControllerService Service { get; }
    public ObservableCollection<Profile> Profiles { get; } = [];

    [ObservableProperty] private AppSettings _settings;
    private Profile _activeProfile;
    [ObservableProperty] private ControllerSession? _primarySession;
    [ObservableProperty] private int _controllerCount;
    [ObservableProperty] private bool _isControllerBlocked;
    [ObservableProperty] private bool _mappingEnabled = true;

    /// <summary>Raised on the UI thread when a controller connects or disconnects.</summary>
    public event Action<string, string>? Notify;

    private AppState()
    {
        _settings = Store.LoadSettings();
        foreach (var p in Store.LoadAll()) Profiles.Add(p);
        _activeProfile = Profiles.FirstOrDefault(p => p.Name == _settings.ActiveProfile) ?? Profiles[0];

        Service = new ControllerService(_activeProfile);
        Service.SessionsChanged += () => OnUi(RefreshSessions);
        Service.ControllerConnected += s => OnUi(() =>
        {
            Notify?.Invoke("Controller connesso",
                s.Device.Connection == Core.DualSense.ConnectionType.Bluetooth ? "DualSense collegato via Bluetooth" : "DualSense collegato via USB");
            if (Settings.HideControllerWithHidHide) HideControllers();
        });
        Service.ControllerDisconnected += _ => OnUi(() => Notify?.Invoke("Controller disconnesso", "Il DualSense non è più collegato"));
        Service.LowBattery += (_, level) => OnUi(() => Notify?.Invoke("Batteria scarica", $"Batteria del controller al {level}%"));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => FlushSave();
    }

    public bool IsViGEmInstalled => Service.IsViGEmInstalled;

    public void Start() => Service.Start();

    /// <summary>The profile applied to every controller. Null assignments (from lists being refreshed) are ignored.</summary>
    public Profile ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (value is null || ReferenceEquals(value, _activeProfile)) return;
            _activeProfile = value;
            OnPropertyChanged();
            Service.Profile = value;
            if (Settings.ActiveProfile != value.Name) UpdateSettings(s => s with { ActiveProfile = value.Name });
        }
    }

    partial void OnMappingEnabledChanged(bool value) => Service.MappingEnabled = value;

    /// <summary>Applies a change to the active profile immediately and saves it shortly after.</summary>
    public void UpdateProfile(Func<Profile, Profile> change)
    {
        var old = ActiveProfile;
        var updated = change(old);
        var index = Profiles.IndexOf(old);
        if (index >= 0) Profiles[index] = updated;
        ActiveProfile = updated;
        _pendingSave = updated;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Changes one button of the active profile.</summary>
    public void SetButton(Core.DualSense.DsButtons button, ButtonAction action) =>
        UpdateProfile(p =>
        {
            var buttons = new Dictionary<Core.DualSense.DsButtons, ButtonAction>(p.Buttons) { [button] = action };
            return p with { Buttons = buttons };
        });

    public void FlushSave()
    {
        _saveTimer.Stop();
        if (_pendingSave is null) return;
        Store.Save(_pendingSave);
        _pendingSave = null;
    }

    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        Settings = change(Settings);
        Store.SaveSettings(Settings);
    }

    public Profile CreateProfile(string name, Profile? basedOn = null)
    {
        FlushSave();
        name = UniqueName(name);
        var profile = basedOn is null ? Profile.CreateDefault(name) : basedOn with { Name = name, Buttons = new(basedOn.Buttons) };
        Store.Save(profile);
        Profiles.Add(profile);
        return profile;
    }

    public void RenameProfile(Profile profile, string newName)
    {
        FlushSave();
        newName = newName.Trim();
        if (newName.Length == 0 || newName == profile.Name) return;
        newName = UniqueName(newName);
        Store.Rename(profile.Name, newName);
        var renamed = profile with { Name = newName };
        Profiles[Profiles.IndexOf(profile)] = renamed;
        if (ReferenceEquals(ActiveProfile, profile)) ActiveProfile = renamed;
    }

    public void DeleteProfile(Profile profile)
    {
        if (Profiles.Count <= 1) return;
        FlushSave();
        Store.Delete(profile.Name);
        var wasActive = ReferenceEquals(ActiveProfile, profile);
        Profiles.Remove(profile);
        if (wasActive) ActiveProfile = Profiles[0];
    }

    public Profile? ImportProfile(string file)
    {
        var imported = Store.Import(file);
        if (imported is null) return null;
        var existing = Profiles.FirstOrDefault(p => p.Name == imported.Name);
        if (existing is not null) Profiles[Profiles.IndexOf(existing)] = imported;
        else Profiles.Add(imported);
        return imported;
    }

    public string UniqueName(string name)
    {
        var candidate = name;
        for (var i = 2; Profiles.Any(p => string.Equals(p.Name, candidate, StringComparison.CurrentCultureIgnoreCase)); i++)
            candidate = $"{name} ({i})";
        return candidate;
    }

    /// <summary>Hides connected controllers from games through HidHide (one UAC prompt for controllers not hidden yet).</summary>
    public async void HideControllers()
    {
        if (!Core.Output.HidHideService.IsInstalled) return;
        var ids = Service.Sessions.Select(s => s.Device.InstanceId).Where(id => !Settings.HiddenDevices.Contains(id)).ToList();
        if (ids.Count == 0) return;
        var appPath = Environment.ProcessPath ?? "";
        var ok = await Task.Run(() => Core.Output.HidHideService.Hide(ids, appPath));
        if (ok) UpdateSettings(s => s with { HiddenDevices = [.. s.HiddenDevices.Union(ids)] });
        else Notify?.Invoke("HidHide", "Non è stato possibile nascondere il controller (permesso negato?)");
    }

    /// <summary>Makes every controller DualPad hid visible to games again.</summary>
    public async Task<bool> UnhideControllersAsync()
    {
        var ids = Settings.HiddenDevices.ToList();
        var ok = await Task.Run(() => Core.Output.HidHideService.Unhide(ids));
        if (ok) UpdateSettings(s => s with { HiddenDevices = [] });
        return ok;
    }

    private void RefreshSessions()
    {
        var sessions = Service.Sessions;
        PrimarySession = sessions.FirstOrDefault();
        ControllerCount = sessions.Count;
        IsControllerBlocked = sessions.Count == 0 && Service.Blocked.Count > 0;
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        FlushSave();
        Service.Dispose();
    }
}
