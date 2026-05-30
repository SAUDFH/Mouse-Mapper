using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MouseMapper.Models;
using MouseMapper.Services;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace MouseMapper.Views.Controls;

public partial class CurvePreviewControl : System.Windows.Controls.UserControl
{
    private float _deadzone = 0.03f;
    private float _lowSlope = 0.3f;
    private float _kneePoint = 0.35f;
    private float _highSlope = 2.5f;
    private int _smoothingMs;
    private float _antiDeadzone = 0.24f;

    public CurvePreviewControl()
    {
        InitializeComponent();
    }

    public void UpdateParameters(float deadzone, float lowSlope, float kneePoint, float highSlope, int smoothingMs, float antiDeadzone = 0.24f)
    {
        _deadzone = deadzone;
        _lowSlope = lowSlope;
        _kneePoint = kneePoint;
        _highSlope = highSlope;
        _smoothingMs = smoothingMs;
        _antiDeadzone = antiDeadzone;
        DrawCurve();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCurve();
    }

    private void DrawCurve()
    {
        Canvas.Children.Clear();

        double w = Canvas.ActualWidth;
        double h = Canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double margin = 8;
        double graphW = w - 2 * margin;
        double graphH = h - 2 * margin;

        var gridBrush = new SolidColorBrush(WpfColor.FromRgb(0x33, 0x33, 0x55));
        for (int i = 1; i < 4; i++)
        {
            double y = margin + graphH * i / 4;
            Canvas.Children.Add(new Line { X1 = margin, Y1 = y, X2 = margin + graphW, Y2 = y, Stroke = gridBrush, StrokeThickness = 0.5 });
            double x = margin + graphW * i / 4;
            Canvas.Children.Add(new Line { X1 = x, Y1 = margin, X2 = x, Y2 = margin + graphH, Stroke = gridBrush, StrokeThickness = 0.5 });
        }

        var p = new CurveParameters
        {
            Deadzone = _deadzone,
            LowSlope = _lowSlope,
            KneePoint = _kneePoint,
            HighSlope = _highSlope,
            SmoothingMs = _smoothingMs,
            AntiDeadzone = _antiDeadzone
        };

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(WpfColor.FromRgb(0x4E, 0xCD, 0xC4)),
            StrokeThickness = 2
        };

        float prev = 0f;
        for (int i = 0; i <= 100; i++)
        {
            float input = i / 100f;
            float output = CurveCalculator.Apply(input, p, ref prev, 0f);

            double px = margin + input * graphW;
            double py = margin + graphH - output * graphH;
            polyline.Points.Add(new WpfPoint(px, py));
        }

        Canvas.Children.Add(polyline);
    }
}
