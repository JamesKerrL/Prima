using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Prima.App.Commands;
using Prima.Desktop.Commands;
using Xunit;

namespace Prima.Desktop.Tests;

/// <summary>
/// Exercises the reveal-and-locate half of command search end to end: given a
/// real MainWindow, does choosing a command actually find the right control
/// (and, for menu items, open the containing menu first) so ControlHighlighter
/// has something real to flash?
/// </summary>
public class CommandRevealTests
{
    [AvaloniaFact]
    public void LocateMenuItem_FindsNestedMenuItemByHeaderPath()
    {
        var window = new MainWindow();

        var open = window.LocateMenuItem("_File", "_Open...");
        Assert.NotNull(open);

        var exportPng = window.LocateMenuItem("_File", "_Export", "As _PNG...");
        Assert.NotNull(exportPng);
    }

    [AvaloniaFact]
    public void LocateMenuItem_UnknownHeaderPath_ReturnsNull()
    {
        var window = new MainWindow();

        Assert.Null(window.LocateMenuItem("_File", "_DoesNotExist"));
        Assert.Null(window.LocateMenuItem("_NoSuchMenu"));
    }

    [AvaloniaFact]
    public async Task RevealMenuAsync_OpensTheNamedTopLevelMenu()
    {
        var window = new MainWindow();
        window.Show();

        var fileMenuItem = window.LocateMenuItem("_File");
        Assert.NotNull(fileMenuItem);
        Assert.False(fileMenuItem!.IsSubMenuOpen);

        await window.RevealMenuAsync("_File");

        Assert.True(fileMenuItem.IsSubMenuOpen);
    }

    [AvaloniaFact]
    public void CommandCatalog_RegistersATargetForEveryCommand()
    {
        var window = new MainWindow();
        var registry = new CommandRegistry();
        var targets = new CommandTargetRegistry();

        CommandCatalog.Populate(window, registry, targets);

        foreach (var command in registry.All)
        {
            var target = targets.Get(command.Id);
            Assert.True(target is not null, $"no CommandTarget registered for '{command.Id}'");
        }
    }

    [AvaloniaFact]
    public async Task CommandCatalog_MenuCommand_RevealsMenuAndLocatesFlashableMenuItem()
    {
        var window = new MainWindow();
        window.Show();
        var registry = new CommandRegistry();
        var targets = new CommandTargetRegistry();
        CommandCatalog.Populate(window, registry, targets);

        var target = targets.Get("file.open");
        Assert.NotNull(target);

        var control = target!.Locate();
        Assert.NotNull(control);
        Assert.IsType<MenuItem>(control);
        Assert.False(((MenuItem)control!).IsSubMenuOpen); // File menu not open yet

        Assert.NotNull(target.Reveal);
        await target.Reveal!();

        var fileMenuItem = window.LocateMenuItem("_File");
        Assert.True(fileMenuItem!.IsSubMenuOpen);
    }

    [AvaloniaFact]
    public void CommandCatalog_ToolCommand_LocatesRealControlWithNoMenuToReveal()
    {
        var window = new MainWindow();
        window.Show();
        var registry = new CommandRegistry();
        var targets = new CommandTargetRegistry();
        CommandCatalog.Populate(window, registry, targets);

        var target = targets.Get("tool.brush");
        Assert.NotNull(target);
        Assert.Null(target!.Reveal);

        var control = target.Locate();
        Assert.NotNull(control);
        Assert.IsType<RadioButton>(control);
    }

    [AvaloniaFact]
    public async Task CommandCatalog_EveryTarget_ResolvesUnderAnAdornerLayer()
    {
        // ControlHighlighter has a PART_HighlightLayer Canvas fallback for controls
        // with no AdornerLayer, but every control CommandCatalog registers today
        // lives inside MainWindow's themed content, which always has one. This
        // pins that invariant: if it ever breaks, ControlHighlighter's fallback
        // path (currently untested dead code) becomes load-bearing and needs
        // coverage of its own.
        var window = new MainWindow();
        window.Show();
        var registry = new CommandRegistry();
        var targets = new CommandTargetRegistry();
        CommandCatalog.Populate(window, registry, targets);

        foreach (var command in registry.All)
        {
            var target = targets.Get(command.Id)!;
            if (target.Reveal is { } reveal) await reveal();

            var control = target.Locate();
            Assert.True(control is not null, $"'{command.Id}' located no control");
            Assert.True(AdornerLayer.GetAdornerLayer(control!) is not null,
                $"'{command.Id}' resolved to a control with no AdornerLayer — " +
                "ControlHighlighter would silently fall back to PART_HighlightLayer");
        }
    }

    [AvaloniaFact]
    public async Task CommandCatalog_ChosenCommand_ControlActuallyFlashes()
    {
        // End-to-end: reveal + locate the target the same way MainWindow's
        // palette-chosen handler does, then confirm ControlHighlighter puts a
        // visible highlight on the real, located control.
        var window = new MainWindow();
        window.Show();
        var registry = new CommandRegistry();
        var targets = new CommandTargetRegistry();
        CommandCatalog.Populate(window, registry, targets);

        var target = targets.Get("color.tab.hex");
        Assert.NotNull(target);

        var control = target!.Locate();
        Assert.NotNull(control);

        var layer = window.FindControl<Canvas>("PART_HighlightLayer");
        Assert.NotNull(layer);

        var initialCount = layer!.Children.Count;

        var flashTask = ControlHighlighter.FlashAsync(control);
        // Pump the dispatcher until the highlight appears instead of relying on timing.
        await WaitForConditionAsync(() => layer!.Children.Count > initialCount);
        Assert.Equal(initialCount + 1, layer.Children.Count);

        await flashTask;
        Assert.Equal(initialCount, layer.Children.Count);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int maxWaitMs = 1000)
    {
        const int pollIntervalMs = 10;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            // Pump the Avalonia dispatcher to process any pending work and layout changes
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Normal);

            if (condition())
                return;

            if (sw.ElapsedMilliseconds > maxWaitMs)
                throw new TimeoutException($"Condition not met within {maxWaitMs}ms");

            await Task.Delay(pollIntervalMs);
        }
    }
}
