using System.Diagnostics;
using System.Windows.Input;
using DualTune.App.Localization;
using DualTune.App.ViewModels;
using DualTune.Core;
using DualTune.Device;

namespace DualTune.App.Guide;

public enum StepStatus { Todo, Ok, Warn }

/// <summary>État de Vectra utile à l'assistant, fourni par la fenêtre principale.</summary>
/// <param name="ViGEmLoaded">ViGEmBus était présent au lancement de Vectra (sinon il faut relancer Vectra).</param>
/// <param name="HidHideLoaded">HidHide était présent au lancement de Vectra (sinon il faut relancer Vectra).</param>
public readonly record struct GuideContext(ConnectionKind Connection, ControllerState Raw, bool VirtualPadReady, bool HidingActive,
    bool ViGEmLoaded = true, bool HidHideLoaded = true);

public sealed class GuideStep : ViewModelBase
{
    private StepStatus _status;
    private bool _isCurrent;

    private readonly string _key;

    public GuideStep(string key) => _key = key;

    public string Title => Loc.T(_key);

    internal void LanguageChanged() => Raise(nameof(Title));
    public StepStatus Status { get => _status; set => Set(ref _status, value); }
    public bool IsCurrent { get => _isCurrent; set => Set(ref _isCurrent, value); }
}

/// <summary>Assistant de premier lancement : 7 étapes vérifiées en direct (spec §4).</summary>
public sealed class GuideViewModel : ViewModelBase
{
    public const int WelcomeStep = 0, ViGEmStep = 1, HidHideStep = 2, ConflictsStep = 3, ControllerStep = 4, TestStep = 5, DoneStep = 6;

    public const string ViGEmPackage = "ViGEm.ViGEmBus";
    public const string HidHidePackage = "Nefarius.HidHide";
    // Liens directs vers les installeurs (ceux des manifestes winget) : un seul fichier, rien à choisir.
    private const string ViGEmUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";
    private const string HidHideUrl = "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe";
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DropWindow = TimeSpan.FromSeconds(60);

    private readonly ISystemProbe _probe;
    private readonly Func<GuideContext> _context;
    private readonly Func<DateTime> _clock;
    private readonly Action _onDone;
    private readonly Action _restart;
    private readonly InstallGate _gate;
    private int? _errorStep;
    private bool _restarting;
    private bool _canInstall;
    private bool _restartNeeded;
    private readonly List<DateTime> _drops = new();
    private DateTime _lastProbe = DateTime.MinValue;
    private bool _viGEm, _hidHide;
    private IReadOnlyList<string> _conflicts = Array.Empty<string>();
    private ConnectionKind _lastConnection;
    private bool _done;

    private int _current;
    private string? _installErrorKey;
    private bool _showCableTip;
    private bool _sawCross, _sawStick, _virtualOk, _hiddenOk;
    private ConnectionKind _connection;

    /// <param name="gate">Installation en cours partagée entre les ouvertures successives du guide.</param>
    public GuideViewModel(ISystemProbe probe, Func<GuideContext> context, Func<DateTime> clock, Action onDone, Action restart,
        InstallGate? gate = null)
    {
        _gate = gate ?? new InstallGate();
        _gate.Changed += OnGateChanged;
        _probe = probe;
        _context = context;
        _clock = clock;
        _onDone = onDone;
        _restart = restart;
        Steps = new[] { "welcome", "vigem", "hidhide", "conflicts", "controller", "test", "done" }
            .Select(k => new GuideStep("guide.step." + k)).ToList();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Ready = DetectWingetAsync(); // en arrière-plan : la vérification de winget peut prendre plusieurs secondes

        NextCommand = new RelayCommand(() => Current = Math.Min(Current + 1, DoneStep));
        BackCommand = new RelayCommand(() => Current = Math.Max(Current - 1, WelcomeStep));
        FinishCommand = new RelayCommand(() =>
        {
            MarkDone();
            CloseRequested?.Invoke();
        });
        InstallViGEmCommand = new RelayCommand(async () => await InstallAsync(ViGEmStep));
        InstallHidHideCommand = new RelayCommand(async () => await InstallAsync(HidHideStep));
        DownloadViGEmCommand = new RelayCommand(() => Open(ViGEmUrl));
        DownloadHidHideCommand = new RelayCommand(() => Open(HidHideUrl));
        RestartCommand = new RelayCommand(() =>
        {
            _restarting = true; // la fermeture qui suit ne doit pas marquer le guide comme terminé
            _restart();
        });

        Steps[0].IsCurrent = true;
        Refresh();
    }

