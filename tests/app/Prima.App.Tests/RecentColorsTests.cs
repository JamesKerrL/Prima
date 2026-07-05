using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class RecentColorsTests
{
    [Fact]
    public void Record_MostRecentIsFirst()
    {
        var recent = new RecentColors();
        recent.Record(Rgba.White);
        recent.Record(Rgba.Black);

        Assert.Equal([Rgba.Black, Rgba.White], recent.Colors);
    }

    [Fact]
    public void Record_ExistingColor_MovesToFrontWithoutDuplicating()
    {
        var recent = new RecentColors();
        recent.Record(Rgba.White);
        recent.Record(Rgba.Black);
        recent.Record(Rgba.White);

        Assert.Equal([Rgba.White, Rgba.Black], recent.Colors);
        Assert.Equal(2, recent.Colors.Count);
    }

    [Fact]
    public void Record_BeyondCapacity_EvictsOldest()
    {
        var recent = new RecentColors(capacity: 2);
        recent.Record(new Rgba(1, 0, 0, 255));
        recent.Record(new Rgba(2, 0, 0, 255));
        recent.Record(new Rgba(3, 0, 0, 255));

        Assert.Equal(
            [new Rgba(3, 0, 0, 255), new Rgba(2, 0, 0, 255)],
            recent.Colors);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecentColors(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecentColors(-1));
    }

    [Fact]
    public void Clear_EmptiesHistory()
    {
        var recent = new RecentColors();
        recent.Record(Rgba.White);
        recent.Clear();

        Assert.Empty(recent.Colors);
    }
}
