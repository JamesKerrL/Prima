namespace Prima.App;

public sealed unsafe partial class Document
{
    /// <summary>This document's undo/redo history. One instance per document.</summary>
    public History History { get; } = new();

    /// <summary>True if the document has unsaved changes (diverged from the last <see cref="MarkSaved"/>).</summary>
    public bool IsModified => History.IsModified;

    /// <summary>Record the current state as matching what's on disk.</summary>
    public void MarkSaved() => History.MarkSaved();

    /// <summary>Undo the most recent edit. Returns the rect to re-render (empty if nothing to undo).</summary>
    public DirtyRect Undo()
    {
        ThrowIfDisposed();
        return History.Undo(this);
    }

    /// <summary>Redo the most recently undone edit. Returns the rect to re-render (empty if nothing to redo).</summary>
    public DirtyRect Redo()
    {
        ThrowIfDisposed();
        return History.Redo(this);
    }

    /// <summary>
    /// Record a completed brush stroke as a single undoable edit. Call after
    /// <see cref="BrushEngine.EndStroke"/> with the union of every dirty rect
    /// the stroke produced. A no-op for strokes that touched no pixels.
    /// </summary>
    public void PushStrokeEdit(BrushEngine engine, DirtyRect strokeDirty)
    {
        ThrowIfDisposed();
        if (strokeDirty.IsEmpty) return;

        byte[] before = engine.ReadBaselineRegion(strokeDirty);
        byte[] after = ReadRegion(strokeDirty);
        History.Push(new RegionEdit(strokeDirty, before, after, "Brush stroke"));
    }
}
