using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class ColorHistoryTests
{
    [Fact]
    public void Record_MostRecentIsFirst()
    {
        var history = new ColorHistory();
        history.Record(Rgba.White);
        history.Record(Rgba.Black);

        Assert.Equal([Rgba.Black, Rgba.White], history.Colors);
    }

    [Fact]
    public void Record_ExistingColor_MovesToFrontWithoutDuplicating()
    {
        var history = new ColorHistory();
        history.Record(Rgba.White);
        history.Record(Rgba.Black);
        history.Record(Rgba.White);

        Assert.Equal([Rgba.White, Rgba.Black], history.Colors);
        Assert.Equal(2, history.Colors.Count);
    }

    [Fact]
    public void Record_BeyondCapacity_EvictsOldest()
    {
        var history = new ColorHistory(capacity: 2);
        history.Record(new Rgba(1, 0, 0, 255));
        history.Record(new Rgba(2, 0, 0, 255));
        history.Record(new Rgba(3, 0, 0, 255));

        Assert.Equal(
            [new Rgba(3, 0, 0, 255), new Rgba(2, 0, 0, 255)],
            history.Colors);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorHistory(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorHistory(-1));
    }

    [Fact]
    public void Clear_EmptiesHistory()
    {
        var history = new ColorHistory();
        history.Record(Rgba.White);
        history.Clear();

        Assert.Empty(history.Colors);
    }
}
