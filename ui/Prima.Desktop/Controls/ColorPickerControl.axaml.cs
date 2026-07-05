using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Prima.App;

namespace Prima.Desktop.Controls;

/// <summary>
/// Wraps the third-party Avalonia.Controls.ColorPicker control. This is the
/// only file allowed to reference the underlying library's types — swapping
/// to a different color-picker library later means rewriting this file's
/// internals only; consumers (MainWindow, CanvasControl) only ever see Rgba.
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

    private readonly ColorPicker? _picker;

    public ColorPickerControl()
    {
        InitializeComponent();
        _picker = this.FindControl<ColorPicker>("PART_ColorPicker");
        if (_picker is not null)
            _picker.Color = ToLibraryColor(SelectedColor);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedColorProperty && _picker is not null)
        {
            var libraryColor = ToLibraryColor(SelectedColor);
            if (_picker.Color != libraryColor)
                _picker.Color = libraryColor;
        }
    }

    private void OnLibraryColorChanged(object? sender, ColorChangedEventArgs e)
    {
        var rgba = ToRgba(e.NewColor);
        SelectedColor = rgba;
        ColorChanged?.Invoke(this, rgba);
    }

    private static Color ToLibraryColor(Rgba c) => new(c.A, c.R, c.G, c.B);
    private static Rgba ToRgba(Color c) => new(c.R, c.G, c.B, c.A);
}
