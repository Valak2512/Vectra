using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DualTune.Core;

namespace DualTune.App.Controls;

/// <summary>
/// Éditeur de courbe : glisser un point, double-clic pour en ajouter un, clic droit pour en supprimer un.
/// Les extrémités (0,0) et (1,1) sont fixes.
/// </summary>
public sealed class CurveEditor : FrameworkElement
{
    private const double Pad = 24;
    private const double HitRadius = 9;
    private const float MinGap = 0.01f;

    public CurveEditor()
    {
        // Le libellé « entrée → sortie » suit la langue.
        Loaded += (_, _) => Localization.Loc.Instance.LanguageChanged += InvalidateVisual;
        Unloaded += (_, _) => Localization.Loc.Instance.LanguageChanged -= InvalidateVisual;
    }

    public static readonly DependencyProperty CurveProperty = DependencyProperty.Register(
        nameof(Curve), typeof(ResponseCurve), typeof(CurveEditor),
        new FrameworkPropertyMetadata(ResponseCurve.Linear,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty CurrentInputProperty = DependencyProperty.Register(
        nameof(CurrentInput), typeof(float), typeof(CurveEditor),
        new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Bg = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0D)));
    private static readonly Pen GridPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))), 1));
    private static readonly Pen FramePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))), 1));
    private static readonly Pen DiagPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))), 1.5) { DashStyle = DashStyles.Dash });
    private static readonly Pen CurvePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xE1, 0x06, 0x00))), 2.5));
    private static readonly Brush PointFill = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)));
    private static readonly Pen MarkerPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x90, 0x2B, 0xD4, 0x6B))), 1.5));
    private static readonly Brush MarkerFill = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0xD4, 0x6B)));
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)));

    private int _dragIndex = -1;

    public ResponseCurve Curve { get => (ResponseCurve)GetValue(CurveProperty); set => SetValue(CurveProperty, value); }
    public float CurrentInput { get => (float)GetValue(CurrentInputProperty); set => SetValue(CurrentInputProperty, value); }

    private double Plot => Math.Max(1, Math.Min(ActualWidth, ActualHeight) - 2 * Pad);

    private Point ToScreen(float x, float y) => new(Pad + x * Plot, Pad + (1 - y) * Plot);

    private (float X, float Y) ToCurve(Point p) =>
        ((float)Math.Clamp((p.X - Pad) / Plot, 0, 1), (float)Math.Clamp(1 - (p.Y - Pad) / Plot, 0, 1));

    protected override Size MeasureOverride(Size available)
    {
        double s = Math.Min(double.IsInfinity(available.Width) ? 400 : available.Width,
                            double.IsInfinity(available.Height) ? 400 : available.Height);
        return new Size(s, s);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        dc.DrawRectangle(Bg, FramePen, new Rect(0.5, 0.5, size - 1, size - 1));

        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            dc.DrawLine(GridPen, ToScreen(t, 0), ToScreen(t, 1));
            dc.DrawLine(GridPen, ToScreen(0, t), ToScreen(1, t));
        }
        dc.DrawLine(DiagPen, ToScreen(0, 0), ToScreen(1, 1));

        var pts = Curve.Points;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(ToScreen(pts[0].X, pts[0].Y), false, false);
            for (int i = 1; i < pts.Count; i++) ctx.LineTo(ToScreen(pts[i].X, pts[i].Y), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, CurvePen, geo);

        for (int i = 0; i < pts.Count; i++)
        {
            bool end = i == 0 || i == pts.Count - 1;
            double half = end ? 2.5 : 4.5;
            var pt = ToScreen(pts[i].X, pts[i].Y);
            dc.DrawRectangle(PointFill, null, new Rect(pt.X - half, pt.Y - half, 2 * half, 2 * half));
        }

        float x = Math.Clamp(CurrentInput, 0, 1);
        float y = Curve.Evaluate(x);
        dc.DrawLine(MarkerPen, ToScreen(x, 0), ToScreen(x, y));
        dc.DrawLine(MarkerPen, ToScreen(0, y), ToScreen(x, y));
        dc.DrawEllipse(MarkerFill, null, ToScreen(x, y), 5, 5);

        var label = new FormattedText(Localization.Loc.F("curves.point", x.ToString("P0"), y.ToString("P0")),System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Consolas"), 12, LabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(label, new Point(Pad, size - Pad + 4));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (e.ClickCount == 2)
        {
            var (x, y) = ToCurve(pos);
            Curve = new ResponseCurve(Curve.Points.Append(new CurvePoint(x, y)));
            e.Handled = true;
            return;
        }

        _dragIndex = HitTestPoint(pos);
        if (_dragIndex >= 0)
        {
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        Cursor = _dragIndex >= 0 || HitTestPoint(pos) >= 0 ? Cursors.Hand : Cursors.Cross;
        if (_dragIndex < 0 || e.LeftButton != MouseButtonState.Pressed) return;

        var pts = Curve.Points.ToArray();
        var (x, y) = ToCurve(pos);
        if (DragPoint(pts, _dragIndex, x, y) is not { } moved) return;
        pts[_dragIndex] = moved;
        Curve = new ResponseCurve(pts);
    }

    /// <summary>Nouvelle position du point <paramref name="i"/> bornée par ses voisins, ou null s'il n'y a pas la place.</summary>
    internal static CurvePoint? DragPoint(IReadOnlyList<CurvePoint> pts, int i, float x, float y)
    {
        float lo = pts[i - 1].X + MinGap, hi = pts[i + 1].X - MinGap;
        if (lo > hi) return null;
        x = Math.Clamp(x, lo, hi);
        y = Math.Clamp(y, pts[i - 1].Y, Math.Max(pts[i - 1].Y, pts[i + 1].Y));
        return new CurvePoint(x, y);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragIndex = -1;
        ReleaseMouseCapture();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        int i = HitTestPoint(e.GetPosition(this));
        if (i < 0) return;
        Curve = new ResponseCurve(Curve.Points.Where((_, k) => k != i));
        e.Handled = true;
    }

    /// <summary>Indice du point intérieur sous le curseur, ou -1 (les extrémités ne se déplacent pas).</summary>
    private int HitTestPoint(Point pos)
    {
        var pts = Curve.Points;
        for (int i = 1; i < pts.Count - 1; i++)
            if ((ToScreen(pts[i].X, pts[i].Y) - pos).Length <= HitRadius) return i;
        return -1;
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
