using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Prima.App;

namespace Prima.Desktop.Controls;

/// <summary>
/// Renders a <see cref="SwatchPalette"/> and <see cref="ColorHistory"/> as
/// clickable swatch grids. The models aren't observable, so callers must call
/// <see cref="Refresh"/> after mutating them (e.g. after adding a swatch or
/// recording a pick).
/// </summary>
public sealed partial class SwatchStrip : UserControl
{
    private const double SwatchSize = 18;

    public SwatchPalette Palette { get; set; } = new();
    public ColorHistory History { get; set; } = new();

    /// <summary>Raised when the user clicks a swatch or history entry.</summary>
    public event EventHandler<Rgba>? ColorSelected;

    private readonly WrapPanel? _swatches;
    private readonly WrapPanel? _history;

    public SwatchStrip()
    {
        InitializeComponent();
        _swatches = this.FindControl<WrapPanel>("PART_Swatches");
        _history = this.FindControl<WrapPanel>("PART_History");
    }

    /// <summary>Rebuilds the swatch/history grids from the current model state.</summary>
    public void Refresh()
    {
        Rebuild(_swatches, Palette.Swatches);
        Rebuild(_history, History.Colors);
    }

    private void Rebuild(WrapPanel? panel, IReadOnlyList<Rgba> colors)
    {
        if (panel is null) return;
        panel.Children.Clear();
        foreach (var color in colors)
        {
            var swatch = new Border
            {
                Width = SwatchSize,
                Height = SwatchSize,
                Margin = new Avalonia.Thickness(2),
                Background = new SolidColorBrush(new Color(color.A, color.R, color.G, color.B)),
                BorderBrush = (IBrush?)this.FindResource("PrimaTextBrush"),
                BorderThickness = new Avalonia.Thickness(1),
            };
            swatch.PointerPressed += (_, _) => ColorSelected?.Invoke(this, color);
            panel.Children.Add(swatch);
        }
    }
}
