using System.Windows;
using System.Windows.Media;
using DualTune.Core;

namespace DualTune.App.Controls;

/// <summary>
/// Dessine le stick : cadre, forme de zone morte intérieure, contour de sortie (cercle/carré),
/// seuil extérieur, point brut (gris), point traité (accent) et sa traînée.
/// </summary>
public sealed class StickVisualizer : FrameworkElement
{
    private const int TrailLength = 12;

    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings), typeof(StickSettings), typeof(StickVisualizer),
        new FrameworkPropertyMetadata(new StickSettings(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawProperty = DependencyProperty.Register(
        nameof(Raw), typeof(StickState), typeof(StickVisualizer),
        new FrameworkPropertyMetadata(default(StickState), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProcessedProperty = DependencyProperty.Register(
        nameof(Processed), typeof(StickState), typeof(StickVisualizer),
        new FrameworkPropertyMetadata(default(StickState), FrameworkPropertyMetadataOptions.AffectsRender, OnProcessedChanged));

    // Palette course (spec §4) : cadre Line, contour Muted, zone morte rouge, cônes verts, sortie blanche.
    private static readonly Brush FrameFill = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0D)));
    private static readonly Pen FramePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))), 1));
    private static readonly Pen GridPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))), 1));
    private static readonly Pen GatePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A))), 1.2));
    private static readonly Pen OuterPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))), 1) { DashStyle = DashStyles.Dash });
    private static readonly Brush DeadFill = Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0xE1, 0x06, 0x00)));
    private static readonly Pen DeadPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xE1, 0x06, 0x00))), 1));
    private static readonly Brush SnapFill = Freeze(new SolidColorBrush(Color.FromArgb(0x15, 0x2B, 0xD4, 0x6B)));
    private static readonly Brush RawBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)));
    private static readonly Color AccentColor = Color.FromRgb(0xF2, 0xF2, 0xF2);
    private static readonly Brush AccentBrush = Freeze(new SolidColorBrush(AccentColor));

    private readonly Queue<StickState> _trail = new();

    public StickSettings Settings { get => (StickSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
    public StickState Raw { get => (StickState)GetValue(RawProperty); set => SetValue(RawProperty, value); }
    public StickState Processed { get => (StickState)GetValue(ProcessedProperty); set => SetValue(ProcessedProperty, value); }

    protected override Size MeasureOverride(Size available)
    {
        double s = Math.Min(double.IsInfinity(available.Width) ? 260 : available.Width,
                            double.IsInfinity(available.Height) ? 260 : available.Height);
        return new Size(s, s);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        double r = size / 2 - 10;
        var c = new Point(size / 2, size / 2);
        Point Map(float x, float y) => new(c.X + x * r, c.Y - y * r);

        var s = Settings;
        dc.DrawRectangle(FrameFill, FramePen, new Rect(0.5, 0.5, size - 1, size - 1));
        dc.DrawLine(GridPen, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        dc.DrawLine(GridPen, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));

        // Contour de sortie : cercle ou carré.
        if (s.OuterShape == OuterShape.Square) dc.DrawRectangle(null, GatePen, new Rect(c.X - r, c.Y - r, 2 * r, 2 * r));
        else dc.DrawEllipse(null, GatePen, c, r, r);

        // Cônes d'accrochage aux axes.
        for (int k = 0; k < 4; k++)
        {
            float angle = k % 2 == 0 ? s.HorizontalSnapAngle : s.VerticalSnapAngle; // droite, haut, gauche, bas
            if (angle > 0) DrawWedge(dc, c, r, k * Math.PI / 2, Math.Min(angle, 44) * Math.PI / 180);
        }

        // Seuil extérieur (au-delà, la sortie est à 100 %).
        double outer = (1 - s.OuterDeadzone) * r;
        if (s.OuterDeadzone > 0) dc.DrawEllipse(null, OuterPen, c, outer, outer);

        // Zone morte intérieure.
        double d = s.InnerDeadzone * r;
        double a = s.AxialDeadzone * r;
        switch (s.InnerShape)
        {
            case InnerShape.Round:
                dc.DrawEllipse(DeadFill, DeadPen, c, d, d);
                break;
            case InnerShape.Square:
                dc.DrawRectangle(DeadFill, DeadPen, new Rect(c.X - d, c.Y - d, 2 * d, 2 * d));
                break;
            case InnerShape.Cross:
                DrawCross(dc, c, r, a);
                break;
            case InnerShape.Hybrid:
                DrawCross(dc, c, r, a);
                dc.DrawEllipse(DeadFill, DeadPen, c, d, d);
                break;
        }

        // Traînée du point traité.
        int i = 0;
        foreach (var p in _trail)
        {
            byte alpha = (byte)(20 + 120 * ++i / TrailLength);
            var b = new SolidColorBrush(Color.FromArgb(alpha, AccentColor.R, AccentColor.G, AccentColor.B));
            dc.DrawEllipse(b, null, Map(p.X, p.Y), 3, 3);
        }

        dc.DrawEllipse(RawBrush, null, Map(Raw.X, Raw.Y), 5, 5);
        dc.DrawEllipse(AccentBrush, null, Map(Processed.X, Processed.Y), 6, 6);
    }

    private static void DrawWedge(DrawingContext dc, Point c, double r, double axis, double half)
    {
        // Angles mathématiques (Y vers le haut) → écran (Y vers le bas).
        Point At(double a) => new(c.X + r * Math.Cos(a), c.Y - r * Math.Sin(a));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(c, true, true);
            ctx.LineTo(At(axis - half), false, false);
            ctx.ArcTo(At(axis + half), new Size(r, r), 0, false, SweepDirection.Counterclockwise, false, false);
        }
        geo.Freeze();
        dc.DrawGeometry(SnapFill, null, geo);
    }

    private static void DrawCross(DrawingContext dc, Point c, double r, double a)
    {
        if (a <= 0) return;
        dc.DrawRectangle(DeadFill, null, new Rect(c.X - r, c.Y - a, 2 * r, 2 * a));
        dc.DrawRectangle(DeadFill, null, new Rect(c.X - a, c.Y - r, 2 * a, 2 * r));
    }

    private static void OnProcessedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var v = (StickVisualizer)d;
        v._trail.Enqueue((StickState)e.NewValue);
        while (v._trail.Count > TrailLength) v._trail.Dequeue();
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
