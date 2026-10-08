using System.Windows;
using System.Windows.Media;

namespace Reclaim.Controls;

public sealed class ActivityChart : FrameworkElement
{
    const double Pad = 6;
    const double EndInset = 8;

    double[] primary = [];
    double[] secondary = [];
    IReadOnlyList<int> marks = [];
    int count;
    int capacity = 2;

    public Color PrimaryColor { get; set; } = Color.FromRgb(0x7C, 0x6C, 0xFF);
    public Color SecondaryColor { get; set; } = Color.FromRgb(0x2B, 0xD4, 0xE8);
    public Color MarkColor { get; set; } = Color.FromRgb(0x3D, 0xDC, 0x97);
    public Color GridColor { get; set; } = Color.FromRgb(0x23, 0x29, 0x36);

    public void Update(double[] primaryValues, double[] secondaryValues, int sampleCount, int sampleCapacity, IReadOnlyList<int> markPositions)
    {
        primary = primaryValues;
        secondary = secondaryValues;
        count = sampleCount;
        capacity = Math.Max(2, sampleCapacity);
        marks = markPositions;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 4 || height < 4)
            return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        var gridPen = new Pen(new SolidColorBrush(GridColor), 1) { DashStyle = new DashStyle([2, 4], 0) };
        gridPen.Freeze();
        for (int i = 0; i <= 4; i++)
        {
            double y = Math.Round(Y(i / 4d, height)) + 0.5;
            dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        if (count < 2)
            return;

        double plotWidth = width - EndInset;
        double step = plotWidth / (capacity - 1);

        var markBrush = new SolidColorBrush(Color.FromArgb(150, MarkColor.R, MarkColor.G, MarkColor.B));
        var markPen = new Pen(markBrush, 1) { DashStyle = new DashStyle([3, 3], 0) };
        foreach (int position in marks)
        {
            double x = Math.Round(X(position, plotWidth, step)) + 0.5;
            dc.DrawLine(markPen, new Point(x, Pad), new Point(x, height - Pad));
            dc.DrawEllipse(new SolidColorBrush(MarkColor), null, new Point(x, Pad), 3, 3);
        }

        DrawSeries(dc, secondary, SecondaryColor, plotWidth, step, height);
        DrawSeries(dc, primary, PrimaryColor, plotWidth, step, height);
    }

    void DrawSeries(DrawingContext dc, double[] values, Color color, double plotWidth, double step, double height)
    {
        var points = new Point[count];
        for (int i = 0; i < count; i++)
            points[i] = new Point(X(i, plotWidth, step), Y(values[i], height));

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo(points[1..], true, true);
        }
        line.Freeze();

        double baseline = height - Pad;
        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(new Point(points[0].X, baseline), true, true);
            context.PolyLineTo(points, true, true);
            context.LineTo(new Point(points[^1].X, baseline), true, true);
        }
        area.Freeze();

        var fill = new LinearGradientBrush(Color.FromArgb(64, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B), 90);
        dc.DrawGeometry(fill, null, area);

        var stroke = new SolidColorBrush(color);
        dc.DrawGeometry(null, new Pen(stroke, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);

        var last = points[^1];
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(56, color.R, color.G, color.B)), null, last, 7, 7);
        dc.DrawEllipse(stroke, null, last, 3.5, 3.5);
    }

    double X(int index, double plotWidth, double step) => plotWidth - (count - 1 - index) * step;

    static double Y(double fraction, double height) => Pad + (1 - Math.Clamp(fraction, 0, 1)) * (height - Pad * 2);
}
