using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// Headless integration tests against the real native prima_c library: flood
// fill mutates the document and is recorded as a single undoable edit.
public class FloodFillTests
{
    private static byte[] Full(Document doc) =>
        doc.ReadRegion(new DirtyRect(0, 0, doc.Width, doc.Height));

    [Fact]
    public void FloodFill_UniformCanvas_FillsEverythingAndIsModified()
    {
        using var doc = new Document(16, 16);
        doc.Clear(new Rgba(10, 20, 30, 255));
        doc.MarkSaved();

        var dirty = doc.FloodFill(8, 8, new Rgba(200, 100, 50, 255));

        Assert.Equal(new DirtyRect(0, 0, 16, 16), dirty);
        Assert.True(doc.IsModified);
        Assert.True(doc.History.CanUndo);
        Assert.Equal(200, doc.Pixels[0]);
        Assert.Equal(100, doc.Pixels[1]);
        Assert.Equal(50, doc.Pixels[2]);
    }

    [Fact]
    public void FloodFill_UndoRestoresExactPixels_RedoReapplies()
    {
        using var doc = new Document(24, 24);
        doc.Clear(new Rgba(0, 0, 0, 255));
        // A brush dab creates a distinct region so the fill isn't whole-canvas.
        doc.BrushDab(12, 12, 5, new Rgba(255, 0, 0, 255));
        byte[] before = Full(doc);

        // Fill the black background (corner pixel is background).
        var dirty = doc.FloodFill(0, 0, new Rgba(0, 0, 255, 255));
        Assert.False(dirty.IsEmpty);

        byte[] afterFill = Full(doc);
        Assert.NotEqual(before, afterFill);
        // Background changed to blue...
        Assert.Equal(255, doc.Pixels[2]);
        // ...the red dab center is untouched.
        int centerIdx = (12 * doc.Stride) + 12 * 4;
        Assert.Equal(255, doc.Pixels[centerIdx + 0]);
        Assert.Equal(0, doc.Pixels[centerIdx + 2]);

        doc.Undo();
        Assert.Equal(before, Full(doc));

        doc.Redo();
        Assert.Equal(afterFill, Full(doc));
    }

    [Fact]
    public void FloodFill_WithSeedColor_IsNoOp_NoHistoryEntry()
    {
        using var doc = new Document(8, 8);
        doc.FloodFill(0, 0, new Rgba(50, 60, 70, 255));
        doc.MarkSaved();
        byte[] before = Full(doc);

        // Filling the same region with its own color changes nothing.
        var dirty = doc.FloodFill(4, 4, new Rgba(50, 60, 70, 255));

        Assert.True(dirty.IsEmpty);
        Assert.False(doc.IsModified); // no new edit pushed
        Assert.Equal(before, Full(doc));
    }

    [Fact]
    public void FloodFill_OutOfBoundsSeed_IsNoOp()
    {
        using var doc = new Document(8, 8);
        doc.FloodFill(0, 0, new Rgba(1, 2, 3, 255));
        doc.MarkSaved();
        byte[] before = Full(doc);

        Assert.True(doc.FloodFill(-1, 0, new Rgba(9, 9, 9, 255)).IsEmpty);
        Assert.True(doc.FloodFill(0, 8, new Rgba(9, 9, 9, 255)).IsEmpty);

        Assert.False(doc.IsModified); // no new edit pushed
        Assert.Equal(before, Full(doc));
    }

    [Fact]
    public void FloodFill_DirtyRectMatchesFilledRegion()
    {
        using var doc = new Document(32, 32);
        doc.Clear(new Rgba(0, 0, 0, 255));
        // Carve a 4x4 red block; fill it and expect a tight 4x4 dirty rect.
        for (int y = 10; y < 14; y++)
            for (int x = 20; x < 24; x++)
                doc.BrushDab(x, y, 0, new Rgba(255, 0, 0, 255));

        var dirty = doc.FloodFill(20, 10, new Rgba(0, 255, 0, 255));

        Assert.Equal(new DirtyRect(20, 10, 4, 4), dirty);
    }

    [Fact]
    public void FloodFill_ContiguousOnly_StopsAtBoundary()
    {
        using var doc = new Document(16, 3);
        doc.Clear(new Rgba(255, 255, 255, 255));
        // A vertical wall at column 8 splits the row band into left and right.
        for (int y = 0; y < 3; y++)
            doc.BrushDab(8, y, 0, new Rgba(0, 0, 0, 255));

        doc.FloodFill(0, 1, new Rgba(255, 0, 0, 255));

        // Left of the wall is red; right of the wall is still white.
        int leftIdx = (1 * doc.Stride) + 0 * 4;
        int rightIdx = (1 * doc.Stride) + 12 * 4;
        Assert.Equal(255, doc.Pixels[leftIdx + 0]);
        Assert.Equal(0, doc.Pixels[leftIdx + 1]);
        Assert.Equal(255, doc.Pixels[rightIdx + 0]);
        Assert.Equal(255, doc.Pixels[rightIdx + 1]);
    }
}
