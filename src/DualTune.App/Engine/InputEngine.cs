using DualTune.Core;
using DualTune.Device;
using DualTune.Output;

namespace DualTune.App.Engine;

public sealed record EngineSnapshot(
    ControllerState Raw,
    XboxState Output,
    StickState LeftProcessed,
    StickState RightProcessed,
    ConnectionKind Connection,
    byte[]? LastRawReport)
{
    public static EngineSnapshot Empty { get; } = new(ControllerState.Neutral, XboxState.Neutral, StickState.Zero, StickState.Zero, ConnectionKind.None, null);
}

/// <summary>
/// Relie la manette au pipeline et à la manette virtuelle. Tourne sur le thread de lecture ; aucun verrou :
/// le profil et l'instantané sont des références immuables échangées atomiquement.
/// </summary>
public sealed class InputEngine : IDisposable
{
    private readonly IControllerSource _source;
    private readonly IVirtualPad _pad;
    private Profile _profile = Profile.CreateDefault();
    private EngineSnapshot _snapshot = EngineSnapshot.Empty;
    private volatile bool _outputEnabled = true;
    private volatile bool _captureRaw;
    private byte[]? _lastRaw;

    public InputEngine(IControllerSource source, IVirtualPad pad)
    {
        _source = source;
        _pad = pad;
        _source.StateReceived += OnState;
        _source.ConnectionChanged += OnConnection;
        _source.RawReportReceived += OnRaw;
        _source.Faulted += AppLog.Error;
    }

    public event Action<ConnectionKind>? ConnectionChanged;

    public EngineSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public Profile ActiveProfile
    {
        get => Volatile.Read(ref _profile);
        set => Volatile.Write(ref _profile, value);
    }

    public bool OutputEnabled
    {
        get => _outputEnabled;
        set
        {
            _outputEnabled = value;
            if (!value) SubmitSafe(XboxState.Neutral);
        }
    }

    public bool CaptureRaw
    {
        get => _captureRaw;
        set
        {
            _captureRaw = value;
            if (!value) Volatile.Write(ref _lastRaw, null);
        }
    }

    public void Start()
    {
        _pad.Connect();
        SubmitSafe(XboxState.Neutral);
        _source.Start();
    }

    public void Dispose()
    {
        _source.StateReceived -= OnState;
        _source.ConnectionChanged -= OnConnection;
        _source.RawReportReceived -= OnRaw;
        _source.Faulted -= AppLog.Error;
        _source.Dispose();
        _pad.Dispose();
    }

    private void OnState(ControllerState raw)
    {
        try
        {
            var p = ActiveProfile;
            var output = Pipeline.Process(raw, p);
            _pad.Submit(_outputEnabled ? output : XboxState.Neutral);
            Volatile.Write(ref _snapshot, new EngineSnapshot(
                raw, output,
                StickProcessor.Process(raw.Left, p.LeftStick),
                StickProcessor.Process(raw.Right, p.RightStick),
                _source.Connection,
                Volatile.Read(ref _lastRaw)));
        }
        catch (Exception e)
        {
            AppLog.Error(e);
        }
    }

    private void OnConnection(ConnectionKind kind)
    {
        Volatile.Write(ref _snapshot, kind == ConnectionKind.None ? EngineSnapshot.Empty : Snapshot with { Connection = kind });
        AppLog.Info($"Manette : {kind}");

        try
        {
            if (kind == ConnectionKind.None) _pad.Submit(XboxState.Neutral);
            ConnectionChanged?.Invoke(kind);
        }
        catch (Exception e)
        {
            AppLog.Error(e);
        }
    }

    private void SubmitSafe(in XboxState s)
    {
        try
        {
            _pad.Submit(s);
        }
        catch (Exception e)
        {
            AppLog.Error(e);
        }
    }

    private void OnRaw(ReadOnlySpan<byte> report)
    {
        if (!_captureRaw) return;
        var copy = report.ToArray();
        Volatile.Write(ref _lastRaw, copy);
        Volatile.Write(ref _snapshot, Snapshot with { LastRawReport = copy });
    }
}
