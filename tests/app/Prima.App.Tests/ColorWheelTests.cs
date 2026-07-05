using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// These run against the real native prima_c library.
public class ColorWheelTests
{
    [Fact]
    public void Construction_ReportsDimensions()
    {
        using var wheel = new ColorWheel(64, 8);
        Assert.Equal(64, wheel.RingWidth);
        Assert.Equal(64, wheel.RingHeight);
        Assert.Equal(64 - 2 * 8, wheel.TriangleWidth);
        Assert.Equal(64 - 2 * 8, wheel.TriangleHeight);
    }

    [Fact]
    public void RingPixels_IsNonEmptyAndHasSomeOpaquePixels()
    {
        using var wheel = new ColorWheel(64, 8);
        var ring = wheel.RingPixels;
        Assert.Equal(64 * 64 * 4, ring.Length);

        bool anyOpaque = false;
        for (int i = 3; i < ring.Length; i += 4)
        {
            if (ring[i] > 0) { anyOpaque = true; break; }
        }
        Assert.True(anyOpaque);
    }

    [Fact]
    public void TrianglePixels_CornerIsTransparent()
    {
        using var wheel = new ColorWheel(200, 20);
        var triangle = wheel.TrianglePixels;
        // Top-left corner of the bounding box is outside the triangle.
        Assert.Equal(0, triangle[3]);
    }

    [Fact]
    public void SetHue_ChangesTriangleTopColor()
    {
        using var wheel = new ColorWheel(200, 20);
        int w = wheel.TriangleWidth;

        wheel.SetHue(0.0);
        int topOffset = (1 * w + w / 2) * 4;
        var red = wheel.TrianglePixels.Slice(topOffset, 4).ToArray();

        wheel.SetHue(120.0);
        var green = wheel.TrianglePixels.Slice(topOffset, 4).ToArray();

        Assert.NotEqual(red[0], green[0]);
    }

    [Fact]
    public void Construction_RejectsNonPositiveOuterSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorWheel(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorWheel(-1, 4));
    }

    [Fact]
    public void Construction_RejectsNegativeThickness()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorWheel(64, -1));
    }

    [Fact]
    public void UseAfterDispose_Throws()
    {
        var wheel = new ColorWheel(64, 8);
        wheel.Dispose();
        Assert.Throws<ObjectDisposedException>(() => wheel.SetHue(90.0));
    }
}
