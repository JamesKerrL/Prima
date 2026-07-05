using System.Runtime.InteropServices;
using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// These run against the real native prima_c library, exercising the brush engine
// through the full interop stack: C# -> ABI -> engine -> pixels back.
public class BrushEngineTests
{
    [Fact]
    public void BeginAddEnd_ChangesPixels_AndReturnsNonEmptyDirtyRect()
    {
        using var doc = new Document(64, 64);
        doc.Clear(Rgba.Transparent);

        using var engine = BrushEngine.Create();
        var bp = BrushParams.Default(new Rgba(255, 0, 0, 255), radius: 10);

        engine.BeginStroke(doc, bp);
        var dirty = engine.AddSamples([new InputSample(32f, 32f, 1f)]);
        dirty = dirty.Union(engine.EndStroke());

        Assert.False(dirty.IsEmpty);
        Assert.InRange(dirty.X, 0, doc.Width);
        Assert.InRange(dirty.Y, 0, doc.Height);
        Assert.InRange(dirty.Width, 1, doc.Width);
        Assert.InRange(dirty.Height, 1, doc.Height);

        var px = doc.Pixels;
        int center = (32 * 64 + 32) * 4;
        Assert.Equal(255, px[center + 0]);
    }

    [Fact]
    public void ReplayDeterminism_ProducesIdenticalPixelHash()
    {
        var samples = new InputSample[]
        {
            new(10f, 10f, 0.3f),
            new(20f, 12f, 0.6f),
            new(35f, 18f, 0.8f),
            new(50f, 30f, 1.0f),
        };
        var bp = BrushParams.Default(new Rgba(40, 90, 200, 255), radius: 8);

        ulong Hash(Document doc)
        {
            var px = doc.Pixels;
            ulong h = 14695981039346656037;
            foreach (byte b in px) { h ^= b; h *= 1099511628211; }
            return h;
        }

        ulong hashA, hashB;
        using (var docA = new Document(64, 64))
        {
            docA.Clear(Rgba.Transparent);
            using var engineA = BrushEngine.Create();
            engineA.BeginStroke(docA, bp);
            engineA.AddSamples(samples);
            engineA.EndStroke();
            hashA = Hash(docA);
        }

        using (var docB = new Document(64, 64))
        {
            docB.Clear(Rgba.Transparent);
            using var engineB = BrushEngine.Create();
            engineB.BeginStroke(docB, bp);
            engineB.AddSamples(samples);
            engineB.EndStroke();
            hashB = Hash(docB);
        }

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void BatchingEquivalence_SingleCallVsMultipleCalls_Match()
    {
        var samples = new InputSample[]
        {
            new(10f, 10f, 1f),
            new(30f, 20f, 1f),
            new(50f, 30f, 1f),
        };
        var bp = BrushParams.Default(new Rgba(0, 200, 50, 255), radius: 8);

        ulong Hash(Document doc)
        {
            var px = doc.Pixels;
            ulong h = 14695981039346656037;
            foreach (byte b in px) { h ^= b; h *= 1099511628211; }
            return h;
        }

        ulong hashSingle, hashMulti;
        using (var doc = new Document(64, 64))
        {
            doc.Clear(Rgba.Transparent);
            using var engine = BrushEngine.Create();
            engine.BeginStroke(doc, bp);
            engine.AddSamples(samples);
            engine.EndStroke();
            hashSingle = Hash(doc);
        }

        using (var doc = new Document(64, 64))
        {
            doc.Clear(Rgba.Transparent);
            using var engine = BrushEngine.Create();
            engine.BeginStroke(doc, bp);
            foreach (var s in samples)
                engine.AddSamples([s]);
            engine.EndStroke();
            hashMulti = Hash(doc);
        }

        Assert.Equal(hashSingle, hashMulti);
    }

    [Fact]
    public void StructSizes_MatchNativeLayout()
    {
        Assert.Equal(44, Marshal.SizeOf<BrushParams>());
        Assert.Equal(32, Marshal.SizeOf<InputSample>());
    }

    [Fact]
    public void StrokeActive_TracksState()
    {
        using var doc = new Document(16, 16);
        doc.Clear(Rgba.Transparent);

        using var engine = BrushEngine.Create();
        Assert.False(engine.StrokeActive);

        var bp = BrushParams.Default(Rgba.White);
        engine.BeginStroke(doc, bp);
        Assert.True(engine.StrokeActive);

        engine.EndStroke();
        Assert.False(engine.StrokeActive);
    }

    [Fact]
    public void UseAfterDispose_Throws()
    {
        var engine = BrushEngine.Create();
        engine.Dispose();
        var bp = BrushParams.Default(Rgba.White);
        using var doc = new Document(8, 8);
        Assert.Throws<ObjectDisposedException>(() => engine.BeginStroke(doc, bp));
    }
}
