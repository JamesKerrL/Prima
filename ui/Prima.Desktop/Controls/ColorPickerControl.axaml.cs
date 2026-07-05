using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Prima.App;

namespace Prima.Desktop.Controls;

public sealed partial class ColorPickerControl : UserControl
{
    public static readonly StyledProperty<Rgba> SelectedColorProperty =
        AvaloniaProperty.Register<ColorPickerControl, Rgba>(
            nameof(SelectedColor), defaultValue: new Rgba(40, 90, 220, 255));

    public event EventHandler<Rgba>? ColorChanged;

    public Rgba SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private readonly ColorHistory _history = new();
    private readonly SwatchPalette _palette = new();
    private bool _syncing;
    private Rgba _previousColor;

    private enum ColorTab { Rgb, Hsv, Hex }
    private ColorTab _currentTab = ColorTab.Rgb;

    private readonly Border? _buttonSwatch;
    private readonly TextBlock? _buttonHex;
    private readonly HueRingTriangleControl? _wheel;
    private readonly ColorPreviewSplit? _preview;
    private readonly SwatchStrip? _swatches;
    private readonly LabeledColorField? _hex;

    private readonly ColorSliderControl? _sliderR;
    private readonly ColorSliderControl? _sliderG;
    private readonly ColorSliderControl? _sliderB;
    private readonly ColorSliderControl? _sliderA;
    private readonly ColorSliderControl? _sliderH;
    private readonly ColorSliderControl? _sliderS;
    private readonly ColorSliderControl? _sliderV;
    private readonly ColorSliderControl? _sliderA2;

    private readonly ToggleButton? _tabRgb;
    private readonly ToggleButton? _tabHsv;
    private readonly ToggleButton? _tabHex;
    private readonly StackPanel? _rgbPanel;
    private readonly StackPanel? _hsvPanel;
    private readonly StackPanel? _hexPanel;

