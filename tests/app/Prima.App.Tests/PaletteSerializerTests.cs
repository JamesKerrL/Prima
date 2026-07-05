using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class PaletteSerializerTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesColorsAndOrder()
    {
        var palette = new SwatchPalette();
        palette.Add(new Rgba(10, 20, 30, 255));
        palette.Add(new Rgba(200, 100, 50, 128));
        palette.Add(Rgba.Black);

        var restored = PaletteSerializer.Deserialize(PaletteSerializer.Serialize(palette));

        Assert.Equal(palette.Swatches, restored.Swatches);
    }

    [Fact]
    public void RoundTrip_PreservesAlpha()
    {
        var palette = new SwatchPalette();
        palette.Add(new Rgba(255, 0, 0, 64));

        var restored = PaletteSerializer.Deserialize(PaletteSerializer.Serialize(palette));

        Assert.Equal((byte)64, Assert.Single(restored.Swatches).A);
    }

    [Fact]
    public void Serialize_EmptyPalette_RoundTripsToEmpty()
    {
        var restored = PaletteSerializer.Deserialize(PaletteSerializer.Serialize(new SwatchPalette()));

        Assert.Empty(restored.Swatches);
    }

    [Fact]
    public void Deserialize_SkipsInvalidHexEntries()
    {
        // "not-a-color" and "#12" are unparseable; the two valid entries survive.
        const string json = """
            { "Colors": ["#FF0000FF", "not-a-color", "#12", "#00FF00FF"] }
            """;

        var palette = PaletteSerializer.Deserialize(json);

        Assert.Equal(
            [new Rgba(255, 0, 0, 255), new Rgba(0, 255, 0, 255)],
            palette.Swatches);
    }

    [Fact]
    public void Deserialize_MissingColorsProperty_ReturnsEmptyPalette()
    {
        var palette = PaletteSerializer.Deserialize("{}");

        Assert.Empty(palette.Swatches);
    }

    [Fact]
    public void SaveToFile_CreatesMissingDirectory()
    {
        string dir = Path.Combine(Path.GetTempFileName() + "_d", "nested");
        string path = Path.Combine(dir, "palette.json");
        try
        {
            var palette = new SwatchPalette();
            palette.Add(Rgba.White);

            PaletteSerializer.SaveToFile(palette, path);

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(path)!)))
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!, recursive: true);
        }
    }

    [Fact]
    public void SaveToFile_LoadFromFile_RoundTrip()
    {
        string path = Path.GetTempFileName() + ".json";
        try
        {
            var palette = new SwatchPalette();
            palette.Add(new Rgba(1, 2, 3, 4));
            palette.Add(new Rgba(250, 240, 230, 255));

            PaletteSerializer.SaveToFile(palette, path);
            var loaded = PaletteSerializer.LoadFromFile(path);

            Assert.Equal(palette.Swatches, loaded.Swatches);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
