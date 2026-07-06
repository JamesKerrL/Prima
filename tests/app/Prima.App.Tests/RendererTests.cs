using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// Runs against the real native prima_c library: interop smoke test for the
// rendering path plus a check that the viewport math matches the engine.
public class RendererTests
{
    // Build a document where pixel (x,y) encodes R=x, G=y, B=0, A=255.
    private static Document CoordDocument(int w, int h)
    {
        var doc = new Document(w, h);
        var px = doc.Pixels;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = (y * w + x) * 4;
            px[i + 0] = (byte)x;
            px[i + 1] = (byte)y;
            px[i + 2] = 0;
            px[i + 3] = 255;
        }
        return doc;
    }

    [Fact]
    public void IdentityViewport_ReproducesCanvas()
    {
        using var doc = CoordDocument(4, 3);
        using var renderer = Renderer.CreateSoftware();
        var target = new byte[4 * 3 * 4];

        renderer.Render(doc, target, 4, 3, 4 * 4, Viewport.Identity, Rgba.Transparent);

        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 4; x++)
        {
            int i = (y * 4 + x) * 4;
            Assert.Equal((byte)x, target[i + 0]);
            Assert.Equal((byte)y, target[i + 1]);
            Assert.Equal(255, target[i + 3]);
        }
    }

    [Fact]
    public void PannedViewport_ShiftsSampledPixels()
    {
        using var doc = CoordDocument(8, 8);
        using var renderer = Renderer.CreateSoftware();
        var target = new byte[4 * 4 * 4];

        renderer.Render(doc, target, 4, 4, 4 * 4, new Viewport(2, 3, 1.0), Rgba.Transparent);

        // Target origin samples canvas (2,3).
        Assert.Equal(2, target[0]);
        Assert.Equal(3, target[1]);
    }

    [Fact]
    public void ZoomedViewport_Magnifies()
    {
        using var doc = CoordDocument(4, 4);
        using var renderer = Renderer.CreateSoftware();
        var target = new byte[8 * 8 * 4];

        renderer.Render(doc, target, 8, 8, 8 * 4, new Viewport(0, 0, 2.0), Rgba.Transparent);

        // Target (2,0) samples canvas (1,0) at 2x zoom.
        int i = 2 * 4;
        Assert.Equal(1, target[i + 0]);
    }

    [Fact]
    public void OutsideCanvas_GetsBackground()
    {
        using var doc = CoordDocument(2, 2);
        using var renderer = Renderer.CreateSoftware();
        var target = new byte[4 * 4 * 4];
        var bg = new Rgba(9, 8, 7, 200);

        renderer.Render(doc, target, 4, 4, 4 * 4, new Viewport(-1, -1, 1.0), bg);

        // Target (0,0) -> canvas (-1,-1) -> background.
        Assert.Equal(9, target[0]);
        Assert.Equal(8, target[1]);
        Assert.Equal(7, target[2]);
        Assert.Equal(200, target[3]);
    }

    [Fact]
    public void ManagedViewport_MatchesEngineMapping()
    {
        // The C# Viewport mirrors the engine mapping; verify by rendering and
        // cross-checking the sampled canvas pixel against TargetToCanvas.
        using var doc = CoordDocument(16, 16);
        using var renderer = Renderer.CreateSoftware();
        var vp = new Viewport(1.5, 2.5, 2.0);
        var target = new byte[8 * 8 * 4];

        renderer.Render(doc, target, 8, 8, 8 * 4, vp, Rgba.Transparent);

        // For target pixel (5,4): the engine samples floor(TargetToCanvas(px+0.5)).
        int cx = (int)Math.Floor(vp.TargetToCanvasX(5 + 0.5));
        int cy = (int)Math.Floor(vp.TargetToCanvasY(4 + 0.5));
        int i = (4 * 8 + 5) * 4;
        Assert.Equal((byte)cx, target[i + 0]);
        Assert.Equal((byte)cy, target[i + 1]);
    }

    [Fact]
    public void UseAfterDispose_Throws()
    {
        var renderer = Renderer.CreateSoftware();
        renderer.Dispose();
        using var doc = CoordDocument(2, 2);
        Assert.Throws<ObjectDisposedException>(
            () => renderer.Render(doc, new byte[2 * 2 * 4], 2, 2, 2 * 4, Viewport.Identity, Rgba.Transparent));
    }

    [Fact]
    public void SoftwareRenderer_NameIsSoftware()
    {
        using var renderer = Renderer.CreateSoftware();
        Assert.Equal("software", renderer.Name);
    }

    [Fact]
    public void CreateD3D11_ReturnsRendererOrNull_WithoutThrowing()
    {
        // Null is the expected outcome where D3D11 is unavailable (e.g. a
        // non-Windows build of the native library); non-null must be a working,
        // disposable d3d11 backend.
        using var renderer = Renderer.CreateD3D11();
        if (renderer is not null)
            Assert.Equal("d3d11", renderer.Name);
    }

    [Fact]
    public void CreateBest_AlwaysReturnsWorkingRenderer()
    {
        using var doc = CoordDocument(4, 4);
        using var renderer = Renderer.CreateBest();

        Assert.Contains(renderer.Name, new[] { "d3d11", "software" });

        var target = new byte[4 * 4 * 4];
        renderer.Render(doc, target, 4, 4, 4 * 4, Viewport.Identity, Rgba.Transparent);
        Assert.Equal(255, target[3]); // canvas (0,0) alpha made it through
    }

    [Fact]
    public void D3D11_RenderMatchesSoftware()
    {
        using var d3d = Renderer.CreateD3D11();
        if (d3d is null)
            return; // D3D11 unavailable on this machine; parity is covered where it exists.

        using var doc = CoordDocument(16, 12);
        using var brush = BrushEngine.Create();
        brush.BeginStroke(doc, BrushParams.Default(new Rgba(200, 40, 40, 255), radius: 3f));
        brush.AddSamples(new[] { new InputSample(3, 3), new InputSample(12, 8) });
        brush.EndStroke();

        using var sw = Renderer.CreateSoftware();
        var bg = new Rgba(9, 8, 7, 200);
        foreach (var vp in new[] { Viewport.Identity, new Viewport(1.25, 2.75, 2.5), new Viewport(-3, -2, 1.0) })
        {
            var expected = new byte[24 * 20 * 4];
            var actual = new byte[24 * 20 * 4];
            sw.Render(doc, expected, 24, 20, 24 * 4, vp, bg);
            d3d.Render(doc, actual, 24, 20, 24 * 4, vp, bg);
            Assert.Equal(expected, actual);
        }
    }
}
