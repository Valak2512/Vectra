using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using DualTune.App.Engine;
using DualTune.App.Guide;
using DualTune.App.Localization;
using DualTune.Core;
using DualTune.Device;
using DualTune.Output;

namespace DualTune.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly InputEngine _engine;
    private readonly ProfileStore _store;
    private readonly IVirtualPad _pad;
    private readonly IDeviceHider _hider;
    private readonly IControllerSource _source;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _saveTimer;
    private AppSettings _settings;
    private int _tick;
    private readonly ISystemProbe _probe = new SystemProbe();
    private GuideViewModel? _guide;
    private readonly InstallGate _installGate = new(); // survit aux fermetures du guide
    private readonly bool _viGEmLoaded;   // pilotes présents au lancement : un pilote installé ensuite
    private readonly bool _hidHideLoaded; // n'est utilisable qu'après une relance de Vectra
    private int _guideTick;
    private string? _viGEmBanner;   // bannières retraduites au changement de langue
    private string? _hidHideBanner;

    private ConnectionKind _connection = (ConnectionKind)(-1);
    private string _controllerStatus = "";
    private bool _controllerOk;
    private string _hidHideStatus = "";
    private bool _hidHideOk;
    private bool _captureMode;
    private string? _updateVersion;
    private string _rawHex = "";

    public MainViewModel(InputEngine engine, ProfileStore store, IVirtualPad pad, IDeviceHider hider, IControllerSource source)
    {
        _engine = engine;
        _store = store;
        _pad = pad;
        _hider = hider;
        _viGEmLoaded = pad.IsAvailable;
        _hidHideLoaded = hider.IsAvailable;
        _source = source;
        _settings = store.LoadSettings();
        Loc.Instance.Language = Loc.Resolve(_settings.Language, CultureInfo.CurrentUICulture);

        var profiles = store.LoadAll(out var errors);
        foreach (var err in errors) Banners.Add(err);
        _engine.ActiveProfile = profiles.FirstOrDefault(p => p.Name == _settings.LastProfile) ?? profiles[0];
        _engine.OutputEnabled = _settings.OutputEnabled;
        Profiles = new ProfilesViewModel(store, profiles, _engine.ActiveProfile.Name, ActivateProfile,
            Views.NameDialog.Ask, Views.NameDialog.Confirm, Views.NameDialog.PickSavePath, Views.NameDialog.PickOpenPath);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => FlushSave();

        if (!pad.IsAvailable)
        {
            _viGEmBanner = Loc.T("banner.noVigem");
            Banners.Add(_viGEmBanner);
        }

        LeftStick = new StickViewModel(() => _engine.ActiveProfile, UpdateProfile, isLeft: true);
        RightStick = new StickViewModel(() => _engine.ActiveProfile, UpdateProfile, isLeft: false);
        Tick += LeftStick.OnTick;
        Tick += RightStick.OnTick;
        Sticks = new SticksPageViewModel(LeftStick, RightStick);
        Curves = new CurvesViewModel(() => _engine.ActiveProfile, UpdateProfile);
        Tick += Curves.OnTick;
        L2 = new TriggerViewModel(() => _engine.ActiveProfile, UpdateProfile, isL2: true);
        R2 = new TriggerViewModel(() => _engine.ActiveProfile, UpdateProfile, isL2: false);
        Buttons = new ButtonsViewModel(() => _engine.ActiveProfile, UpdateProfile);
        Tick += L2.OnTick;
        Tick += R2.OnTick;
        Tick += Buttons.OnTick;

        _engine.ConnectionChanged += kind => Application.Current?.Dispatcher.BeginInvoke(() => OnConnectionChanged(kind));
        UpdateHidHideStatus();
        Loc.Instance.LanguageChanged += OnLanguageChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        if (_settings.CheckUpdates) _ = CheckForUpdateAsync();
    }

    /// <summary>Déclenché ~60 fois par seconde avec le dernier état, pour les onglets.</summary>
    public event Action<EngineSnapshot>? Tick;

    /// <summary>Déclenché après toute modification du profil actif.</summary>
    public event Action<Profile>? ProfileChanged;

    public ObservableCollection<string> Banners { get; } = new();

    public StickViewModel LeftStick { get; }
    public StickViewModel RightStick { get; }
    public SticksPageViewModel Sticks { get; }
    public CurvesViewModel Curves { get; }
    public ProfilesViewModel Profiles { get; }
    public TriggerViewModel L2 { get; }
    public TriggerViewModel R2 { get; }
    public ButtonsViewModel Buttons { get; }

    public Profile ActiveProfile => _engine.ActiveProfile;

    /// <summary>Applique une modification au profil actif et la pousse immédiatement au moteur.</summary>
    public void UpdateProfile(Func<Profile, Profile> change)
    {
        var updated = change(_engine.ActiveProfile);
        _engine.ActiveProfile = updated;
        Profiles.OnActiveChanged(updated);
        ProfileChanged?.Invoke(updated);
        _saveTimer.Stop();
        _saveTimer.Start(); // sauvegarde 500 ms après la dernière modification
    }

    private void ActivateProfile(Profile p)
    {
        FlushSave();
        _engine.ActiveProfile = p;
        SaveSettings(_settings with { LastProfile = p.Name });
        LeftStick.Refresh();
        RightStick.Refresh();
        Curves.Refresh();
        L2.Refresh();
        R2.Refresh();
        Buttons.Refresh();
    }

    private void FlushSave()
    {
        if (!_saveTimer.IsEnabled) return;
        _saveTimer.Stop();
        try
        {
            _store.Save(_engine.ActiveProfile);
        }
        catch (IOException e)
        {
            AppLog.Error(e);
            Banners.Add(Loc.F("banner.saveFailed", e.Message));
        }
    }

    public string ControllerStatus { get => _controllerStatus; private set => Set(ref _controllerStatus, value); }
    public bool ControllerOk { get => _controllerOk; private set => Set(ref _controllerOk, value); }
    public string ViGEmStatus => Loc.T(_pad.IsAvailable ? "status.vigemReady" : "status.vigemMissing");
    public bool ViGEmOk => _pad.IsAvailable;
    public string HidHideStatus { get => _hidHideStatus; private set => Set(ref _hidHideStatus, value); }
    public bool HidHideOk { get => _hidHideOk; private set => Set(ref _hidHideOk, value); }

    public bool OutputEnabled
    {
        get => _settings.OutputEnabled;
        set
        {
            _engine.OutputEnabled = value;
            SaveSettings(_settings with { OutputEnabled = value });
            Raise();
        }
    }

    public bool HideController
    {
        get => _settings.HideController;
        set
        {
            SaveSettings(_settings with { HideController = value });
            if (value) ApplyHiding();
            else _hider.Unhide();
            UpdateHidHideStatus();
            Raise();
        }
    }

    public bool CaptureMode
    {
        get => _captureMode;
        set
        {
            if (!Set(ref _captureMode, value)) return;
            _engine.CaptureRaw = value;
            if (!value) RawHex = "";
        }
    }

    public string RawHex { get => _rawHex; private set => Set(ref _rawHex, value); }

    public string AppVersion => AppInfo.VersionText;

    public bool CheckUpdates
    {
        get => _settings.CheckUpdates;
        set
        {
            SaveSettings(_settings with { CheckUpdates = value });
            Raise();
            if (value && _updateVersion is null) _ = CheckForUpdateAsync();
        }
    }

    /// <summary>Numéro de la version plus récente publiée sur GitHub, ou null.</summary>
    public string? UpdateVersion
    {
        get => _updateVersion;
        private set
        {
            if (!Set(ref _updateVersion, value)) return;
            Raise(nameof(UpdateAvailable));
        }
    }

    public bool UpdateAvailable => _updateVersion is not null;

    private ICommand? _openUrl;
    /// <summary>Ouvre une adresse web (paramètre : « repo », « release », « license » ou une URL).</summary>
    public ICommand OpenUrlCommand => _openUrl ??= new RelayCommand(p => OpenUrl((p as string) switch
    {
        "repo" => AppInfo.RepoUrl,
        "release" => AppInfo.ReleasesUrl,
        "license" => AppInfo.RepoUrl + "/blob/main/LICENSE",
        "notices" => AppInfo.RepoUrl + "/blob/main/THIRD-PARTY-NOTICES.md",
        var other => other,
    }));

    private static void OpenUrl(string? url)
    {
        if (url is null || !url.StartsWith("https://", StringComparison.Ordinal)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async Task CheckForUpdateAsync()
    {
        var newer = await UpdateChecker.CheckAsync(AppInfo.Version);
        if (newer is not null) UpdateVersion = $"{newer.Major}.{newer.Minor}.{newer.Build}";
    }

    /// <summary>L'assistant doit s'ouvrir au démarrage (jamais terminé ni fermé).</summary>
    public bool ShouldShowGuide => !_settings.GuideDone;

    /// <summary>La fenêtre principale ouvre (ou ramène au premier plan) la fenêtre de l'assistant.</summary>
    public event Action<GuideViewModel>? GuideRequested;

    private ICommand? _openGuide;
    public ICommand OpenGuideCommand => _openGuide ??= new RelayCommand(OpenGuide);

    public void OpenGuide()
    {
        _guide ??= new GuideViewModel(_probe, GuideContextNow, () => DateTime.Now,
            () => SaveSettings(_settings with { GuideDone = true }), RestartApp, _installGate);
        GuideRequested?.Invoke(_guide);
    }

    public void GuideClosed() => _guide = null;

    private GuideContext GuideContextNow()
    {
        var snap = _engine.Snapshot;
        return new GuideContext(snap.Connection, snap.Raw,
            VirtualPadReady: _pad.IsAvailable && _pad.Error is null && _settings.OutputEnabled,
            HidingActive: snap.Connection != ConnectionKind.None && _hider.IsHiding,
            ViGEmLoaded: _viGEmLoaded,
            HidHideLoaded: _hidHideLoaded);
    }

    /// <summary>Relance Vectra (après installation d'un pilote) : nouvelle instance 2 s après la fermeture de celle-ci.</summary>
    private static void RestartApp()
    {
        if (Environment.ProcessPath is not { } exe) return;
        string script = $"Start-Sleep -Seconds 2; Start-Process -FilePath '{exe.Replace("'", "''")}'";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell",
            $"-NoProfile -WindowStyle Hidden -Command \"{script}\"") { UseShellExecute = false, CreateNoWindow = true });
        Application.Current.Shutdown();
    }

    public void Shutdown()
    {
        _timer.Stop();
        FlushSave();
        _hider.Unhide();
        _engine.Dispose();
    }

    private void OnTick()
    {
        var snap = _engine.Snapshot;
        if (snap.Connection != _connection)
        {
            _connection = snap.Connection;
            ControllerOk = _connection != ConnectionKind.None;
            ControllerStatus = Loc.T(_connection switch
            {
                ConnectionKind.Usb => "status.padUsb",
                ConnectionKind.Bluetooth => "status.padBluetooth",
                _ => "status.padMissing",
            });
        }

        if (_captureMode && ++_tick % 6 == 0 && snap.LastRawReport is { } raw)
            RawHex = FormatHex(raw);

        Tick?.Invoke(snap);

        if (_guide is { } guide)
        {
            guide.OnTick(GuideContextNow());
            if (++_guideTick % 30 == 0) guide.Refresh(); // ~2 fois par seconde ; les sondes restent limitées à 1/s
        }
    }

    private void OnConnectionChanged(ConnectionKind kind)
    {
        if (kind != ConnectionKind.None) ApplyHiding();
    }

    private void ApplyHiding()
    {
        if (!_settings.HideController || _source.DeviceInstancePath is not { } path || Environment.ProcessPath is not { } exe)
            return;
        _hider.Hide(path, exe);
        UpdateHidHideStatus();
    }

    private void UpdateHidHideStatus()
    {
        bool available = _hider.IsAvailable;
        HidHideOk = available && _hider.Error is null;
        HidHideStatus = Loc.T(!available ? "status.hideMissing"
            : !_settings.HideController ? "status.hideOff"
            : _hider.Error is null ? "status.hideOn"
            : "status.hideError");

        if (_hidHideBanner is not null) Banners.Remove(_hidHideBanner);
        _hidHideBanner = null;
        if (_settings.HideController && (!available || _hider.Error is not null))
        {
            _hidHideBanner = Loc.F("banner.twoPads", available ? _hider.Error : Loc.T("banner.noHidHide"));
            Banners.Add(_hidHideBanner);
        }
    }

    /// <summary>Retraduit les textes composés ici et mémorise le choix.</summary>
    private void OnLanguageChanged()
    {
        _connection = (ConnectionKind)(-1); // statut de la manette recalculé au prochain tick
        Raise(nameof(ViGEmStatus));
        UpdateHidHideStatus();
        if (_viGEmBanner is not null)
        {
            int i = Banners.IndexOf(_viGEmBanner);
            _viGEmBanner = Loc.T("banner.noVigem");
            if (i >= 0) Banners[i] = _viGEmBanner;
        }
        if (_settings.Language != Loc.Instance.Language) SaveSettings(_settings with { Language = Loc.Instance.Language });
    }

    private void SaveSettings(AppSettings s)
    {
        _settings = s;
        _store.SaveSettings(s);
    }

    internal static string FormatHex(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            sb.Append(i.ToString("D2")).Append(" : ");
            for (int j = i; j < Math.Min(i + 16, data.Length); j++)
                sb.Append(data[j].ToString("X2")).Append(' ');
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
