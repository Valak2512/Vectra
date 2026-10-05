using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DualTune.App.Controls;

/// <summary>
/// Jauge « compte-tours » : segments inclinés allumés jusqu'à la valeur. Glisser à la souris, molette,
/// flèches du clavier. En lecture seule, sert d'indicateur (gâchettes brut / sortie).
/// </summary>
public sealed class SegmentSlider : RangeBase
{
    private const double Gap = 2;
    private static readonly double SkewTan = Math.Tan(20 * Math.PI / 180);

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(int), typeof(SegmentSlider),
        new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(SegmentSlider),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(SegmentSlider),
        new PropertyMetadata(false, (d, _) => ((SegmentSlider)d).UpdateInteractivity()));

    static SegmentSlider()
    {
        HeightProperty.OverrideMetadata(typeof(SegmentSlider), new FrameworkPropertyMetadata(10.0));
        FocusableProperty.OverrideMetadata(typeof(SegmentSlider), new FrameworkPropertyMetadata(true));
        FocusVisualStyleProperty.OverrideMetadata(typeof(SegmentSlider), new FrameworkPropertyMetadata(null));
    }

    public SegmentSlider()
    {
        SmallChange = 0.01;
        UpdateInteractivity();
        IsKeyboardFocusedChanged += (_, _) => InvalidateVisual();
    }

    public int Segments { get => (int)GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>Valeur correspondant à une abscisse dans le contrôle (bornée, jamais NaN).</summary>
    internal static double ValueFromPosition(double x, double width, double min, double max)
    {
        if (width <= 0 || double.IsNaN(x)) return min;
        return min + Math.Clamp(x / width, 0, 1) * (max - min);
    }

    /// <summary>Pas de Page haut / Page bas : un dixième de la plage (adapté à chaque jauge).</summary>
    internal static double PageStep(double min, double max) => (max - min) / 10;

    /// <summary>Nombre de segments allumés, fractionnaire (le dernier est allumé au prorata).</summary>
    internal static double LitSegments(double value, double min, double max, int count)
    {
        double range = max - min;
        if (range <= 0 || double.IsNaN(value)) return 0;
        return Math.Clamp((value - min) / range, 0, 1) * count;
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        int n = Math.Max(1, Segments);
        if (w <= 0 || h <= 0) return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // zone cliquable pleine
        double slant = h * SkewTan;
        double segW = Math.Max(1, (w - slant - Gap * (n - 1)) / n);
        double lit = LitSegments(Value, Minimum, Maximum, n);

        var on = Accent ?? (TryFindResource("Text") as Brush) ?? Brushes.White;
        var off = (TryFindResource("Off") as Brush) ?? Brushes.DimGray;

        for (int i = 0; i < n; i++)
        {
            double x = i * (segW + Gap);
            var geo = Parallelogram(x, segW, h, slant);
            double fill = Math.Clamp(lit - i, 0, 1);
            dc.DrawGeometry(off, null, geo);
            if (fill <= 0) continue;
            if (fill >= 1)
            {
                dc.DrawGeometry(on, null, geo);
            }
            else
            {
                dc.PushClip(new RectangleGeometry(new Rect(x, 0, slant + segW * fill, h)));
                dc.DrawGeometry(on, null, geo);
                dc.Pop();
            }
        }

        if (IsKeyboardFocused)
            dc.DrawRectangle(TryFindResource("Red") as Brush ?? Brushes.Red, null, new Rect(0, h + 2, w, 1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;
        Focus();
        CaptureMouse();
        SetFromMouse(e);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) SetFromMouse(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>
    /// La molette ne règle la jauge que si elle a le focus (cliquée ou tabulée) : sinon un simple défilement de
    /// page qui passe dessus modifierait le réglage en silence.
    /// </summary>
    internal static bool HandlesWheel(bool isReadOnly, bool isKeyboardFocused) => !isReadOnly && isKeyboardFocused;

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (!HandlesWheel(IsReadOnly, IsKeyboardFocused)) return;
        Value = Math.Clamp(Value + Math.Sign(e.Delta) * SmallChange, Minimum, Maximum);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsReadOnly) return;
        double step = e.Key switch
        {
            Key.Right or Key.Up => SmallChange,
            Key.Left or Key.Down => -SmallChange,
            Key.PageUp => PageStep(Minimum, Maximum),
            Key.PageDown => -PageStep(Minimum, Maximum),
            _ => 0,
        };
        if (step == 0) return;
        Value = Math.Clamp(Value + step, Minimum, Maximum);
        e.Handled = true;
    }

    private void SetFromMouse(MouseEventArgs e)
    {
        double slant = ActualHeight * SkewTan;
        Value = ValueFromPosition(e.GetPosition(this).X - slant / 2, ActualWidth - slant, Minimum, Maximum);
    }

    private void UpdateInteractivity()
    {
        Cursor = IsReadOnly ? Cursors.Arrow : Cursors.Hand;
        Focusable = !IsReadOnly;
    }

    private static StreamGeometry Parallelogram(double x, double width, double height, double slant)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x + slant, 0), true, true);
            c.LineTo(new Point(x + slant + width, 0), false, false);
            c.LineTo(new Point(x + width, height), false, false);
            c.LineTo(new Point(x, height), false, false);
        }
        g.Freeze();
        return g;
    }
}
