using System.IO;

namespace DualTune.App.Engine;

/// <summary>Journal texte simple dans %APPDATA%\DualTune\log.txt, rotation à 1 Mo.</summary>
public static class AppLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();
    private static string? _path;

    public static void Init(string dir)
    {
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "log.txt");
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(Exception e) => Write("ERREUR", e.ToString());

    private static void Write(string level, string message)
    {
        if (_path is null) return;
        lock (Gate)
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(_path, Path.Combine(info.DirectoryName!, "log.old.txt"), overwrite: true);
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Le journal ne doit jamais faire planter l'application.
            }
        }
    }
}
