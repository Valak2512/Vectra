namespace DualTune.Core;

public static class TriggerProcessor
{
    public static byte Process(float raw, TriggerSettings s)
    {
        float span = MathF.Max(0.05f, (1f - s.OuterDeadzone) - s.InnerDeadzone);
        float t = Math.Clamp((raw - s.InnerDeadzone) / span, 0f, 1f);
        t = s.Curve.Evaluate(t);
        if (t > 0) t = s.AntiDeadzone + (1 - s.AntiDeadzone) * t;
        return (byte)MathF.Round(Math.Clamp(t, 0f, 1f) * 255f);
    }
}
