using Prima.App;
using Xunit;

namespace Prima.App.Tests;

// Headless integration tests against the real native prima_c library: brush
// strokes and Clear are recorded as undoable edits and can be reverted/replayed.
public class DocumentHistoryTests
{
    private static BrushParams TestBrush(Rgba color)
    {
        var p = BrushParams.Default(color, radius: 6f);
        p.Hardness = 1f;
        return p;
    }

    [Fact]
    public void ReadRegion_WriteRegion_RoundTripsExactBytes()
    {
        using var doc = new Document(8, 8);
        doc.Clear(new Rgba(1, 2, 3, 4));
        doc.BrushDab(4, 4, 2, new Rgba(9, 8, 7, 6));

        var region = new DirtyRect(1, 1, 5, 5);
        byte[] snapshot = doc.ReadRegion(region);

        doc.BrushDab(1, 1, 1, new Rgba(255, 255, 255, 255));
        Assert.NotEqual(snapshot, doc.ReadRegion(region));

        doc.WriteRegion(region, snapshot);
        Assert.Equal(snapshot, doc.ReadRegion(region));
    }

    [Fact]
    public void FreshDocument_IsNotModified_AndHasEmptyHistory()
    {
        using var doc = new Document(8, 8);
        Assert.False(doc.IsModified);
        Assert.False(doc.History.CanUndo);
        Assert.False(doc.History.CanRedo);
    }

    [Fact]
    public void BrushStroke_PushStrokeEdit_UndoRestoresExactPreStrokePixels()
    {
        using var doc = new Document(32, 32);
        doc.Clear(new Rgba(5, 6, 7, 8));
        byte[] before = doc.ReadRegion(new DirtyRect(0, 0, 32, 32));

        using var engine = BrushEngine.Create();
        var brush = TestBrush(new Rgba(200, 20, 20, 255));

        engine.BeginStroke(doc, brush);
        var dirty = engine.AddSamples(new[] { new InputSample(16, 16, 1f) });
        dirty = dirty.Union(engine.EndStroke());

        doc.PushStrokeEdit(engine, dirty);
        Assert.True(doc.IsModified);

        byte[] afterStroke = doc.ReadRegion(new DirtyRect(0, 0, 32, 32));
        Assert.NotEqual(before, afterStroke);

        doc.Undo();
        byte[] afterUndo = doc.ReadRegion(new DirtyRect(0, 0, 32, 32));
        Assert.Equal(before, afterUndo);

        doc.Redo();
        byte[] afterRedo = doc.ReadRegion(new DirtyRect(0, 0, 32, 32));
        Assert.Equal(afterStroke, afterRedo);
    }

    [Fact]
    public void EmptyStroke_PushesNoHistoryEntry()
    {
        using var doc = new Document(8, 8);
        using var engine = BrushEngine.Create();
        var brush = TestBrush(new Rgba(1, 1, 1, 1));

        engine.BeginStroke(doc, brush);
        var dirty = engine.EndStroke(); // no samples added: empty dirty rect

        doc.PushStrokeEdit(engine, dirty);
        Assert.False(doc.History.CanUndo);
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void Clear_IsUndoable()
    {
        using var doc = new Document(8, 8);
        doc.Clear(new Rgba(10, 20, 30, 40));
        doc.MarkSaved();

        doc.Clear(new Rgba(200, 200, 200, 255));
        Assert.True(doc.IsModified);
        Assert.Equal(200, doc.Pixels[0]);

        doc.Undo();
        Assert.False(doc.IsModified);
        Assert.Equal(10, doc.Pixels[0]);
        Assert.Equal(20, doc.Pixels[1]);
        Assert.Equal(30, doc.Pixels[2]);
        Assert.Equal(40, doc.Pixels[3]);

        doc.Redo();
        Assert.Equal(200, doc.Pixels[0]);
    }

    [Fact]
    public void MultiStrokeSequence_UndoRedoRestoresEachStepExactly()
    {
        using var doc = new Document(32, 32);

        byte[] Snapshot() => doc.ReadRegion(new DirtyRect(0, 0, 32, 32));

        // doc.Clear is itself an undoable edit, so the very first snapshot
        // (before any history exists) is captured before it runs.
        var snapshots = new List<byte[]> { Snapshot() };
        doc.Clear(new Rgba(0, 0, 0, 0));
        snapshots.Add(Snapshot());

        using var engine = BrushEngine.Create();
        var brush = TestBrush(new Rgba(255, 0, 0, 255));

        foreach (var (x, y) in new (float, float)[] { (8, 8), (16, 16), (24, 24) })
        {
            engine.BeginStroke(doc, brush);
            var dirty = engine.AddSamples(new[] { new InputSample(x, y, 1f) });
            dirty = dirty.Union(engine.EndStroke());
            doc.PushStrokeEdit(engine, dirty);
            snapshots.Add(Snapshot());
        }

        // Undo Clear + all three strokes, checking against each prior snapshot.
        for (int i = snapshots.Count - 1; i >= 1; i--)
        {
            doc.Undo();
            Assert.Equal(snapshots[i - 1], Snapshot());
        }
        Assert.False(doc.History.CanUndo);

        // Redo everything back to the final state.
        for (int i = 1; i < snapshots.Count; i++)
        {
            doc.Redo();
            Assert.Equal(snapshots[i], Snapshot());
        }
        Assert.False(doc.History.CanRedo);
    }

    [Fact]
    public void SaveAsPng_MarksSaved()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"prima_test_{Guid.NewGuid():N}.png");
        try
        {
            using var doc = new Document(4, 4);
            doc.Clear(new Rgba(1, 2, 3, 4));
            Assert.True(doc.IsModified);

            doc.SaveAsPng(path);
            Assert.False(doc.IsModified);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
