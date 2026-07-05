namespace Prima.App;

/// <summary>
/// Per-document undo/redo stack. Bounds its own memory footprint by evicting
/// the oldest entries once <see cref="MemoryBudgetBytes"/> or
/// <see cref="MaxDepth"/> is exceeded. Fully headless — no UI dependency.
/// </summary>
public sealed class History
{
    /// <summary>Total bytes of undo history to retain before evicting the oldest entries.</summary>
    public long MemoryBudgetBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>Maximum number of undo entries to retain.</summary>
    public int MaxDepth { get; set; } = 200;

    // Bottom (index 0) = oldest. Push appends; eviction removes from the front.
    private readonly List<IUndoableEdit> _undo = new();
    private readonly Stack<IUndoableEdit> _redo = new();

    private long _sizeBytes;

    // Monotonic position counter: +1 on Push/Redo, -1 on Undo. Eviction never
    // changes it — it only shrinks how far _undo can reach back, which the
    // counter reflects implicitly (once the list empties, undo can't go
    // further, so position can never return to an evicted saved position).
    // IsModified is just "does the current position match the saved one" —
    // no extra bookkeeping needed for eviction to be handled correctly.
    private long _position;
    private long _savedPosition;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>True if the document has diverged from the last <see cref="MarkSaved"/> position.</summary>
    public bool IsModified => _position != _savedPosition;

    public event Action? Changed;

    /// <summary>Push a newly-applied edit onto the undo stack, clearing any redo history.</summary>
    public void Push(IUndoableEdit edit)
    {
        _redo.Clear();
        _undo.Add(edit);
        _sizeBytes += edit.SizeBytes;
        _position++;
        Evict();
        Changed?.Invoke();
    }

    public DirtyRect Undo(Document doc)
    {
        if (_undo.Count == 0) return default;
        var edit = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _sizeBytes -= edit.SizeBytes;
        DirtyRect rect = edit.Undo(doc);
        _redo.Push(edit);
        _position--;
        Changed?.Invoke();
        return rect;
    }

    public DirtyRect Redo(Document doc)
    {
        if (_redo.Count == 0) return default;
        var edit = _redo.Pop();
        DirtyRect rect = edit.Redo(doc);
        _undo.Add(edit);
        _sizeBytes += edit.SizeBytes;
        _position++;
        Evict();
        Changed?.Invoke();
        return rect;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _sizeBytes = 0;
        _position = 0;
        _savedPosition = 0;
        Changed?.Invoke();
    }

    /// <summary>Record the current position as "clean" (matches on-disk state).</summary>
    public void MarkSaved()
    {
        _savedPosition = _position;
        Changed?.Invoke();
    }

    private void Evict()
    {
        while (_undo.Count > 0 &&
               (_undo.Count > MaxDepth || _sizeBytes > MemoryBudgetBytes))
        {
            var oldest = _undo[0];
            _undo.RemoveAt(0);
            _sizeBytes -= oldest.SizeBytes;
        }
    }
}
