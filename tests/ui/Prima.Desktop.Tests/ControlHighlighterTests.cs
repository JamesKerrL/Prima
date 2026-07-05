using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Prima.Desktop.Commands;
using Xunit;

namespace Prima.Desktop.Tests;

public class ControlHighlighterTests
{
    [AvaloniaFact]
    public async Task FlashAsync_WithAdornerLayer_AddsHighlightThenRemovesIt()
    {
        var target = new Button { Width = 40, Height = 20 };
        var window = new Window { Content = target, Width = 200, Height = 200 };
        window.Show();

        var adorner = AdornerLayer.GetAdornerLayer(target);
        Assert.NotNull(adorner);
        Assert.Empty(adorner!.Children);

        var flashTask = ControlHighlighter.FlashAsync(target);

        // give the flash a chance to add its highlight before it completes
        await Task.Delay(30);
        var highlight = Assert.Single(adorner.Children);
        Assert.IsType<Border>(highlight);

        await flashTask;

        Assert.Empty(adorner.Children);
    }

    // No test for the PART_HighlightLayer Canvas fallback branch: every themed
    // Avalonia Window supplies an AdornerLayer to its content via the default
    // Fluent control template (VisualLayerManager), so AdornerLayer.GetAdornerLayer
    // never returns null for a control hosted in a real Window. The fallback is
    // unreachable for every control CommandCatalog currently registers — see
    // CommandRevealTests.CommandCatalog_EveryTarget_ResolvesUnderAnAdornerLayer.

    [AvaloniaFact]
    public async Task FlashAsync_NullControl_IsNoOp()
    {
        await ControlHighlighter.FlashAsync(null);
    }
}
