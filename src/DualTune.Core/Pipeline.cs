namespace DualTune.Core;

public static class Pipeline
{
    public static XboxState Process(ControllerState raw, Profile p)
    {
        var left = StickProcessor.Process(raw.Left, p.LeftStick);
        var right = StickProcessor.Process(raw.Right, p.RightStick);
        var (buttons, forceLT, forceRT) = ButtonMapper.Map(raw.Buttons, p.ButtonMap);
        byte lt = forceLT ? (byte)255 : TriggerProcessor.Process(raw.L2, p.L2);
        byte rt = forceRT ? (byte)255 : TriggerProcessor.Process(raw.R2, p.R2);

        return new XboxState(
            StickProcessor.ToXbox(left.X), StickProcessor.ToXbox(left.Y),
            StickProcessor.ToXbox(right.X), StickProcessor.ToXbox(right.Y),
            lt, rt, buttons);
    }
}
