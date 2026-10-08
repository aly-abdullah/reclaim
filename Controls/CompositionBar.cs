using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Reclaim.Controls;

public sealed class CompositionBar : FrameworkElement
{
    const double Gap = 3;
    const double Hidden = 0.002;

    static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress", typeof(double), typeof(CompositionBar),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    double[] from = [];
    double[] to = [];
    Brush[] fills = [];

    public void Update(double[] fractions, Brush[] brushes)
    {
        var current = Interpolated();
        from = current.Length == fractions.Length ? current : fractions;
        to = fractions;
        fills = brushes;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(650))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(ProgressProperty, animation);
    }

    double[] Interpolated()
    {
        double progress = (double)GetValue(ProgressProperty);
        if (from.Length != to.Length)
            return to;
        var values = new double[to.Length];
        for (int i = 0; i < to.Length; i++)
            values[i] = from[i] + (to[i] - from[i]) * progress;
        return values;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        var values = Interpolated();
        if (width <= 0 || height <= 0 || values.Length == 0)
            return;

        double sum = values.Sum();
        if (sum <= 0)
            return;

        int visible = values.Count(v => v / sum > Hidden);
        double usable = width - Gap * Math.Max(0, visible - 1);
        double radius = height / 2;

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height), radius, radius));
        double x = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] / sum <= Hidden)
                continue;
            double segment = usable * values[i] / sum;
            double corner = Math.Min(3, segment / 2);
            dc.DrawRoundedRectangle(fills[i], null, new Rect(x, 0, segment, height), corner, corner);
            x += segment + Gap;
        }
        dc.Pop();
    }
}
