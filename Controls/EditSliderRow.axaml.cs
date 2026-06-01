using Avalonia;
using Avalonia.Controls;

namespace Lumen.Controls;

public partial class EditSliderRow : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<EditSliderRow, string>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<EditSliderRow, double>(
            nameof(Value),
            defaultValue: 0d,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<EditSliderRow, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<EditSliderRow, double>(nameof(Maximum), defaultValue: 100d);

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<EditSliderRow, string>(nameof(ValueFormat), "0");

    public static readonly StyledProperty<string> ValueDisplayProperty =
        AvaloniaProperty.Register<EditSliderRow, string>(nameof(ValueDisplay), "0");

    public EditSliderRow() => InitializeComponent();

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public string ValueDisplay
    {
        get => GetValue(ValueDisplayProperty);
        private set => SetValue(ValueDisplayProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && change.NewValue is double v)
            UpdateValueDisplay(v);
        else if (change.Property == ValueFormatProperty)
            UpdateValueDisplay(Value);
    }

    private void UpdateValueDisplay(double v)
    {
        try
        {
            ValueDisplay = v.ToString(ValueFormat);
        }
        catch
        {
            ValueDisplay = v.ToString("0.##");
        }
    }
}
