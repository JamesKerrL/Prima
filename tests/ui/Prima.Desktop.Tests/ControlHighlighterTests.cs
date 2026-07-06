using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Prima.Desktop.Commands;
using Xunit;

namespace Prima.Desktop.Tests;

public class ControlHighlighterTests
{
    [AvaloniaFact]
    public async Task FlashAsync_NullControl_IsNoOp()
    {
        await ControlHighlighter.FlashAsync(null);
    }
}
