using DualTune.App.Controls;
using DualTune.Core;

namespace DualTune.Tests;

public class CurveEditorTests
{
    [Fact]
    public void DragPoint_NoRoomBetweenNeighbours_ReturnsNull()
    {
        var pts = new[] { new CurvePoint(0, 0), new CurvePoint(0.985f, 0.9f), new CurvePoint(0.995f, 0.95f), new CurvePoint(1, 1) };
        Assert.Null(CurveEditor.DragPoint(pts, 2, 0.5f, 0.5f));
    }

    [Fact]
    public void DragPoint_ClampsBetweenNeighbours()
    {
        var pts = new[] { new CurvePoint(0, 0), new CurvePoint(0.5f, 0.5f), new CurvePoint(1, 1) };
        var moved = CurveEditor.DragPoint(pts, 1, 2f, 0.3f);
        Assert.NotNull(moved);
        Assert.Equal(0.99f, moved!.Value.X, 3);
        Assert.Equal(0.3f, moved.Value.Y, 3);
    }
}