    /// <summary>La fenêtre doit se fermer (après TERMINER).</summary>
    public event Action? CloseRequested;

    public IReadOnlyList<GuideStep> Steps { get; }

    public int Current
    {
        get => _current;
        set
        {
            if (!Set(ref _current, value)) return;
            for (int i = 0; i < Steps.Count; i++) Steps[i].IsCurrent = i == value;
            Raise(nameof(CurrentStep));
            Raise(nameof(ShowInstalling));
            Raise(nameof(StepError));
            Raise(nameof(NextLabel));
            Raise(nameof(IsLast));
        }
    }

    public GuideStep CurrentStep => Steps[_current];
    public bool IsLast => _current == DoneStep;
    public string NextLabel => Loc.T(CurrentStep.Status == StepStatus.Ok ? "guide.next" : "guide.skip");

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand InstallViGEmCommand { get; }
    public ICommand InstallHidHideCommand { get; }
    public ICommand DownloadViGEmCommand { get; }
    public ICommand DownloadHidHideCommand { get; }
    public ICommand RestartCommand { get; }

    /// <summary>Terminé quand la détection de winget est finie.</summary>
    public Task Ready { get; }

    public bool CanInstall
    {
        get => _canInstall;
        private set
        {
            if (!Set(ref _canInstall, value)) return;
            Raise(nameof(ShowInstallViGEm));
            Raise(nameof(ShowInstallHidHide));
            Raise(nameof(ShowNoWinget));
        }
    }

    public bool ShowInstallViGEm => CanInstall && Steps[ViGEmStep].Status == StepStatus.Todo;
    public bool ShowInstallHidHide => CanInstall && Steps[HidHideStep].Status == StepStatus.Todo;
    public bool ShowNoWinget => Ready.IsCompleted && !CanInstall;

    /// <summary>Un pilote a été installé pendant que Vectra tournait : il faut relancer Vectra.</summary>
    public bool RestartNeeded { get => _restartNeeded; private set => Set(ref _restartNeeded, value); }
    /// <summary>Un pilote s'installe (depuis ce guide ou une ouverture précédente).</summary>
    public bool IsInstalling => _gate.Step is not null;

    /// <summary>Le message « installation en cours » concerne la page affichée.</summary>
    public bool ShowInstalling => _gate.Step == _current;
    public string? InstallError => _installErrorKey is null ? null : Loc.T(_installErrorKey);

    /// <summary>L'erreur d'installation, seulement sur la page du pilote concerné.</summary>
    public string? StepError => _errorStep == _current ? InstallError : null;

    private void SetInstallError(int step, string? key)
    {
        _errorStep = step;
        _installErrorKey = key;
        Raise(nameof(InstallError));
        Raise(nameof(StepError));
    }
    public bool ShowCableTip { get => _showCableTip; private set => Set(ref _showCableTip, value); }
    public string ConflictsText => string.Join(" · ", _conflicts);

    public bool SawCross { get => _sawCross; private set => Set(ref _sawCross, value); }
    public bool SawStick { get => _sawStick; private set => Set(ref _sawStick, value); }
    public bool VirtualOk { get => _virtualOk; private set => Set(ref _virtualOk, value); }
    public bool HiddenOk { get => _hiddenOk; private set => Set(ref _hiddenOk, value); }
    public string ConnectionText => Loc.T(_connection switch
    {
        ConnectionKind.Usb => "guide.connUsb",
        ConnectionKind.Bluetooth => "guide.connBluetooth",
        _ => "guide.connNone",
    });

    /// <summary>~60 fois par seconde : mémorise ce qui a été vu, suit les coupures. N'appelle aucune sonde.</summary>
    public void OnTick(GuideContext ctx)
    {
        if ((ctx.Raw.Buttons & Buttons.Cross) != 0) SawCross = true;
        var l = ctx.Raw.Left;
        if (MathF.Sqrt(l.X * l.X + l.Y * l.Y) >= 0.9f) SawStick = true;
        VirtualOk = ctx.VirtualPadReady;
        HiddenOk = ctx.HidingActive;
        if (_connection != ctx.Connection)
        {
            _connection = ctx.Connection;
            Raise(nameof(ConnectionText));
        }

        bool dropped = _lastConnection != ConnectionKind.None && ctx.Connection == ConnectionKind.None;
        if (dropped || _drops.Count > 0)
        {
            // Astuce câble : au moins 2 coupures dans la dernière minute ; disparaît après une minute stable.
            var now = _clock();
            if (dropped) _drops.Add(now);
            _drops.RemoveAll(t => now - t > DropWindow);
            ShowCableTip = _drops.Count >= 2;
        }
        _lastConnection = ctx.Connection;
    }

