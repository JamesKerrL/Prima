using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class SwatchPaletteTests
{
    [Fact]
    public void Add_AppendsInOrder()
    {
        var palette = new SwatchPalette();
        palette.Add(Rgba.White);
        palette.Add(Rgba.Black);

        Assert.Equal([Rgba.White, Rgba.Black], palette.Swatches);
    }

    [Fact]
    public void Add_DedupesExactColor()
    {
        var palette = new SwatchPalette();
        palette.Add(new Rgba(1, 2, 3, 4));
        palette.Add(new Rgba(1, 2, 3, 4));

        Assert.Single(palette.Swatches);
    }

    [Fact]
    public void Remove_DropsMatchingColor()
    {
        var palette = new SwatchPalette();
        palette.Add(Rgba.White);
        palette.Add(Rgba.Black);
        palette.Remove(Rgba.White);

        Assert.Equal([Rgba.Black], palette.Swatches);
    }

    [Fact]
    public void Remove_MissingColor_IsNoOp()
    {
        var palette = new SwatchPalette();
        palette.Add(Rgba.White);
        palette.Remove(Rgba.Black);

        Assert.Equal([Rgba.White], palette.Swatches);
    }

    [Fact]
    public void Clear_EmptiesPalette()
    {
        var palette = new SwatchPalette();
        palette.Add(Rgba.White);
        palette.Clear();

        Assert.Empty(palette.Swatches);
    }
}
