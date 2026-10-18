using System.Text.Json.Serialization;

namespace DualTune.Core;

public enum InnerShape { Round, Square, Cross, Hybrid }

public enum OuterShape { Circle, Square }

/// <summary>Réglages d'un stick (spec §5). Valeurs par défaut du tableau de la spec.</summary>
public sealed record StickSettings
{
    public float CenterOffsetX { get; init; }
    public float CenterOffsetY { get; init; }
    public InnerShape InnerShape { get; init; } = InnerShape.Round;
    public float InnerDeadzone { get; init; } = 0.05f;
    public float AxialDeadzone { get; init; } = 0.05f;
    public float OuterDeadzone { get; init; } = 0.02f;
    public OuterShape OuterShape { get; init; } = OuterShape.Circle;
    /// <summary>Accrochage à l'axe horizontal (gauche / droite), en degrés (0 = désactivé, max 30).</summary>
    public float HorizontalSnapAngle { get; init; }
    /// <summary>Accrochage à l'axe vertical (haut / bas), en degrés (0 = désactivé, max 30).</summary>
    public float VerticalSnapAngle { get; init; }
    /// <summary>Ancien réglage unique (profils d'avant la v1.1) : lu, repris sur les deux axes, jamais réécrit.</summary>
    [JsonPropertyName("AxisSnapAngle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? LegacyAxisSnapAngle { get; init; }
    public ResponseCurve Curve { get; init; } = ResponseCurve.Linear;
    public float AntiDeadzone { get; init; }
    public float Sensitivity { get; init; } = 1f;
    public bool InvertX { get; init; }
    public bool InvertY { get; init; }
}