    /// <summary>Met à jour les voyants ; les sondes système sont interrogées au plus une fois par seconde.</summary>
    public void Refresh()
    {
        var now = _clock();
        if (now - _lastProbe >= ProbeInterval)
        {
            _lastProbe = now;
            _viGEm = _probe.IsViGEmInstalled();
            _hidHide = _probe.IsHidHideInstalled();
            _conflicts = _probe.RunningConflicts();
            Raise(nameof(ConflictsText));
        }

        var ctx = _context();
        Steps[WelcomeStep].Status = StepStatus.Ok;
        Steps[ViGEmStep].Status = DriverStatus(_viGEm, ctx.ViGEmLoaded);
        Steps[HidHideStep].Status = DriverStatus(_hidHide, ctx.HidHideLoaded);
        RestartNeeded = (_viGEm && !ctx.ViGEmLoaded) || (_hidHide && !ctx.HidHideLoaded);
        Steps[ConflictsStep].Status = _conflicts.Count == 0 ? StepStatus.Ok : StepStatus.Todo;
        Steps[ControllerStep].Status = ctx.Connection == ConnectionKind.None ? StepStatus.Todo
            : ShowCableTip ? StepStatus.Warn : StepStatus.Ok;
        Steps[TestStep].Status = SawCross && SawStick && ctx.VirtualPadReady && ctx.HidingActive ? StepStatus.Ok : StepStatus.Todo;
        Steps[DoneStep].Status = StepStatus.Ok;
        Raise(nameof(NextLabel));
        Raise(nameof(ShowInstallViGEm));
        Raise(nameof(ShowInstallHidHide));
    }

    /// <summary>Installé et chargé = OK ; installé mais Vectra pas encore relancé = attention.</summary>
    private static StepStatus DriverStatus(bool installed, bool loaded) =>
        !installed ? StepStatus.Todo : loaded ? StepStatus.Ok : StepStatus.Warn;

    private async Task DetectWingetAsync()
    {
        CanInstall = await Task.Run(_probe.IsWingetAvailable);
        Raise(nameof(ShowNoWinget));
    }

    /// <summary>Installe le pilote de l'étape via winget, puis revérifie.</summary>
    public async Task InstallAsync(int step)
    {
        // Réservé tout de suite : un second clic (ou un guide rouvert) pendant l'installation ne relance rien.
        if (!_gate.TryEnter(step)) return;
        int code;
        try
        {
            await Ready;
            if (!CanInstall) return;
            SetInstallError(step, null);
            code = await _probe.WingetInstallAsync(step == ViGEmStep ? ViGEmPackage : HidHidePackage);
        }
        finally
        {
            _gate.Exit();
        }
        _lastProbe = DateTime.MinValue;
        Refresh();
        // Jugé sur le résultat réel : winget renvoie un code non nul pour « déjà installé » ou « redémarrage requis ».
        if (Steps[step].Status == StepStatus.Todo)
            SetInstallError(step, code == 0 ? "guide.errNotDetected" : "guide.errInstallFailed");
    }

    /// <summary>
    /// Fenêtre fermée : le guide ne se rouvrira plus tout seul, sauf si les pilotes ne sont pas prêts
    /// (il reprendra au prochain lancement) ou si l'on relance Vectra depuis le guide.
    /// </summary>
    public void Close()
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _gate.Changed -= OnGateChanged;
        if (_restarting) return;
        if (Steps[ViGEmStep].Status == StepStatus.Ok && Steps[HidHideStep].Status == StepStatus.Ok) MarkDone();
    }

    private void OnLanguageChanged()
    {
        foreach (var s in Steps) s.LanguageChanged();
        Raise(nameof(NextLabel));
        Raise(nameof(ConnectionText));
        Raise(nameof(InstallError));
        Raise(nameof(StepError));
    }

    private void OnGateChanged()
    {
        Raise(nameof(IsInstalling));
        Raise(nameof(ShowInstalling));
    }

    private void MarkDone()
    {
        if (_done) return;
        _done = true;
        _onDone();
    }

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
