using DualTune.App.Engine;
using DualTune.Core;
using DualTune.Device;
using DualTune.Output;

namespace DualTune.Tests;

public class InputEngineTests
{
    private sealed class FakeSource : IControllerSource
    {
        public event Action<ControllerState>? StateReceived;
        public event Action<ConnectionKind>? ConnectionChanged;
        public event RawReportHandler? RawReportReceived;
#pragma warning disable CS0067 // jamais déclenché par la fausse source
        public event Action<Exception>? Faulted;
#pragma warning restore CS0067
        public string? DeviceInstancePath => null;
        public ConnectionKind Connection { get; private set; }
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }

        public void Send(ControllerState s) => StateReceived?.Invoke(s);
        public void Raw(byte[] r) => RawReportReceived?.Invoke(r);

        public void Connect(ConnectionKind k)
        {
            Connection = k;
            ConnectionChanged?.Invoke(k);
        }
    }

    private sealed class FakePad : IVirtualPad
    {
        public List<XboxState> Submitted { get; } = new();
        public bool IsAvailable => true;
        public string? Error => null;
        public void Connect() { }
        public void Submit(in XboxState s) => Submitted.Add(s);
        public void Dispose() { }
    }

    private static (InputEngine engine, FakeSource src, FakePad pad) Make()
    {
        var src = new FakeSource();
        var pad = new FakePad();
        var engine = new InputEngine(src, pad) { ActiveProfile = Profile.CreateDefault() };
        engine.Start();
        return (engine, src, pad);
    }

    [Fact]
    public void Engine_ProcessesStateAndSubmits()
    {
        var (engine, src, pad) = Make();
        src.Send(ControllerState.Neutral with { Buttons = Buttons.Cross });
        Assert.Equal(XboxButtons.A, pad.Submitted[^1].Buttons);
        Assert.Equal(Buttons.Cross, engine.Snapshot.Raw.Buttons);
    }

    [Fact]
    public void Engine_OnDisconnect_SubmitsNeutral()
    {
        var (engine, src, pad) = Make();
        src.Connect(ConnectionKind.Usb);
        src.Send(ControllerState.Neutral with { Left = new(1, 0), Buttons = Buttons.Cross });
        src.Connect(ConnectionKind.None);
        Assert.Equal(XboxState.Neutral, pad.Submitted[^1]);
        Assert.Equal(ConnectionKind.None, engine.Snapshot.Connection);
        Assert.Equal(ControllerState.Neutral, engine.Snapshot.Raw);
    }

    [Fact]
    public void Engine_ProfileSwap_TakesEffectNextReport()
    {
        var (engine, src, pad) = Make();
        var p = Profile.CreateDefault();
        engine.ActiveProfile = p with { ButtonMap = new Dictionary<Buttons, XboxTarget> { [Buttons.Cross] = XboxTarget.B } };
        src.Send(ControllerState.Neutral with { Buttons = Buttons.Cross });
        Assert.Equal(XboxButtons.B, pad.Submitted[^1].Buttons);
    }

    [Fact]
    public void Engine_OutputDisabled_SubmitsNeutral()
    {
        var (engine, src, pad) = Make();
        engine.OutputEnabled = false;
        src.Send(ControllerState.Neutral with { Buttons = Buttons.Cross });
        Assert.Equal(XboxState.Neutral, pad.Submitted[^1]);
        Assert.Equal(XboxButtons.A, engine.Snapshot.Output.Buttons); // le visualiseur montre quand même le résultat
    }

    [Fact]
    public void Engine_CaptureRaw_KeepsCopyOfLastReport()
    {
        var (engine, src, _) = Make();
        src.Raw(new byte[] { 1, 2, 3 });
        Assert.Null(engine.Snapshot.LastRawReport);
        engine.CaptureRaw = true;
        var buf = new byte[] { 4, 5, 6 };
        src.Raw(buf);
        buf[0] = 9;
        Assert.Equal(new byte[] { 4, 5, 6 }, engine.Snapshot.LastRawReport);
    }

    private sealed class ThrowingPad : IVirtualPad
    {
        public bool IsAvailable => true;
        public string? Error => null;
        public void Connect() { }
        public void Submit(in XboxState s) => throw new InvalidOperationException("VigemTargetNotPluggedIn");
        public void Dispose() { }
    }

    [Fact]
    public void Engine_PadThrows_OnStateAndDisconnect_DoesNotPropagate()
    {
        var src = new FakeSource();
        var engine = new InputEngine(src, new ThrowingPad());
        src.Send(ControllerState.Neutral with { Buttons = Buttons.Cross });
        src.Connect(ConnectionKind.Usb);
        src.Connect(ConnectionKind.None);
        Assert.Equal(ConnectionKind.None, engine.Snapshot.Connection);
    }

    [Fact]
    public void Engine_ProcessedSticks_AreExposed()
    {
        var (engine, src, _) = Make();
        src.Send(ControllerState.Neutral with { Left = new(0.01f, 0.01f) }); // dans la zone morte par défaut
        Assert.Equal(StickState.Zero, engine.Snapshot.LeftProcessed);
    }
}
