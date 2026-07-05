using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class DocumentIoTests
{
    [Fact]
    public void LoadFromFile_NonExistentPath_ReturnsNull()
    {
        var doc = Document.LoadFromFile("Z:\\nonexistent\\path\\image.png");
        Assert.Null(doc);
    }

    [Fact]
    public void SaveAndLoadPng_RoundTrip_ProducesIdenticalPixels()
    {
        // Create a known pattern
        using var original = new Document(4, 4);
        original.Clear(new Rgba(100, 150, 200, 255));
        original.BrushDab(1, 1, 1, new Rgba(255, 0, 0, 255));

        string path = Path.GetTempFileName() + ".png";
        try
        {
            original.SaveAsPng(path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);

            using var reloaded = Document.LoadFromFile(path);
            Assert.NotNull(reloaded);
            Assert.Equal(original.Width, reloaded!.Width);
            Assert.Equal(original.Height, reloaded.Height);

            // PNG is lossless — pixels must match exactly
            Assert.Equal(original.Pixels.ToArray(), reloaded.Pixels.ToArray());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveJpeg_ProducesValidFile()
    {
        using var doc = new Document(8, 8);
        doc.Clear(new Rgba(80, 160, 240, 255));

        string path = Path.GetTempFileName() + ".jpg";
        try
        {
            doc.SaveAsJpeg(path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);

            // Reload the JPEG and verify dimensions match
            using var reloaded = Document.LoadFromFile(path);
            Assert.NotNull(reloaded);
            Assert.Equal(doc.Width, reloaded!.Width);
            Assert.Equal(doc.Height, reloaded.Height);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveJpegWithAlpha_CompositesAgainstBackground()
    {
        using var doc = new Document(2, 2);
        // Semi-transparent red on transparent background
        doc.Clear(new Rgba(255, 0, 0, 128));

        string path = Path.GetTempFileName() + ".jpg";
        try
        {
            // Composite against white background
            doc.SaveAsJpeg(path, quality: 95, background: Rgba.White);
            Assert.True(File.Exists(path));

            // Reload and check the first pixel — should be ~255,128,128
            // (red 255*0.5 + white 255*0.5 = 255, 0*0.5 + 255*0.5 = 128)
            using var reloaded = Document.LoadFromFile(path);
            Assert.NotNull(reloaded);
            var px = reloaded!.Pixels;

            // JPEG is lossy, so allow some tolerance (±10)
            Assert.InRange(px[0], 245, 255); // R ≈ 255
            Assert.InRange(px[1], 118, 138); // G ≈ 128
            Assert.InRange(px[2], 118, 138); // B ≈ 128
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LoadJpeg_CanRoundTripThroughMemory()
    {
        // Create, save as JPEG, reload, verify the reloaded image loads
        // through the interop layer correctly
        using var doc = new Document(16, 16);
        doc.Clear(new Rgba(0, 200, 0, 255));

        string path = Path.GetTempFileName() + ".jpg";
        try
        {
            doc.SaveAsJpeg(path, quality: 90);
            using var reloaded = Document.LoadFromFile(path);
            Assert.NotNull(reloaded);
            Assert.Equal(16, reloaded!.Width);
            Assert.Equal(16, reloaded.Height);
            Assert.Equal(16 * 16 * 4, reloaded.Pixels.Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
