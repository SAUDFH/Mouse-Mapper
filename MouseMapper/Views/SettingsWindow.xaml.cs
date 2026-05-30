using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MouseMapper.Models;
using MouseMapper.Services;
using MouseMapper.ViewModels;
using MouseMapper.Views.Controls;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfBinding = System.Windows.Data.Binding;

namespace MouseMapper.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppConfig _config;
    private CurvePreviewControl? _steeringPreview;
    private CurvePreviewControl? _throttlePreview;

    public event EventHandler<AppConfig>? ConfigSaved;

    public SettingsWindow(AppConfig config, InputMapper mapper)
    {
        InitializeComponent();
        _config = config;
        _vm = new SettingsViewModel(config, mapper);
        _vm.ConfigSaved += (s, cfg) =>
        {
            ConfigSaved?.Invoke(this, cfg);
            Close();
        };
        DataContext = _vm;

        NavList.SelectionChanged += (s, e) => ShowPage(NavList.SelectedIndex);
        NavList.SelectedIndex = 0;
    }

    private void ShowPage(int index)
    {
        PageContent.Content = index switch
        {
            0 => CreateKeyBindingsPage(),
            1 => CreateCurvePage("Steering", _config.Steering, p => _steeringPreview = p),
            2 => CreateCurvePage("Throttle", _config.Throttle, p => _throttlePreview = p),
            3 => CreateOsdPage(),
            4 => CreateAboutPage(),
            _ => null
        };
    }

    private FrameworkElement CreateKeyBindingsPage()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Key Bindings",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfBrushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        });

        stack.Children.Add(CreateLabeledTextBox("Toggle Key:", "ToggleKey"));
        stack.Children.Add(CreateLabeledTextBox("Throttle Reset:", "ThrottleResetButton"));
        stack.Children.Add(CreateLabeledTextBox("Steering Reset:", "SteeringResetKey"));
        stack.Children.Add(CreateLabeledTextBox("Gear Up:", "GearUpKey"));
        stack.Children.Add(CreateLabeledTextBox("Gear Down:", "GearDownKey"));

        stack.Children.Add(new TextBlock
        {
            Text = "Click the field and press the desired key/button to bind it.",
            FontSize = 11,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)),
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return stack;
    }

    private FrameworkElement CreateCurvePage(string title, CurveParameters cp, Action<CurvePreviewControl> setPreview)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = $"{title} Curve",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfBrushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var preview = new CurvePreviewControl();
        Grid.SetRow(preview, 1);
        setPreview(preview);
        grid.Children.Add(preview);

        var sliders = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(sliders, 2);

        string prefix = title == "Steering" ? "Steering" : "Throttle";

        float deadzone = cp.Deadzone;
        float lowSlope = cp.LowSlope;
        float kneePoint = cp.KneePoint;
        float highSlope = cp.HighSlope;
        int smoothingMs = cp.SmoothingMs;
        float antiDeadzone = cp.AntiDeadzone;

        sliders.Children.Add(CreateSlider("Deadzone: {0:F0}%", $"{prefix}Deadzone", 0, 20,
            v => preview.UpdateParameters((float)v / 100, lowSlope, kneePoint, highSlope, smoothingMs, antiDeadzone)));

        sliders.Children.Add(CreateSlider("Low Slope: {0:F1}x", $"{prefix}LowSlope", 0.1f, title == "Steering" ? 1.0f : 3.0f,
            v => { lowSlope = (float)v; preview.UpdateParameters(deadzone, lowSlope, kneePoint, highSlope, smoothingMs, antiDeadzone); }));

        sliders.Children.Add(CreateSlider("Knee Point: {0:F0}%", $"{prefix}KneePoint", 10, 90,
            v => { kneePoint = (float)v / 100; preview.UpdateParameters(deadzone, lowSlope, kneePoint, highSlope, smoothingMs, antiDeadzone); }));

        sliders.Children.Add(CreateSlider("High Slope: {0:F1}x", $"{prefix}HighSlope", title == "Steering" ? 1.0f : 0.1f, title == "Steering" ? 4.0f : 1.0f,
            v => { highSlope = (float)v; preview.UpdateParameters(deadzone, lowSlope, kneePoint, highSlope, smoothingMs, antiDeadzone); }));

        sliders.Children.Add(CreateSlider("Smoothing: {0:F0}ms", $"{prefix}Smoothing", 0, 200,
            v => { smoothingMs = (int)v; preview.UpdateParameters(deadzone, lowSlope, kneePoint, highSlope, smoothingMs, antiDeadzone); }));

        sliders.Children.Add(CreateSlider("Anti-Deadzone: {0:F0}%", $"{prefix}AntiDeadzone", 0, 25,
            v => { antiDeadzone = (float)v / 100; preview.UpdateParameters(deadzone, lowSlope, kneePoint, highSlope, smoothingMs, antiDeadzone); }));

        sliders.Children.Add(CreateSlider("Input Range: {0:F1}x", $"{prefix}InputRange", 0.1f, 3.0f, null));

        grid.Children.Add(sliders);
        return grid;
    }

    private FrameworkElement CreateOsdPage()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "OSD Settings",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfBrushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var cb = new WpfCheckBox
        {
            Content = "Enable OSD Overlay",
            Foreground = WpfBrushes.White
        };
        cb.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, "OsdEnabled");
        stack.Children.Add(cb);

        stack.Children.Add(CreateSlider("Scale: {0:F1}x", "OsdScale", 0.5f, 3.0f, null));

        return stack;
    }

    private FrameworkElement CreateAboutPage()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "About",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfBrushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "MouseMapper v1.0",
            Foreground = WpfBrushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 6)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Maps mouse input to virtual Xbox 360 controller via ViGEm.",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap
        });

        bool driverOk = false;
        try { using var client = new Nefarius.ViGEm.Client.ViGEmClient(); driverOk = true; } catch { }

        stack.Children.Add(new TextBlock
        {
            Text = driverOk ? "ViGEm Driver: CONNECTED" : "ViGEm Driver: NOT FOUND",
            Foreground = driverOk
                ? new SolidColorBrush(WpfColor.FromRgb(0x4E, 0xCD, 0xC4))
                : new SolidColorBrush(WpfColor.FromRgb(0xFF, 0x6B, 0x6B)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        });

        if (!driverOk)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Install ViGEmBus from: https://github.com/nefarius/ViGEmBus/releases",
                FontSize = 11,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }

        return stack;
    }

    private static FrameworkElement CreateLabeledTextBox(string label, string binding)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = WpfBrushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        });
        var tb = new WpfTextBox
        {
            Width = 120,
            Height = 26,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };
        tb.SetBinding(WpfTextBox.TextProperty, binding);
        stack.Children.Add(tb);
        return stack;
    }

    private static FrameworkElement CreateSlider(string format, string binding, double min, double max, Action<double>? onValueChanged)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Foreground = WpfBrushes.White,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 140
        };

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Margin = new Thickness(8, 0, 8, 0)
        };

        var value = new TextBlock
        {
            Foreground = WpfBrushes.White,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 50,
            TextAlignment = TextAlignment.Right
        };

        slider.SetBinding(Slider.ValueProperty, binding);

        // Label shows the name prefix, value shows just the number + unit
        int fmtStart = format.IndexOf("{0");
        string prefix = fmtStart >= 0 ? format[..fmtStart] : format;
        string numFormat = fmtStart >= 0 ? format[fmtStart..] : "{0}";
        label.Text = prefix;

        var valueBinding = new WpfBinding(binding) { StringFormat = numFormat };
        value.SetBinding(TextBlock.TextProperty, valueBinding);

        if (onValueChanged != null)
        {
            slider.ValueChanged += (s, e) => onValueChanged(e.NewValue);
        }

        Grid.SetColumn(label, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(value, 2);
        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(value);

        return grid;
    }
}
