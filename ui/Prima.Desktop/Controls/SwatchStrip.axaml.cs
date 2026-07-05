using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Prima.App;

namespace Prima.Desktop.Controls;

public sealed partial class SwatchStrip : UserControl
{
    private const double SwatchSize = 18;

    public SwatchPalette Palette { get; set; } = new();
    public ColorHistory History { get; set; } = new();

    public event EventHandler<Rgba>? ColorSelected;
    public event EventHandler? SaveRequested;
    public event EventHandler? LoadRequested;
    public event EventHandler<string>? OpenRecentRequested;

    public IReadOnlyList<string> RecentPaths { get; set; } = Array.Empty<string>();

    private readonly WrapPanel? _swatches;
    private readonly WrapPanel? _history;

    public SwatchStrip()
    {
        InitializeComponent();
        _swatches = this.FindControl<WrapPanel>("PART_Swatches");
        _history = this.FindControl<WrapPanel>("PART_History");
    }

    private void OnPaletteMenuClick(object? sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();

        var saveItem = new MenuItem { Header = "Save" };
        saveItem.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        flyout.Items.Add(saveItem);

        var loadItem = new MenuItem { Header = "Load" };
        loadItem.Click += (_, _) => LoadRequested?.Invoke(this, EventArgs.Empty);
        flyout.Items.Add(loadItem);

        if (RecentPaths.Count > 0)
        {
            flyout.Items.Add(new Separator());
            foreach (var path in RecentPaths)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var item = new MenuItem { Header = name };
                item.Click += (_, _) => OpenRecentRequested?.Invoke(this, path);
                flyout.Items.Add(item);
            }
        }

        flyout.ShowAt((Control)sender!);
    }

    public void Refresh()
    {
        Rebuild(_swatches, Palette.Swatches, showDelete: true);
        Rebuild(_history, History.Colors, showDelete: false);
    }

    private void Rebuild(WrapPanel? panel, IReadOnlyList<Rgba> colors, bool showDelete)
    {
        if (panel is null) return;
        panel.Children.Clear();
        foreach (var color in colors)
        {
            if (showDelete)
            {
                var container = new Grid();
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

                var deleteBtn = new Border
                {
                    Width = 10,
                    Height = 10,
                    Background = new SolidColorBrush(Colors.Black, 0.5),
                    CornerRadius = new Avalonia.CornerRadius(5),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Margin = new Avalonia.Thickness(0, -2, -2, 0),
                    Child = new TextBlock
                    {
                        Text = "\u00d7",
                        FontSize = 8,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    },
                };
                deleteBtn.PointerPressed += (_, e) =>
                {
                    Palette.Remove(color);
                    Refresh();
                    e.Handled = true;
                };

                container.Children.Add(swatch);
                container.Children.Add(deleteBtn);
                panel.Children.Add(container);
            }
            else
            {
                var container = new Grid();
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

                var addBtn = new Border
                {
                    Width = 10,
                    Height = 10,
                    Background = new SolidColorBrush(Colors.Black, 0.5),
                    CornerRadius = new Avalonia.CornerRadius(5),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Margin = new Avalonia.Thickness(0, -2, -2, 0),
                    Child = new TextBlock
                    {
                        Text = "+",
                        FontSize = 8,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    },
                };
                addBtn.PointerPressed += (_, e) =>
                {
                    Palette.Add(color);
                    Refresh();
                    e.Handled = true;
                };

                container.Children.Add(swatch);
                container.Children.Add(addBtn);
                panel.Children.Add(container);
            }
        }
    }
}
