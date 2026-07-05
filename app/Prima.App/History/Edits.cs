namespace Prima.App;

/// <summary>
/// A single reversible document mutation. Implementations own the pixel data
/// needed to undo/redo themselves; <see cref="History"/> only orders them.
/// </summary>
public interface IUndoableEdit
{
    /// <summary>Human-readable label (menu text, debugging).</summary>
    string Name { get; }

    /// <summary>Approximate memory footprint, for <see cref="History"/>'s budget.</summary>
    long SizeBytes { get; }

    /// <summary>Reverts this edit on <paramref name="doc"/>. Returns the rect to re-render.</summary>
    DirtyRect Undo(Document doc);

    /// <summary>Re-applies this edit on <paramref name="doc"/>. Returns the rect to re-render.</summary>
    DirtyRect Redo(Document doc);
}

/// <summary>
/// The workhorse edit: a rectangular region's pixels before and after a
/// mutation (e.g. one brush stroke). Both buffers are packed RGBA8
/// (<c>Region.Width * 4</c> bytes per row, <c>Region.Height</c> rows).
/// </summary>
public sealed class RegionEdit : IUndoableEdit
{
    public string Name { get; }
    public DirtyRect Region { get; }
    public byte[] Before { get; }
    public byte[] After { get; }

    public long SizeBytes => Before.Length + After.Length;

    public RegionEdit(DirtyRect region, byte[] before, byte[] after, string name)
    {
        Region = region;
        Before = before;
        After = after;
        Name = name;
    }

    public DirtyRect Undo(Document doc)
    {
        doc.WriteRegion(Region, Before);
        return Region;
    }

    public DirtyRect Redo(Document doc)
    {
        doc.WriteRegion(Region, After);
        return Region;
    }
}

/// <summary>
/// A whole-canvas fill (e.g. Clear). Stores only the pre-fill snapshot plus
/// the fill color; redo re-fills rather than storing a duplicate full-canvas
/// "after" buffer.
/// </summary>
public sealed class FillEdit : IUndoableEdit
{
    public string Name { get; }
    public byte[] Before { get; }
    public Rgba FillColor { get; }

    public long SizeBytes => Before.Length;

    public FillEdit(byte[] before, Rgba fillColor, string name)
    {
        Before = before;
        FillColor = fillColor;
        Name = name;
    }

    public DirtyRect Undo(Document doc)
    {
        var full = new DirtyRect(0, 0, doc.Width, doc.Height);
        doc.WriteRegion(full, Before);
        return full;
    }

    public DirtyRect Redo(Document doc)
    {
        doc.Clear(FillColor, recordHistory: false);
        return new DirtyRect(0, 0, doc.Width, doc.Height);
    }
}

/// <summary>
/// Groups several edits into a single undo step (layers, filters, transforms
/// that touch multiple regions at once). Undo runs children in reverse order;
/// redo runs them forward. The returned rect is the union of all children's.
/// </summary>
public sealed class CompositeEdit : IUndoableEdit
{
    public string Name { get; }
    private readonly IReadOnlyList<IUndoableEdit> _edits;

    public long SizeBytes
    {
        get
        {
            long total = 0;
            foreach (var edit in _edits) total += edit.SizeBytes;
            return total;
        }
    }

    public CompositeEdit(IReadOnlyList<IUndoableEdit> edits, string name)
    {
        _edits = edits;
        Name = name;
    }

    public DirtyRect Undo(Document doc)
    {
        DirtyRect dirty = default;
        for (int i = _edits.Count - 1; i >= 0; i--)
            dirty = dirty.Union(_edits[i].Undo(doc));
        return dirty;
    }

    public DirtyRect Redo(Document doc)
    {
        DirtyRect dirty = default;
        for (int i = 0; i < _edits.Count; i++)
            dirty = dirty.Union(_edits[i].Redo(doc));
        return dirty;
    }
}
