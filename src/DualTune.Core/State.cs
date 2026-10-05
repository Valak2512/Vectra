namespace DualTune.Core;

/// <summary>Position d'un stick, chaque axe dans [-1, 1], Y positif = haut.</summary>
public readonly record struct StickState(float X, float Y)
{
    public static StickState Zero => default;
}

/// <summary>Boutons physiques de la DualSense Edge.</summary>
[Flags]
public enum Buttons : uint
{
    None = 0,
    Cross = 1u << 0,
    Circle = 1u << 1,
    Square = 1u << 2,
    Triangle = 1u << 3,
    L1 = 1u << 4,
    R1 = 1u << 5,
    L2Click = 1u << 6,
    R2Click = 1u << 7,
    L3 = 1u << 8,
    R3 = 1u << 9,
    Create = 1u << 10,
    Options = 1u << 11,
    PS = 1u << 12,
    Touchpad = 1u << 13,
    Mute = 1u << 14,
    DpadUp = 1u << 15,
    DpadDown = 1u << 16,
    DpadLeft = 1u << 17,
    DpadRight = 1u << 18,
    LeftPaddle = 1u << 19,
    RightPaddle = 1u << 20,
    LeftFn = 1u << 21,
    RightFn = 1u << 22,
}

/// <summary>État brut de la manette ; gâchettes dans [0, 1].</summary>
public readonly record struct ControllerState(StickState Left, StickState Right, float L2, float R2, Buttons Buttons)
{
    public static ControllerState Neutral => default;
}

/// <summary>Boutons Xbox 360, valeurs identiques à Nefarius.ViGEm.Client Xbox360Button.</summary>
[Flags]
public enum XboxButtons : ushort
{
    None = 0,
    DpadUp = 0x0001,
    DpadDown = 0x0002,
    DpadLeft = 0x0004,
    DpadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LS = 0x0040,
    RS = 0x0080,
    LB = 0x0100,
    RB = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>État envoyé à la manette Xbox 360 virtuelle.</summary>
public readonly record struct XboxState(short LX, short LY, short RX, short RY, byte LT, byte RT, XboxButtons Buttons)
{
    public static XboxState Neutral => default;
}
