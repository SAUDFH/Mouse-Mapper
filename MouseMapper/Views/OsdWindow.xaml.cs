using MouseMapper.Models;
using MouseMapper.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace MouseMapper.Views;

public partial class OsdWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;
    private const int HTCLIENT = 1;

    private readonly InputMapper _mapper;
    private OsdConfig _config;
    private DispatcherTimer? _timer;
    private bool _pendingActive;
    private bool _isDragging;
    private WpfPoint _dragStartPoint;

    public OsdWindow(InputMapper mapper, OsdConfig config)
    {
        InitializeComponent();
        _mapper = mapper;
        _config = config;

        ApplyConfig(config);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        DragHandle.MouseLeftButtonDown += DragHandle_MouseLeftButtonDown;
        DragHandle.MouseMove += DragHandle_MouseMove;
        DragHandle.MouseLeftButtonUp += DragHandle_MouseLeftButtonUp;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)(lParam.ToInt64() >> 16);
            var pt = PointFromScreen(new WpfPoint(x, y));

            if (pt.Y >= 0 && pt.Y <= 16)
            {
                handled = true;
                return new IntPtr(HTCLIENT);
            }

            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }
        return IntPtr.Zero;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyActiveState(_pendingActive);

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render,
            (s, args) => UpdateDisplay(), Dispatcher);
        _timer.Start();
    }

    public void SetActive(bool value)
    {
        if (IsLoaded)
            ApplyActiveState(value);
        else
            _pendingActive = value;
    }

    private void ApplyActiveState(bool value)
    {
        ActiveDot.Fill = value
            ? new SolidColorBrush(WpfColor.FromRgb(0x4E, 0xCD, 0xC4))
            : new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
        StatusText.Text = value ? "ACTIVE" : "INACTIVE";
        StatusText.Foreground = value
            ? new SolidColorBrush(WpfColor.FromRgb(0x4E, 0xCD, 0xC4))
            : new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
        KeyHint.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateDisplay()
    {
        float steering = _mapper.SteeringOutput;
        float throttle = _mapper.ThrottleOutput;

        var parent = SteeringFill.Parent as FrameworkElement;
        double barWidth = parent != null && parent.ActualWidth > 0
            ? parent.ActualWidth - 92 : 140;
        double centerX = barWidth / 2;

        SteeringFill.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;

        if (steering >= 0)
        {
            double width = Math.Max(0, steering * centerX);
            SteeringFill.Width = width;
            SteeringFill.Margin = new Thickness(centerX, 0, 0, 0);
        }
        else
        {
            double width = Math.Max(0, -steering * centerX);
            SteeringFill.Width = width;
            SteeringFill.Margin = new Thickness(centerX - width, 0, 0, 0);
        }

        SteeringText.Text = $"{(int)(steering * 100)}%";
        ThrottleFill.Width = throttle * barWidth;
        ThrottleFill.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        ThrottleText.Text = $"{(int)(throttle * 100)}%";
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        DragHandle.CaptureMouse();
    }

    private void DragHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;
        var pt = e.GetPosition(this);
        Left += pt.X - _dragStartPoint.X;
        Top += pt.Y - _dragStartPoint.Y;
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        DragHandle.ReleaseMouseCapture();
    }

    private void ApplyConfig(OsdConfig config)
    {
        _config = config;
        var scale = config.Scale;
        Width = 220 * scale;
        Height = 160 * scale;

        var workArea = System.Windows.SystemParameters.WorkArea;
        Left = workArea.Right - Width - 40 + config.PositionX;
        Top = workArea.Bottom - Height - 40 + config.PositionY;
    }

    public void UpdateConfig(OsdConfig config)
    {
        ApplyConfig(config);
    }
}
