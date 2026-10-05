using DualTune.Core;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DualTune.Output;

public interface IVirtualPad : IDisposable
{
    bool IsAvailable { get; }
    string? Error { get; }
    void Connect();
    void Submit(in XboxState s);
}

/// <summary>Manette Xbox 360 virtuelle via ViGEmBus (spec §8).</summary>
public sealed class ViGemPad : IVirtualPad
{
    private static readonly (XboxButtons Flag, Xbox360Button Button)[] ButtonMap =
    {
        (XboxButtons.A, Xbox360Button.A), (XboxButtons.B, Xbox360Button.B),
        (XboxButtons.X, Xbox360Button.X), (XboxButtons.Y, Xbox360Button.Y),
        (XboxButtons.LB, Xbox360Button.LeftShoulder), (XboxButtons.RB, Xbox360Button.RightShoulder),
        (XboxButtons.LS, Xbox360Button.LeftThumb), (XboxButtons.RS, Xbox360Button.RightThumb),
        (XboxButtons.Back, Xbox360Button.Back), (XboxButtons.Start, Xbox360Button.Start),
        (XboxButtons.Guide, Xbox360Button.Guide),
        (XboxButtons.DpadUp, Xbox360Button.Up), (XboxButtons.DpadDown, Xbox360Button.Down),
        (XboxButtons.DpadLeft, Xbox360Button.Left), (XboxButtons.DpadRight, Xbox360Button.Right),
    };

    private ViGEmClient? _client;
    private IXbox360Controller? _pad;

    public ViGemPad()
    {
        try
        {
            _client = new ViGEmClient();
        }
        catch (Exception e)
        {
            Error = $"ViGEmBus introuvable : {e.Message}";
        }
    }

    public bool IsAvailable => _client is not null;
    public string? Error { get; private set; }

    public void Connect()
    {
        if (_client is null || _pad is not null) return;
        try
        {
            _pad = _client.CreateXbox360Controller();
            _pad.AutoSubmitReport = false;
            _pad.Connect();
        }
        catch (Exception e)
        {
            _pad = null;
            Error = $"Création de la manette virtuelle impossible : {e.Message}";
        }
    }

    public void Submit(in XboxState s)
    {
        var pad = _pad;
        if (pad is null) return;
        pad.SetAxisValue(Xbox360Axis.LeftThumbX, s.LX);
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, s.LY);
        pad.SetAxisValue(Xbox360Axis.RightThumbX, s.RX);
        pad.SetAxisValue(Xbox360Axis.RightThumbY, s.RY);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, s.LT);
        pad.SetSliderValue(Xbox360Slider.RightTrigger, s.RT);
        foreach (var (flag, button) in ButtonMap)
            pad.SetButtonState(button, (s.Buttons & flag) != 0);
        pad.SubmitReport();
    }

    public void Dispose()
    {
        try { _pad?.Disconnect(); } catch (Exception) { /* déjà déconnectée */ }
        _pad = null;
        _client?.Dispose();
        _client = null;
    }
}