    public ColorPickerControl()
    {
        InitializeComponent();

        _buttonSwatch = this.FindControl<Border>("PART_ButtonSwatch");
        _buttonHex = this.FindControl<TextBlock>("PART_ButtonHex");
        _wheel = this.FindControl<HueRingTriangleControl>("PART_Wheel");
        _preview = this.FindControl<ColorPreviewSplit>("PART_Preview");
        _swatches = this.FindControl<SwatchStrip>("PART_Swatches");
        _hex = this.FindControl<LabeledColorField>("PART_Hex");

        _sliderR = this.FindControl<ColorSliderControl>("PART_SliderR");
        _sliderG = this.FindControl<ColorSliderControl>("PART_SliderG");
        _sliderB = this.FindControl<ColorSliderControl>("PART_SliderB");
        _sliderA = this.FindControl<ColorSliderControl>("PART_SliderA");
        _sliderH = this.FindControl<ColorSliderControl>("PART_SliderH");
        _sliderS = this.FindControl<ColorSliderControl>("PART_SliderS");
        _sliderV = this.FindControl<ColorSliderControl>("PART_SliderV");
        _sliderA2 = this.FindControl<ColorSliderControl>("PART_SliderA2");

        _tabRgb = this.FindControl<ToggleButton>("PART_TabRgb");
        _tabHsv = this.FindControl<ToggleButton>("PART_TabHsv");
        _tabHex = this.FindControl<ToggleButton>("PART_TabHex");
        _rgbPanel = this.FindControl<StackPanel>("PART_RgbPanel");
        _hsvPanel = this.FindControl<StackPanel>("PART_HsvPanel");
        _hexPanel = this.FindControl<StackPanel>("PART_HexPanel");

        if (_wheel is not null)
        {
            _wheel.ColorChanged += OnWheelColorChanged;
            _wheel.DragStarted += OnWheelDragStarted;
            _wheel.DragEnded += OnWheelDragEnded;
        }

        if (_hex is not null)
            _hex.Committed += (_, text) => OnHexCommitted(text);

        WireSlider(_sliderR, _ => CommitRgbFromSliders());
        WireSlider(_sliderG, _ => CommitRgbFromSliders());
        WireSlider(_sliderB, _ => CommitRgbFromSliders());
        WireSlider(_sliderA, _ => CommitRgbFromSliders());
        WireSlider(_sliderH, _ => CommitHsvFromSliders());
        WireSlider(_sliderS, _ => CommitHsvFromSliders());
        WireSlider(_sliderV, _ => CommitHsvFromSliders());
        WireSlider(_sliderA2, _ => CommitHsvFromSliders());

        var addSwatch = this.FindControl<Button>("PART_AddSwatch");
        if (addSwatch is not null)
            addSwatch.Click += (_, _) => AddCurrentSwatch();

        if (_swatches is not null)
        {
            _swatches.Palette = _palette;
            _swatches.History = _history;
            _swatches.ColorSelected += (_, color) => ApplyUserColor(color);
        }

        _previousColor = SelectedColor;
        ApplySelectedColor(SelectedColor, resetPrevious: true);
        ActivateTab(ColorTab.Rgb);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedColorProperty && !_syncing)
            ApplySelectedColor(SelectedColor, resetPrevious: true);
    }

    private void WireSlider(ColorSliderControl? slider, Action<double> handler)
    {
        if (slider is null) return;
        slider.ValueChanged += (_, value) => handler(value);
        slider.DragStarted += OnSliderDragStarted;
        slider.DragEnded += OnSliderDragEnded;
    }

    private void OnSliderDragStarted(object? sender, EventArgs e)
    {
        _previousColor = SelectedColor;
        if (_preview is not null)
            _preview.PreviousColor = _previousColor;
    }

    private void OnSliderDragEnded(object? sender, EventArgs e)
    {
        _history.Record(SelectedColor);
        _swatches?.Refresh();
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender == _tabRgb) ActivateTab(ColorTab.Rgb);
        else if (sender == _tabHsv) ActivateTab(ColorTab.Hsv);
        else if (sender == _tabHex) ActivateTab(ColorTab.Hex);
    }

    private void ActivateTab(ColorTab tab)
    {
        _currentTab = tab;
        if (_tabRgb is not null) _tabRgb.IsChecked = tab == ColorTab.Rgb;
        if (_tabHsv is not null) _tabHsv.IsChecked = tab == ColorTab.Hsv;
        if (_tabHex is not null) _tabHex.IsChecked = tab == ColorTab.Hex;
        if (_rgbPanel is not null) _rgbPanel.IsVisible = tab == ColorTab.Rgb;
        if (_hsvPanel is not null) _hsvPanel.IsVisible = tab == ColorTab.Hsv;
        if (_hexPanel is not null) _hexPanel.IsVisible = tab == ColorTab.Hex;
    }

    private void OnWheelDragStarted(object? sender, EventArgs e)
    {
        _previousColor = SelectedColor;
        if (_preview is not null)
            _preview.PreviousColor = _previousColor;
    }

    private void OnWheelDragEnded(object? sender, EventArgs e)
    {
        _history.Record(SelectedColor);
        _swatches?.Refresh();
    }

    private void OnWheelColorChanged(object? sender, Rgba color)
    {
        if (_syncing) return;
        ApplyUserColor(color, updateWheel: false);
    }

    private void OnHexCommitted(string text)
    {
        if (Hsv.TryParseHex(text, out var color))
            ApplyUserColor(color);
        else
            SyncFields(SelectedColor, CurrentHsv());
    }

    private void CommitRgbFromSliders()
    {
        var color = new Rgba(
            (byte)Math.Clamp(_sliderR?.Value ?? 0, 0, 255),
            (byte)Math.Clamp(_sliderG?.Value ?? 0, 0, 255),
            (byte)Math.Clamp(_sliderB?.Value ?? 0, 0, 255),
            (byte)Math.Clamp(_sliderA?.Value ?? 255, 0, 255));
        ApplyUserHsv(Hsv.FromRgba(color), color.A, updateWheel: true);
    }

    private void CommitHsvFromSliders()
    {
        var hsv = new Hsv(
            NormalizeHue(_sliderH?.Value ?? 0),
            Math.Clamp((_sliderS?.Value ?? 0) / 100.0, 0.0, 1.0),
            Math.Clamp((_sliderV?.Value ?? 0) / 100.0, 0.0, 1.0));
        byte alpha = (byte)Math.Clamp(_sliderA2?.Value ?? 255, 0, 255);
        ApplyUserHsv(hsv, alpha, updateWheel: true);
    }

    private void AddCurrentSwatch()
    {
        _palette.Add(SelectedColor);
        _swatches?.Refresh();
    }

    private void ApplyUserColor(Rgba color, bool updateWheel = true)
    {
        ApplyUserHsv(Hsv.FromRgba(color), updateWheel: updateWheel);
    }

    private void ApplyUserHsv(Hsv hsv, byte? alphaOverride = null, bool updateWheel = true)
    {
        var color = hsv.ToRgba(alphaOverride ?? SelectedColor.A);
        SetSelectedColor(color);
        ApplySelectedColor(color, resetPrevious: false, hsvOverride: hsv, updateWheel: updateWheel);
        ColorChanged?.Invoke(this, color);
    }

    private void ApplySelectedColor(
        Rgba color,
        bool resetPrevious,
        Hsv? hsvOverride = null,
        bool updateWheel = true)
    {
        var hsv = hsvOverride ?? Hsv.FromRgba(color);

        _syncing = true;
        try
        {
            if (updateWheel && _wheel is not null)
                _wheel.Color = hsv;

            if (resetPrevious)
                _previousColor = color;

            if (_preview is not null)
            {
                _preview.PreviousColor = _previousColor;
                _preview.CurrentColor = color;
            }

            if (_buttonSwatch is not null)
                _buttonSwatch.Background = ToBrush(color);
            if (_buttonHex is not null)
                _buttonHex.Text = Hsv.ToHex(color);

            SyncFields(color, hsv);
            _swatches?.Refresh();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncFields(Rgba color, Hsv hsv)
    {
        SetSlider(_sliderR, color.R);
        SetSlider(_sliderG, color.G);
        SetSlider(_sliderB, color.B);
        SetSlider(_sliderA, color.A);
        SetSlider(_sliderH, Math.Round(hsv.H));
        SetSlider(_sliderS, Math.Round(hsv.S * 100.0));
        SetSlider(_sliderV, Math.Round(hsv.V * 100.0));
        SetSlider(_sliderA2, color.A);
        if (_hex is not null)
            _hex.Text = Hsv.ToHex(color);
    }

    private static void SetSlider(ColorSliderControl? slider, double value)
    {
        if (slider is not null)
            slider.Value = value;
    }

    private void SetSelectedColor(Rgba color)
    {
        _syncing = true;
        try
        {
            SelectedColor = color;
        }
        finally
        {
            _syncing = false;
        }
    }

    private Hsv CurrentHsv() => _wheel?.Color ?? Hsv.FromRgba(SelectedColor);

    private static double NormalizeHue(double hue)
    {
        hue %= 360.0;
        if (hue < 0.0) hue += 360.0;
        return hue;
    }

    private static IBrush ToBrush(Rgba c) => new SolidColorBrush(new Color(c.A, c.R, c.G, c.B));
}
