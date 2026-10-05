using DualTune.Device;

namespace DualTune.Tests;

public class DualSenseSourceTests
{
    [Fact]
    public void ToInstancePath_ConvertsUsbDevicePath() =>
        Assert.Equal(@"HID\VID_054C&PID_0DF2&MI_03\7&1A2B3C&0&0000",
            DualSenseSource.ToInstancePath(@"\\?\hid#vid_054c&pid_0df2&mi_03#7&1a2b3c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));

    [Fact]
    public void ToInstancePath_ConvertsBluetoothDevicePath() =>
        Assert.Equal(@"HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0DF2\9&2F1E&0&0000",
            DualSenseSource.ToInstancePath(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0df2#9&2f1e&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
}
