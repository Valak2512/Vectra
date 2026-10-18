namespace DualTune.Core;

/// <summary>Pipeline d'un stick : spec §5.1 à §5.6.</summary>
public static class StickProcessor
{
    private const float Epsilon = 1e-6f;
    private const float MinSpan = 0.05f;

    public static StickState Process(StickState raw, StickSettings s)
    {
        // §5.1 Calibration
        float x = raw.X - s.CenterOffsetX;
        float y = raw.Y - s.CenterOffsetY;

        // §5.2 Zone morte intérieure + extérieure → magnitude m et direction θ
        if (!TryInner(x, y, s, out float m, out float theta))
            return StickState.Zero;

        // Accrochage aux axes (transition douce) : seule la direction change.
        if (s.HorizontalSnapAngle > 0 || s.VerticalSnapAngle > 0) theta = SnapToAxes(theta, s.HorizontalSnapAngle, s.VerticalSnapAngle);

        // §5.3 Courbe, §5.4 Anti-deadzone, §5.5 Sensibilité
        m = s.Curve.Evaluate(m);
        if (m > 0) m = s.AntiDeadzone + (1 - s.AntiDeadzone) * m;
        m = MathF.Min(1f, m * s.Sensitivity);
        if (m <= 0) return StickState.Zero;

        // §5.6 Forme extérieure
        float cos = MathF.Cos(theta), sin = MathF.Sin(theta);
        if (s.OuterShape == OuterShape.Square)
        {
            float k = MathF.Max(MathF.Abs(cos), MathF.Abs(sin));
            cos /= k;
            sin /= k;
        }

        float ox = Math.Clamp(m * cos, -1f, 1f);
        float oy = Math.Clamp(m * sin, -1f, 1f);
        if (s.InvertX) ox = -ox;
        if (s.InvertY) oy = -oy;
        return new StickState(ox, oy);
    }

    /// <summary>
    /// Ramène sur l'axe le plus proche toute direction à moins de son angle d'accrochage (horizontal pour
    /// gauche/droite, vertical pour haut/bas) ; au-delà, l'écart restant est étiré linéairement jusqu'à la
    /// diagonale (45°), qui ne bouge pas. Aucun saut de direction, même avec deux angles différents.
    /// </summary>
    private static float SnapToAxes(float theta, float horizontalDegrees, float verticalDegrees)
    {
        const float quarter = MathF.PI / 2;
        const float half = MathF.PI / 4;

        float k = MathF.Round(theta / quarter);
        float axis = k * quarter;
        bool vertical = ((int)k & 1) != 0; // multiples impairs de 90° : haut / bas
        float snap = MathF.Min(vertical ? verticalDegrees : horizontalDegrees, 44f) * MathF.PI / 180f;
        if (snap <= 0) return theta;

        float d = theta - axis; // écart à l'axe le plus proche, dans [-45°, 45°]
        float abs = MathF.Abs(d);
        if (abs <= snap) return axis;
        return axis + MathF.Sign(d) * (abs - snap) * half / (half - snap);
    }

    public static short ToXbox(float v) => (short)MathF.Round(Math.Clamp(v, -1f, 1f) * 32767f);

    private static bool TryInner(float x, float y, StickSettings s, out float m, out float theta)
    {
        m = 0;
        theta = 0;
        float o = s.OuterDeadzone;

        switch (s.InnerShape)
        {
            case InnerShape.Cross:
                return TryAxial(x, y, s.AxialDeadzone, 1f - o, out m, out theta);

            case InnerShape.Hybrid:
                if (!TryRadial(x, y, s.InnerDeadzone, o, square: false, out m, out theta))
                    return false;
                // Aimantation vers les axes : seule la direction change, la magnitude radiale est conservée.
                return TryAxial(m * MathF.Cos(theta), m * MathF.Sin(theta), s.AxialDeadzone, 1f, out _, out theta);

            default:
                return TryRadial(x, y, s.InnerDeadzone, o, s.InnerShape == InnerShape.Square, out m, out theta);
        }
    }

    private static bool TryRadial(float x, float y, float d, float o, bool square, out float m, out float theta)
    {
        m = 0;
        theta = 0;
        float r = MathF.Sqrt(x * x + y * y);
        if (r < Epsilon) return false;

        theta = MathF.Atan2(y, x);
        float dTheta = square ? d / MathF.Max(MathF.Abs(x), MathF.Abs(y)) * r : d;
        if (r <= dTheta) return false;

        m = Math.Clamp((r - dTheta) / MathF.Max(MinSpan, (1f - o) - dTheta), 0f, 1f);
        return true;
    }

    private static bool TryAxial(float x, float y, float a, float full, out float m, out float theta)
    {
        m = 0;
        theta = 0;
        float span = MathF.Max(MinSpan, 1f - a);
        float ax = MathF.Sign(x) * MathF.Max(0f, MathF.Abs(x) - a) / span;
        float ay = MathF.Sign(y) * MathF.Max(0f, MathF.Abs(y) - a) / span;
        float r = MathF.Sqrt(ax * ax + ay * ay);
        if (r < Epsilon) return false;

        theta = MathF.Atan2(ay, ax);
        m = Math.Clamp(r / MathF.Max(MinSpan, full), 0f, 1f);
        return true;
    }
}
