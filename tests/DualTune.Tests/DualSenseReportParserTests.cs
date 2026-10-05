using DualTune.Core;
using DualTune.Device;

namespace DualTune.Tests;

public class DualSenseReportParserTests
{
    private static byte[] MakeUsb(Action<byte[]>? edit = null)
    {
        var r = new byte[64];
        r[0] = 0x01;
        r[1] = r[2] = r[3] = r[4] = 128;
        r[8] = 0x08; // D-pad relâché
        edit?.Invoke(r);
        return r;
    }

    private static byte[] MakeBt(Action<byte[]>? edit = null)
    {
        var r = new byte[78];
        r[0] = 0x31;
        r[2] = r[3] = r[4] = r[5] = 128;
        r[9] = 0x08;
        edit?.Invoke(r);
        return r;
    }

    private static ControllerState Parse(byte[] r) => DualSenseReportParser.Parse(r) ?? throw new Xunit.Sdk.XunitException("null");

    [Fact]
    public void Parse_UsbNeutral_IsNeutral()
    {
        var s = Parse(MakeUsb());
        Assert.InRange(s.Left.X, -0.01f, 0.01f);
        Assert.InRange(s.Right.Y, -0.01f, 0.01f);
        Assert.Equal(Buttons.None, s.Buttons);
    }

    [Fact]
    public void Parse_UsbSticks_NormalizesAndInvertsY()
    {
        var s = Parse(MakeUsb(r => { r[1] = 255; r[2] = 0; r[4] = 255; }));
        Assert.InRange(s.Left.X, 0.99f, 1f);
        Assert.InRange(s.Left.Y, 0.99f, 1f);
        Assert.InRange(s.Right.Y, -1f, -0.99f);
    }

    [Fact]
    public void Parse_UsbTriggers() => Assert.Equal(1f, Parse(MakeUsb(r => r[5] = 255)).L2, 3);

    [Fact]
    public void Parse_UsbFaceButtons() => Assert.Equal(Buttons.Cross, Parse(MakeUsb(r => r[8] = 0x28)).Buttons);

    [Fact]
    public void Parse_UsbDpadDiagonal() =>
        Assert.Equal(Buttons.DpadUp | Buttons.DpadRight, Parse(MakeUsb(r => r[8] = 0x01)).Buttons);

    [Fact]
    public void Parse_UsbShoulderAndSystemButtons() =>
        Assert.Equal(Buttons.L1 | Buttons.R3 | Buttons.PS | Buttons.Mute,
            Parse(MakeUsb(r => { r[9] = 0x81; r[10] = 0x05; })).Buttons);

    [Fact]
    public void Parse_UsbEdgeButtons() =>
        Assert.Equal(Buttons.LeftFn | Buttons.RightFn | Buttons.LeftPaddle | Buttons.RightPaddle,
            Parse(MakeUsb(r => r[10] = 0xF0)).Buttons);

    [Fact]
    public void Parse_BluetoothReport_ShiftsByOne()
    {
        var s = Parse(MakeBt(r => { r[2] = 255; r[9] = 0x28; }));
        Assert.InRange(s.Left.X, 0.99f, 1f);
        Assert.Equal(Buttons.Cross, s.Buttons);
    }

    [Fact]
    public void Parse_OnBluetooth_PaddedReducedReport_ReturnsNull()
    {
        // Windows complète les rapports à la taille max (78 en Bluetooth) : un rapport réduit 0x01 arrive sur 78 octets.
        var padded = new byte[78];
        padded[0] = 0x01;
        padded[8] = 0x08;
        Assert.Null(DualSenseReportParser.Parse(padded, ConnectionKind.Bluetooth));
        Assert.NotNull(DualSenseReportParser.Parse(MakeBt(), ConnectionKind.Bluetooth));
    }

    [Fact]
    public void Parse_OnUsb_BluetoothReport_ReturnsNull() =>
        Assert.Null(DualSenseReportParser.Parse(MakeBt(), ConnectionKind.Usb));

    [Fact]
    public void Parse_ShortReport_ReturnsNull()
    {
        Assert.Null(DualSenseReportParser.Parse(new byte[10]));
        var shortBt = new byte[10];
        shortBt[0] = 0x01; // rapport Bluetooth réduit
        Assert.Null(DualSenseReportParser.Parse(shortBt));
        Assert.Null(DualSenseReportParser.Parse(ReadOnlySpan<byte>.Empty));
    }
}
