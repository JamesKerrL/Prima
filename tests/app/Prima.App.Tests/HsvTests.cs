using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// Round-trips through the real native prima_c library.
public class HsvTests
{
    [Fact]
    public void ToRgba_PureHueValues()
    {
        Assert.Equal(new Rgba(255, 0, 0, 255), new Hsv(0, 1, 1).ToRgba());
        Assert.Equal(new Rgba(0, 255, 0, 255), new Hsv(120, 1, 1).ToRgba());
        Assert.Equal(new Rgba(0, 0, 255, 255), new Hsv(240, 1, 1).ToRgba());
    }

    [Fact]
    public void ToRgba_PassesAlphaThrough()
    {
        Assert.Equal(128, new Hsv(0, 1, 1).ToRgba(128).A);
    }

    [Fact]
    public void FromRgba_White_IsZeroSaturationFullValue()
    {
        var hsv = Hsv.FromRgba(new Rgba(255, 255, 255, 255));
        Assert.Equal(0.0, hsv.S, 3);
        Assert.Equal(1.0, hsv.V, 3);
    }

    [Fact]
    public void RoundTrip_RgbaToHsvToRgba()
    {
        var original = new Rgba(12, 200, 77, 255);
        var hsv = Hsv.FromRgba(original);
        var roundTripped = hsv.ToRgba(original.A);
        Assert.InRange(roundTripped.R, original.R - 1, original.R + 1);
        Assert.InRange(roundTripped.G, original.G - 1, original.G + 1);
        Assert.InRange(roundTripped.B, original.B - 1, original.B + 1);
    }

    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("00FF00", 0, 255, 0)]
    [InlineData("#00F", 0, 0, 255)]
    [InlineData("abc", 0xAA, 0xBB, 0xCC)]
    public void TryParseHex_ValidInputs(string text, byte r, byte g, byte b)
    {
        Assert.True(Hsv.TryParseHex(text, out var color));
        Assert.Equal(new Rgba(r, g, b, 255), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#GGGGGG")]
    [InlineData("#1234")]
    public void TryParseHex_InvalidInputs_ReturnsFalse(string? text)
    {
        Assert.False(Hsv.TryParseHex(text, out _));
    }

    [Fact]
    public void ToHex_FormatsUppercase()
    {
        Assert.Equal("#0AFF64", Hsv.ToHex(new Rgba(0x0A, 0xFF, 0x64, 255)));
    }
}
