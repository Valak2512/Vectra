namespace DualTune.Core;

public readonly record struct CurvePoint(float X, float Y);

/// <summary>
/// Courbe de réponse linéaire par morceaux de (0,0) à (1,1), toujours monotone croissante.
/// </summary>
public sealed record ResponseCurve
{
    public IReadOnlyList<CurvePoint> Points { get; }

    public ResponseCurve(IEnumerable<CurvePoint> points) => Points = Sanitize(points);

    public static ResponseCurve Linear { get; } = new(Array.Empty<CurvePoint>());
    public static ResponseCurve SoftExponential { get; } = Sample(m => MathF.Pow(m, 1.5f));
    public static ResponseCurve StrongExponential { get; } = Sample(m => MathF.Pow(m, 2.2f));
    public static ResponseCurve CenterPrecision { get; } = Sample(m => m * m * (3 - 2 * m));
    public static ResponseCurve Responsive { get; } = Sample(m => MathF.Pow(m, 0.7f));

    public static IReadOnlyDictionary<string, ResponseCurve> Presets { get; } = new Dictionary<string, ResponseCurve>
    {
        ["Linéaire"] = Linear,
        ["Exponentielle douce"] = SoftExponential,
        ["Exponentielle forte"] = StrongExponential,
        ["Précision au centre"] = CenterPrecision,
        ["Réactif"] = Responsive,
    };

    public float Evaluate(float m)
    {
        m = Math.Clamp(m, 0f, 1f);
        var p = Points;
        for (int i = 1; i < p.Count; i++)
        {
            if (m <= p[i].X)
            {
                var a = p[i - 1];
                var b = p[i];
                float span = b.X - a.X;
                return span <= 0 ? b.Y : a.Y + (b.Y - a.Y) * (m - a.X) / span;
            }
        }
        return 1f;
    }

    /// <summary>Trie, borne, dédoublonne (dernier gagne), force les extrémités et la monotonie.</summary>
    public static IReadOnlyList<CurvePoint> Sanitize(IEnumerable<CurvePoint> points)
    {
        var byX = new SortedDictionary<float, float>();
        foreach (var pt in points)
        {
            float x = Math.Clamp(pt.X, 0f, 1f);
            if (x <= 0f || x >= 1f || float.IsNaN(pt.X) || float.IsNaN(pt.Y)) continue;
            byX[x] = Math.Clamp(pt.Y, 0f, 1f);
        }

        var result = new List<CurvePoint>(byX.Count + 2) { new(0, 0) };
        foreach (var (x, y) in byX)
            result.Add(new CurvePoint(x, Math.Max(y, result[^1].Y)));
        result.Add(new CurvePoint(1, 1));
        return result;
    }

    public bool Equals(ResponseCurve? other) => other is not null && Points.SequenceEqual(other.Points);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var p in Points) h.Add(p);
        return h.ToHashCode();
    }

    private static ResponseCurve Sample(Func<float, float> f)
    {
        var pts = new CurvePoint[7];
        for (int i = 1; i <= 7; i++)
        {
            float x = i / 8f;
            pts[i - 1] = new CurvePoint(x, f(x));
        }
        return new ResponseCurve(pts); // 7 intérieurs + 2 extrémités = 9 points
    }
}
