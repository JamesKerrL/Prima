using Prima.App.Commands;
using Xunit;

namespace Prima.App.Tests;

public class CommandRegistryTests
{
    private static CommandRegistry CreatePopulatedRegistry()
    {
        var reg = new CommandRegistry();
        reg.Register(new CommandDescriptor("file.open", "Open File", "File", ["load", "import", "picture"], "Ctrl+O"));
        reg.Register(new CommandDescriptor("file.export.png", "Export as PNG", "File", ["save"], "Ctrl+Shift+S"));
        reg.Register(new CommandDescriptor("file.export.jpeg", "Export as JPEG", "File", ["save"]));
        reg.Register(new CommandDescriptor("app.settings", "Settings", "File", ["preferences", "options"]));
        reg.Register(new CommandDescriptor("view.fullscreen", "Toggle Fullscreen", "View", ["full", "screen"], "F11"));
        reg.Register(new CommandDescriptor("tool.brush", "Brush Tool", "Tools", ["brush", "paint"]));
        return reg;
    }

    [Fact]
    public void Search_ExactTitleMatch_RanksFirst()
    {
        var reg = CreatePopulatedRegistry();
        var results = reg.Search("Open File");
        Assert.NotEmpty(results);
        Assert.Equal("file.open", results[0].Command.Id);
    }

    [Fact]
    public void Search_KeywordMatch_FindsCommand()
    {
        var reg = CreatePopulatedRegistry();
        var results = reg.Search("import");
        Assert.Contains(results, r => r.Command.Id == "file.open");
    }

    [Fact]
    public void Search_CategoryMatch_FindsCommands()
    {
        var reg = CreatePopulatedRegistry();
        var results = reg.Search("View");
        Assert.Contains(results, r => r.Command.Id == "view.fullscreen");
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsAllOrdered()
    {
        var reg = CreatePopulatedRegistry();
        var results = reg.Search("");
        Assert.Equal(6, results.Count);
        for (int i = 1; i < results.Count; i++)
        {
            int cmp = string.CompareOrdinal(results[i - 1].Command.Category, results[i].Command.Category);
            Assert.True(cmp <= 0);
        }
    }

    [Fact]
    public void Search_MaxCapsResults()
    {
        var reg = CreatePopulatedRegistry();
        var results = reg.Search("", max: 2);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var reg = CreatePopulatedRegistry();
        var results = reg.Search("zzzzzthisdoesnotexist");
        Assert.Empty(results);
    }

    [Fact]
    public void RegisterAndAll_ReflectsAddedCommands()
    {
        var reg = new CommandRegistry();
        Assert.Empty(reg.All);
        reg.Register(new CommandDescriptor("test.cmd", "Test", "Test", []));
        Assert.Single(reg.All);
    }

    [Fact]
    public void Search_ContiguousPrefix_OutranksScattered()
    {
        var reg = new CommandRegistry();
        reg.Register(new CommandDescriptor("a.b", "Export as PNG", "A", []));
        reg.Register(new CommandDescriptor("c.d", "Axe", "A", []));
        var results = reg.Search("ex");
        Assert.Equal("Export as PNG", results[0].Command.Title);
    }
}
