using DualTune.Core;

namespace DualTune.Device;

public enum ConnectionKind { None, Usb, Bluetooth }

/// <summary>Reçoit le rapport HID brut sans copie ; ne pas conserver le span.</summary>
public delegate void RawReportHandler(ReadOnlySpan<byte> report);

public interface IControllerSource : IDisposable
{
    /// <summary>Appelé depuis le thread de lecture.</summary>
    event Action<ControllerState>? StateReceived;

    event Action<ConnectionKind>? ConnectionChanged;

    event RawReportHandler? RawReportReceived;

    /// <summary>Erreur inattendue sur le thread de lecture (déjà rattrapée ; la source retente seule).</summary>
    event Action<Exception>? Faulted;

    /// <summary>Chemin d'instance PnP (format HidHide) de la manette connectée, sinon null.</summary>
    string? DeviceInstancePath { get; }

    ConnectionKind Connection { get; }

    void Start();

    void Stop();
}
