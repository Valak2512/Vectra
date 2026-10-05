using System.Diagnostics;
using Microsoft.Win32;

namespace DualTune.App.Guide;

/// <summary>Ce que l'assistant vérifie sur le PC (spec §3).</summary>
public interface ISystemProbe
{
    bool IsViGEmInstalled();
    bool IsHidHideInstalled();
    IReadOnlyList<string> RunningConflicts();
    bool IsWingetAvailable();
    Task<int> WingetInstallAsync(string packageId);
}

public sealed class SystemProbe : ISystemProbe
{
    /// <summary>Logiciels qui se disputent la manette : nom de processus → nom affiché (ordre = ordre d'affichage).</summary>
    private static readonly (string Process, string Display)[] Conflicts =
    {
        ("DS4Windows", "DS4Windows"),
        ("reWASDEngine", "reWASD"),
        ("reWASDTray", "reWASD"),
        ("DSX", "DSX"),
        ("DualSenseX", "DSX"),
    };

    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);
    private bool? _winget;

    public bool IsViGEmInstalled() => ServiceExists("ViGEmBus");

    public bool IsHidHideInstalled() => ServiceExists("HidHide");

    public IReadOnlyList<string> RunningConflicts()
    {
        var running = new List<string>();
        foreach (var name in Conflicts.Select(c => c.Process).Distinct())
        {
            var found = Process.GetProcessesByName(name);
            if (found.Length > 0) running.Add(name);
            foreach (var p in found) p.Dispose();
        }
        return MatchConflicts(running);
    }

    internal static IReadOnlyList<string> MatchConflicts(IEnumerable<string> processNames)
    {
        var running = new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase);
        return Conflicts.Where(c => running.Contains(c.Process)).Select(c => c.Display).Distinct().ToList();
    }

    public bool IsWingetAvailable()
    {
        if (_winget is { } known) return known;
        try
        {
            using var p = Process.Start(Winget("--version"));
            bool exited = p is not null && p.WaitForExit(5000);
            if (p is not null && !exited) p.Kill(entireProcessTree: true);
            _winget = exited && p!.ExitCode == 0;
        }
        catch (Exception)
        {
            _winget = false;
        }
        return _winget.Value;
    }

    public async Task<int> WingetInstallAsync(string packageId)
    {
        try
        {
            using var p = Process.Start(Winget(
                $"install --id {packageId} -e --silent --accept-package-agreements --accept-source-agreements"));
            if (p is null) return -1;
            using var timeout = new CancellationTokenSource(InstallTimeout);
            try
            {
                await p.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                p.Kill(entireProcessTree: true); // demande Windows ignorée ou winget bloqué
                return -1;
            }
            return p.ExitCode;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static ProcessStartInfo Winget(string args) => new("winget", args)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    private static bool ServiceExists(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
        return key is not null;
    }
}
