using DualTune.Core;
using HidSharp;

namespace DualTune.Device;

/// <summary>Lit la DualSense (Edge) en HID sur un thread dédié, avec reconnexion automatique (spec §7).</summary>
public sealed class DualSenseSource : IControllerSource
{
    private const int BluetoothReportLength = 78;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private Thread? _thread;
    private volatile bool _running;
    private volatile HidStream? _stream;

    public event Action<ControllerState>? StateReceived;
    public event Action<ConnectionKind>? ConnectionChanged;
    public event RawReportHandler? RawReportReceived;
    public event Action<Exception>? Faulted;

    public string? DeviceInstancePath { get; private set; }
    public ConnectionKind Connection { get; private set; }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "DualSense reader" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        try { _stream?.Dispose(); } catch (ObjectDisposedException) { }
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose() => Stop();

    private void Run()
    {
        while (_running)
        {
            try
            {
                var device = Find();
                if (device is null || !device.TryOpen(out HidStream stream))
                {
                    Thread.Sleep(RetryDelay);
                    continue;
                }

                using (stream)
                {
                    _stream = stream;
                    ReadLoop(device, stream);
                }
            }
            catch (Exception e)
            {
                // Le thread de lecture ne doit jamais mourir : on signale, puis on retente.
                Faulted?.Invoke(e);
            }
            finally
            {
                _stream = null;
            }

            SetConnection(ConnectionKind.None, null);
            if (_running) Thread.Sleep(RetryDelay);
        }
    }

    private void ReadLoop(HidDevice device, HidStream stream)
    {
        int length = device.GetMaxInputReportLength();
        var kind = length >= BluetoothReportLength ? ConnectionKind.Bluetooth : ConnectionKind.Usb;
        if (kind == ConnectionKind.Bluetooth) EnableFullBluetoothReports(device, stream);

        stream.ReadTimeout = 1000;
        SetConnection(kind, ToInstancePath(device.DevicePath));
        var buffer = new byte[length];

        while (_running)
        {
            int n;
            try
            {
                n = stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                if (_running) Faulted?.Invoke(new IOException($"Lecture interrompue ({device.DevicePath})", e));
                return;
            }

            var report = new ReadOnlySpan<byte>(buffer, 0, n);
            RawReportReceived?.Invoke(report);
            if (DualSenseReportParser.Parse(report, kind) is { } state)
                StateReceived?.Invoke(state);
        }
    }

    /// <summary>En Bluetooth, lire le rapport de fonctionnalité 0x05 fait passer la manette en rapports complets 0x31.</summary>
    private static void EnableFullBluetoothReports(HidDevice device, HidStream stream)
    {
        try
        {
            var feature = new byte[Math.Max(41, device.GetMaxFeatureReportLength())];
            feature[0] = 0x05;
            stream.GetFeature(feature);
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // Certaines piles Bluetooth refusent ; les rapports réduits 0x01 sont alors ignorés (Parse avec ConnectionKind.Bluetooth).
        }
    }

    private static HidDevice? Find() =>
        DeviceList.Local.GetHidDevices(DualSenseReportParser.VendorId)
            .Where(d => d.ProductID is DualSenseReportParser.EdgeProductId or DualSenseReportParser.DualSenseProductId)
            .OrderByDescending(d => d.ProductID == DualSenseReportParser.EdgeProductId)
            .FirstOrDefault();

    private void SetConnection(ConnectionKind kind, string? instancePath)
    {
        DeviceInstancePath = instancePath;
        if (Connection == kind) return;
        Connection = kind;
        ConnectionChanged?.Invoke(kind);
    }

    /// <summary>\\?\hid#vid_..#inst#{guid} → HID\VID_..\INST (format attendu par HidHide).</summary>
    internal static string ToInstancePath(string devicePath)
    {
        var p = devicePath;
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) p = p[4..];
        int lastHash = p.LastIndexOf('#');
        if (lastHash > 0 && p.IndexOf('{', lastHash) == lastHash + 1) p = p[..lastHash];
        return p.Replace('#', '\\').ToUpperInvariant();
    }
}
