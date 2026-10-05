namespace DualTune.Core;

/// <summary>Un jeu complet de réglages (spec §9). Traité comme immuable : on le remplace, on ne le modifie pas.</summary>
public sealed record Profile
{
    public const int CurrentSchemaVersion = 1;

    public string Name { get; init; } = "Défaut";
    public StickSettings LeftStick { get; init; } = new();
    public StickSettings RightStick { get; init; } = new();
    public TriggerSettings L2 { get; init; } = new();
    public TriggerSettings R2 { get; init; } = new();
    public Dictionary<Buttons, XboxTarget> ButtonMap { get; init; } = new(ButtonMapper.Default);
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public static Profile CreateDefault() => new();
}

/// <param name="Language">Langue de l'interface : "fr", "en", ou null = langue de Windows.</param>
/// <param name="CheckUpdates">Chercher une nouvelle version sur GitHub au démarrage.</param>
public sealed record AppSettings(string LastProfile = "Défaut", bool HideController = true, bool OutputEnabled = true, bool GuideDone = false,
    string? Language = null, bool CheckUpdates = true);
