using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Prima.App;

namespace Prima.Desktop.Controls;

/// <summary>
/// Thin color-picker shell used by the rest of the UI. The interactive wheel
/// and color math live below this control; MainWindow only sees Rgba values.
/// </summary>
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

    private readonly Border? _buttonSwatch;
    private readonly TextBlock? _buttonHex;
    private readonly HueRingTriangleControl? _wheel;
    private readonly ColorPreviewSplit? _preview;
    private readonly SwatchStrip? _swatches;
    private readonly LabeledColorField? _hex;
    private readonly LabeledColorField? _r;
    private readonly LabeledColorField? _g;
    private readonly LabeledColorField? _b;
    private readonly LabeledColorField? _h;
    private readonly LabeledColorField? _s;
    private readonly LabeledColorField? _v;

    private bool _syncing;
    private Rgba _previousColor;

    public ColorPickerControl()
    {
        InitializeComponent();

        _buttonSwatch = this.FindControl<Border>("PART_ButtonSwatch");
        _buttonHex = this.FindControl<TextBlock>("PART_ButtonHex");
        _wheel = this.FindControl<HueRingTriangleControl>("PART_Wheel");
        _preview = this.FindControl<ColorPreviewSplit>("PART_Preview");
        _swatches = this.FindControl<SwatchStrip>("PART_Swatches");
        _hex = this.FindControl<LabeledColorField>("PART_Hex");
        _r = this.FindControl<LabeledColorField>("PART_R");
        _g = this.FindControl<LabeledColorField>("PART_G");
        _b = this.FindControl<LabeledColorField>("PART_B");
        _h = this.FindControl<LabeledColorField>("PART_H");
        _s = this.FindControl<LabeledColorField>("PART_S");
        _v = this.FindControl<LabeledColorField>("PART_V");

        if (_wheel is not null)
        {
            _wheel.ColorChanged += OnWheelColorChanged;
            _wheel.DragStarted += OnWheelDragStarted;
            _wheel.DragEnded += OnWheelDragEnded;
        }

        WireField(_hex, OnHexCommitted);
        WireField(_r, _ => CommitRgb());
        WireField(_g, _ => CommitRgb());
        WireField(_b, _ => CommitRgb());
        WireField(_h, _ => CommitHsv());
        WireField(_s, _ => CommitHsv());
        WireField(_v, _ => CommitHsv());

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
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedColorProperty && !_syncing)
            ApplySelectedColor(SelectedColor, resetPrevious: true);
    }

    private static void WireField(LabeledColorField? field, Action<string> handler)
    {
        if (field is not null)
            field.Committed += (_, text) => handler(text);
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

    private void CommitRgb()
    {
        if (!TryReadByte(_r, out byte red) ||
            !TryReadByte(_g, out byte green) ||
            !TryReadByte(_b, out byte blue))
        {
            SyncFields(SelectedColor, CurrentHsv());
            return;
        }

        ApplyUserColor(new Rgba(red, green, blue, SelectedColor.A));
    }

    private void CommitHsv()
    {
        if (!TryReadDouble(_h, out double hue) ||
            !TryReadDouble(_s, out double saturationPercent) ||
            !TryReadDouble(_v, out double valuePercent))
        {
            SyncFields(SelectedColor, CurrentHsv());
            return;
        }

        var hsv = new Hsv(
            NormalizeHue(hue),
            Math.Clamp(saturationPercent / 100.0, 0.0, 1.0),
            Math.Clamp(valuePercent / 100.0, 0.0, 1.0));
        ApplyUserHsv(hsv);
    }

    private void AddCurrentSwatch()
    {
        _palette.Add(SelectedColor);
        _swatches?.Refresh();
    }

    private void ApplyUserColor(Rgba color, bool updateWheel = true)
    {
        ApplyUserHsv(Hsv.FromRgba(color), updateWheel);
    }

    private void ApplyUserHsv(Hsv hsv, bool updateWheel = true)
    {
        var color = hsv.ToRgba(SelectedColor.A);
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
        SetField(_hex, Hsv.ToHex(color));
        SetField(_r, color.R.ToString(CultureInfo.InvariantCulture));
        SetField(_g, color.G.ToString(CultureInfo.InvariantCulture));
        SetField(_b, color.B.ToString(CultureInfo.InvariantCulture));
        SetField(_h, Math.Round(hsv.H).ToString(CultureInfo.InvariantCulture));
        SetField(_s, Math.Round(hsv.S * 100.0).ToString(CultureInfo.InvariantCulture));
        SetField(_v, Math.Round(hsv.V * 100.0).ToString(CultureInfo.InvariantCulture));
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

    private static void SetField(LabeledColorField? field, string text)
    {
        if (field is not null)
            field.Text = text;
    }

    private static bool TryReadByte(LabeledColorField? field, out byte value)
    {
        value = 0;
        return field is not null &&
               byte.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadDouble(LabeledColorField? field, out double value)
    {
        value = 0.0;
        return field is not null &&
               double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static double NormalizeHue(double hue)
    {
        hue %= 360.0;
        if (hue < 0.0) hue += 360.0;
        return hue;
    }

    private static IBrush ToBrush(Rgba c) => new SolidColorBrush(new Color(c.A, c.R, c.G, c.B));
}
