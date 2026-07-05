using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Prima.Desktop.Controls;

public sealed partial class ColorSliderControl : UserControl
{
    public static readonly StyledProperty<string> LabelTextProperty =
        AvaloniaProperty.Register<ColorSliderControl, string>(nameof(LabelText), "");

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ColorSliderControl, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ColorSliderControl, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ColorSliderControl, double>(nameof(Maximum), 255.0);

    public event EventHandler<double>? ValueChanged;
    public event EventHandler? DragStarted;
    public event EventHandler? DragEnded;

    public string LabelText
    {
        get => GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
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

    private readonly TextBlock? _label;
    private readonly TextBox? _input;
    private readonly Slider? _slider;
    private bool _syncing;

    public ColorSliderControl()
    {
        InitializeComponent();
        _label = this.FindControl<TextBlock>("PART_Label");
        _input = this.FindControl<TextBox>("PART_Input");
        _slider = this.FindControl<Slider>("PART_Slider");

        if (_slider is not null)
        {
            _slider.PointerPressed += OnSliderPointerPressed;
            _slider.PointerReleased += OnSliderPointerReleased;
            _slider.Minimum = Minimum;
            _slider.Maximum = Maximum;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelTextProperty)
        {
            if (_label is not null) _label.Text = LabelText;
        }
        else if (change.Property == ValueProperty)
        {
            if (!_syncing)
                SyncToChildControls();
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            if (_slider is not null)
            {
                _slider.Minimum = Minimum;
                _slider.Maximum = Maximum;
            }
        }
    }

    private void SyncToChildControls()
    {
        _syncing = true;
        try
        {
            double v = Math.Clamp(Value, Minimum, Maximum);
            if (_slider is not null) _slider.Value = v;
            if (_input is not null) _input.Text = FormatValue(v);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        DragStarted?.Invoke(this, EventArgs.Empty);
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        DragEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            double v = Math.Clamp(e.NewValue, Minimum, Maximum);
            SetValue(ValueProperty, v);
            if (_input is not null) _input.Text = FormatValue(v);
            ValueChanged?.Invoke(this, v);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitInput();
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e) => CommitInput();

    private void CommitInput()
    {
        if (_syncing) return;
        string text = _input?.Text ?? "";
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            v = Math.Clamp(v, Minimum, Maximum);
            _syncing = true;
            try
            {
                SetValue(ValueProperty, v);
                if (_slider is not null) _slider.Value = v;
                if (_input is not null) _input.Text = FormatValue(v);
                ValueChanged?.Invoke(this, v);
                DragEnded?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _syncing = false;
            }
        }
        else
        {
            if (_input is not null)
                _input.Text = FormatValue(Value);
        }
    }

    private static string FormatValue(double v) =>
        v.ToString("F0", CultureInfo.InvariantCulture);
}
