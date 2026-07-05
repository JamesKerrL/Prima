using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Prima.Desktop.Commands;

public static class ControlHighlighter
{
    public static async Task FlashAsync(Control? control)
    {
        if (control is null) return;

        var adorner = AdornerLayer.GetAdornerLayer(control);

        if (adorner is not null)
        {
            await FlashWithAdornerAsync(control, adorner);
            return;
        }

        var window = FindWindow(control);
        if (window is null) return;

        var layer = window.FindControl<Canvas>("PART_HighlightLayer");
        if (layer is null) return;

        var screenPt = control.PointToScreen(control.Bounds.Position);
        var layerPt = layer.PointToClient(screenPt);

        var highlight = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 76, 141, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 76, 141, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            IsHitTestVisible = false,
            Width = control.Bounds.Width,
            Height = control.Bounds.Height,
        };

        Canvas.SetLeft(highlight, layerPt.X);
        Canvas.SetTop(highlight, layerPt.Y);
        layer.Children.Add(highlight);

        for (int i = 0; i < 6; i++)
        {
            highlight.Opacity = 0.5 + 0.5 * Math.Sin(i * Math.PI / 6);
            await Task.Delay(100);
        }

        layer.Children.Remove(highlight);
    }

    private static async Task FlashWithAdornerAsync(Control control, AdornerLayer adorner)
    {
        var highlight = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 76, 141, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 76, 141, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            IsHitTestVisible = false,
            Width = control.Bounds.Width,
            Height = control.Bounds.Height,
        };

        AdornerLayer.SetAdornedElement(highlight, control);
        AdornerLayer.SetIsClipEnabled(highlight, false);
        adorner.Children.Add(highlight);

        var topLeft = control.TranslatePoint(new Point(0, 0), adorner);
        if (topLeft.HasValue)
        {
            Canvas.SetLeft(highlight, topLeft.Value.X);
            Canvas.SetTop(highlight, topLeft.Value.Y);
        }

        for (int i = 0; i < 6; i++)
        {
            highlight.Opacity = 0.5 + 0.5 * Math.Sin(i * Math.PI / 6);
            await Task.Delay(100);
        }

        adorner.Children.Remove(highlight);
    }

    private static Window? FindWindow(Control control)
    {
        var current = control;
        while (current is not null)
        {
            if (current is Window window)
                return window;
            current = current.Parent as Control;
        }
        return null;
    }
}
