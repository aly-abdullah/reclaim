using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Reclaim.Controls;

public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ArcBrushProperty = DependencyProperty.Register(
        nameof(ArcBrush), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush ArcBrush
    {
        get => (Brush)GetValue(ArcBrushProperty);
        set => SetValue(ArcBrushProperty, value);
    }

    public void AnimateTo(double value, double milliseconds = 700)
    {
        var animation = new DoubleAnimation(Math.Clamp(value, 0, 1), TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        double thickness = StrokeThickness;
        if (size <= thickness)
            return;

        double radius = (size - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen(TrackBrush, thickness), center, radius, radius);

        double value = Math.Clamp(Value, 0, 1);
        if (value < 0.001)
            return;

        var pen = new Pen(ArcBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (value > 0.999)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        double angle = value * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0, value > 0.5, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}
