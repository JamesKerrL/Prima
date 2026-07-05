using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// These run against the real native prima_c library, so they double as the
// interop smoke test and a headless integration test of the whole stack.
public class DocumentTests
{
    [Fact]
    public void Construction_ReportsDimensions()
    {
        using var doc = new Document(16, 8);
        Assert.Equal(16, doc.Width);
        Assert.Equal(8, doc.Height);
        Assert.Equal(16 * 4, doc.Stride);
        Assert.Equal(16 * 8 * 4, doc.Pixels.Length);
    }

    [Fact]
    public void Clear_FillsBufferThroughInterop()
    {
        using var doc = new Document(4, 4);
        doc.Clear(new Rgba(10, 20, 30, 40));

        var px = doc.Pixels;
        for (int i = 0; i < px.Length; i += 4)
        {
            Assert.Equal(10, px[i + 0]);
            Assert.Equal(20, px[i + 1]);
            Assert.Equal(30, px[i + 2]);
            Assert.Equal(40, px[i + 3]);
        }
    }

    [Fact]
    public void BrushDab_PaintsCenter_LeavesFarCornerUntouched()
    {
        using var doc = new Document(9, 9);
        doc.BrushDab(4, 4, 2, new Rgba(255, 0, 0, 255));

        var px = doc.Pixels;
        int center = (4 * 9 + 4) * 4;
        Assert.Equal(255, px[center + 0]);
        Assert.Equal(255, px[center + 3]);
        Assert.Equal(0, px[3]); // top-left corner alpha still zero
    }

    [Fact]
    public void SharedBuffer_ReflectsLaterMutations()
    {
        using var doc = new Document(2, 2);
        doc.Clear(new Rgba(1, 2, 3, 4));
        doc.BrushDab(0, 0, 0, new Rgba(9, 9, 9, 9));

        // Pixels returns a view over the engine's own buffer, so the dab is
        // visible without any re-fetch/copy step.
        Assert.Equal(9, doc.Pixels[0]);
    }

    [Fact]
    public void Construction_RejectsNonPositiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Document(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Document(4, -1));
    }

    [Fact]
    public void UseAfterDispose_Throws()
    {
        var doc = new Document(2, 2);
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => doc.Clear(Rgba.Transparent));
    }
}
