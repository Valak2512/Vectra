namespace DualTune.Core;

/// <summary>Réglages d'une gâchette (spec §6.1).</summary>
public sealed record TriggerSettings
{
    public float InnerDeadzone { get; init; } = 0.02f;
    public float OuterDeadzone { get; init; } = 0.02f;
    public float AntiDeadzone { get; init; }
    public ResponseCurve Curve { get; init; } = ResponseCurve.Linear;
}
