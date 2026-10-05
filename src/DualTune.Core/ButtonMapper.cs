namespace DualTune.Core;

public static class ButtonMapper
{
    /// <summary>Correspondance par défaut (spec §6.4).</summary>
    public static IReadOnlyDictionary<Buttons, XboxTarget> Default { get; } = new Dictionary<Buttons, XboxTarget>
    {
        [Buttons.Cross] = XboxTarget.A,
        [Buttons.Circle] = XboxTarget.B,
        [Buttons.Square] = XboxTarget.X,
        [Buttons.Triangle] = XboxTarget.Y,
        [Buttons.L1] = XboxTarget.LB,
        [Buttons.R1] = XboxTarget.RB,
        [Buttons.L2Click] = XboxTarget.None,
        [Buttons.R2Click] = XboxTarget.None,
        [Buttons.L3] = XboxTarget.LS,
        [Buttons.R3] = XboxTarget.RS,
        [Buttons.Create] = XboxTarget.Back,
        [Buttons.Options] = XboxTarget.Start,
        [Buttons.PS] = XboxTarget.Guide,
        [Buttons.Touchpad] = XboxTarget.Back,
        [Buttons.Mute] = XboxTarget.None,
        [Buttons.DpadUp] = XboxTarget.DpadUp,
        [Buttons.DpadDown] = XboxTarget.DpadDown,
        [Buttons.DpadLeft] = XboxTarget.DpadLeft,
        [Buttons.DpadRight] = XboxTarget.DpadRight,
        [Buttons.LeftPaddle] = XboxTarget.None,
        [Buttons.RightPaddle] = XboxTarget.None,
        [Buttons.LeftFn] = XboxTarget.None,
        [Buttons.RightFn] = XboxTarget.None,
    };

    public static (XboxButtons buttons, bool forceLT, bool forceRT) Map(
        Buttons pressed, IReadOnlyDictionary<Buttons, XboxTarget> map)
    {
        XboxButtons result = XboxButtons.None;
        bool lt = false, rt = false;
        uint bits = (uint)pressed;
        while (bits != 0)
        {
            uint bit = bits & (~bits + 1);
            bits &= bits - 1;
            if (!map.TryGetValue((Buttons)bit, out var target)) continue;
            switch (target)
            {
                case XboxTarget.LT: lt = true; break;
                case XboxTarget.RT: rt = true; break;
                default: result |= ToXbox(target); break;
            }
        }
        return (result, lt, rt);
    }

    private static XboxButtons ToXbox(XboxTarget t) => t switch
    {
        XboxTarget.A => XboxButtons.A,
        XboxTarget.B => XboxButtons.B,
        XboxTarget.X => XboxButtons.X,
        XboxTarget.Y => XboxButtons.Y,
        XboxTarget.LB => XboxButtons.LB,
        XboxTarget.RB => XboxButtons.RB,
        XboxTarget.LS => XboxButtons.LS,
        XboxTarget.RS => XboxButtons.RS,
        XboxTarget.Back => XboxButtons.Back,
        XboxTarget.Start => XboxButtons.Start,
        XboxTarget.Guide => XboxButtons.Guide,
        XboxTarget.DpadUp => XboxButtons.DpadUp,
        XboxTarget.DpadDown => XboxButtons.DpadDown,
        XboxTarget.DpadLeft => XboxButtons.DpadLeft,
        XboxTarget.DpadRight => XboxButtons.DpadRight,
        _ => XboxButtons.None,
    };
}
