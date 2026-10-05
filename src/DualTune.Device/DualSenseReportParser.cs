using DualTune.Core;

namespace DualTune.Device;

/// <summary>Décode un rapport d'entrée DualSense / DualSense Edge (spec §7).</summary>
public static class DualSenseReportParser
{
    public const int VendorId = 0x054C;
    public const int EdgeProductId = 0x0DF2;
    public const int DualSenseProductId = 0x0CE6;

    private const int UsbLength = 64;
    private const int BluetoothLength = 78;

    private static readonly Buttons[] Dpad =
    {
        Buttons.DpadUp,
        Buttons.DpadUp | Buttons.DpadRight,
        Buttons.DpadRight,
        Buttons.DpadDown | Buttons.DpadRight,
        Buttons.DpadDown,
        Buttons.DpadDown | Buttons.DpadLeft,
        Buttons.DpadLeft,
        Buttons.DpadUp | Buttons.DpadLeft,
    };

    /// <summary>
    /// Décode en tenant compte du transport : en Bluetooth, Windows complète les rapports réduits 0x01 à 78 octets,
    /// ils ressembleraient donc à un rapport USB — seuls les rapports complets 0x31 sont acceptés.
    /// </summary>
    public static ControllerState? Parse(ReadOnlySpan<byte> report, ConnectionKind connection) => connection switch
    {
        ConnectionKind.Usb => IsUsb(report) ? Decode(report, 0) : null,
        ConnectionKind.Bluetooth => IsBluetooth(report) ? Decode(report, 1) : null,
        _ => Parse(report),
    };

    /// <summary>Décode en devinant le transport d'après l'ID et la taille du rapport.</summary>
    public static ControllerState? Parse(ReadOnlySpan<byte> report) =>
        IsUsb(report) ? Decode(report, 0) : IsBluetooth(report) ? Decode(report, 1) : null;

    private static bool IsUsb(ReadOnlySpan<byte> r) => r.Length >= UsbLength && r[0] == 0x01;

    private static bool IsBluetooth(ReadOnlySpan<byte> r) => r.Length >= BluetoothLength && r[0] == 0x31;

    private static ControllerState Decode(ReadOnlySpan<byte> report, int o)
    {
        var left = new StickState(Axis(report[o + 1]), -Axis(report[o + 2]));
        var right = new StickState(Axis(report[o + 3]), -Axis(report[o + 4]));
        float l2 = report[o + 5] / 255f;
        float r2 = report[o + 6] / 255f;

        byte b0 = report[o + 8], b1 = report[o + 9], b2 = report[o + 10];
        var buttons = Buttons.None;

        int hat = b0 & 0x0F;
        if (hat < 8) buttons |= Dpad[hat];
        if ((b0 & 0x10) != 0) buttons |= Buttons.Square;
        if ((b0 & 0x20) != 0) buttons |= Buttons.Cross;
        if ((b0 & 0x40) != 0) buttons |= Buttons.Circle;
        if ((b0 & 0x80) != 0) buttons |= Buttons.Triangle;

        if ((b1 & 0x01) != 0) buttons |= Buttons.L1;
        if ((b1 & 0x02) != 0) buttons |= Buttons.R1;
        if ((b1 & 0x04) != 0) buttons |= Buttons.L2Click;
        if ((b1 & 0x08) != 0) buttons |= Buttons.R2Click;
        if ((b1 & 0x10) != 0) buttons |= Buttons.Create;
        if ((b1 & 0x20) != 0) buttons |= Buttons.Options;
        if ((b1 & 0x40) != 0) buttons |= Buttons.L3;
        if ((b1 & 0x80) != 0) buttons |= Buttons.R3;

        if ((b2 & 0x01) != 0) buttons |= Buttons.PS;
        if ((b2 & 0x02) != 0) buttons |= Buttons.Touchpad;
        if ((b2 & 0x04) != 0) buttons |= Buttons.Mute;
        if ((b2 & 0x10) != 0) buttons |= Buttons.LeftFn;
        if ((b2 & 0x20) != 0) buttons |= Buttons.RightFn;
        if ((b2 & 0x40) != 0) buttons |= Buttons.LeftPaddle;
        if ((b2 & 0x80) != 0) buttons |= Buttons.RightPaddle;

        return new ControllerState(left, right, l2, r2, buttons);
    }

    private static float Axis(byte b) => Math.Clamp((b - 128) / 127.5f, -1f, 1f);
}
