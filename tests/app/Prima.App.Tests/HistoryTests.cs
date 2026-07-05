using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class HistoryTests
{
    private sealed class FakeEdit : IUndoableEdit
    {
        private readonly Action<Document> _undo;
        private readonly Action<Document> _redo;

        public string Name { get; }
        public long SizeBytes { get; }

        public FakeEdit(string name, long sizeBytes, Action<Document> undo, Action<Document> redo)
        {
            Name = name;
            SizeBytes = sizeBytes;
            _undo = undo;
            _redo = redo;
        }

        public DirtyRect Undo(Document doc)
        {
            _undo(doc);
            return new DirtyRect(0, 0, 1, 1);
        }

        public DirtyRect Redo(Document doc)
        {
            _redo(doc);
            return new DirtyRect(0, 0, 1, 1);
        }
    }

    [Fact]
    public void FreshHistory_IsNotModifiedAndCannotUndoOrRedo()
    {
        var history = new History();
        Assert.False(history.IsModified);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Push_MarksModifiedAndEnablesUndo()
    {
        using var doc = new Document(2, 2);
        var history = new History();
        history.Push(new FakeEdit("e1", 10, _ => { }, _ => { }));

        Assert.True(history.IsModified);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void MarkSaved_ThenUndo_ReportsModified_ThenRedo_ReportsClean()
    {
        using var doc = new Document(2, 2);
        var history = new History();
        history.Push(new FakeEdit("e1", 10, _ => { }, _ => { }));
        history.MarkSaved();
        Assert.False(history.IsModified);

        history.Undo(doc);
        Assert.True(history.IsModified);

        history.Redo(doc);
        Assert.False(history.IsModified); // back to the saved position
    }

    [Fact]
    public void Push_ClearsRedoStack()
    {
        using var doc = new Document(2, 2);
        var history = new History();
        history.Push(new FakeEdit("e1", 10, _ => { }, _ => { }));
        history.Undo(doc);
        Assert.True(history.CanRedo);

        history.Push(new FakeEdit("e2", 10, _ => { }, _ => { }));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Undo_ThenRedo_RunsInReverseThenForwardOrder()
    {
        using var doc = new Document(2, 2);
        var log = new List<string>();
        var history = new History();
        history.Push(new FakeEdit("a", 1, _ => log.Add("undo-a"), _ => log.Add("redo-a")));
        history.Push(new FakeEdit("b", 1, _ => log.Add("undo-b"), _ => log.Add("redo-b")));

        history.Undo(doc);
        history.Undo(doc);
        Assert.Equal(new[] { "undo-b", "undo-a" }, log);

        log.Clear();
        history.Redo(doc);
        history.Redo(doc);
        Assert.Equal(new[] { "redo-a", "redo-b" }, log);
    }

    [Fact]
    public void MaxDepth_EvictsOldestEntriesFirst()
    {
        using var doc = new Document(2, 2);
        var history = new History { MaxDepth = 2 };
        var applied = new List<string>();

        history.Push(new FakeEdit("a", 1, _ => applied.Remove("a"), _ => applied.Add("a")));
        applied.Add("a");
        history.Push(new FakeEdit("b", 1, _ => applied.Remove("b"), _ => applied.Add("b")));
        applied.Add("b");
        history.Push(new FakeEdit("c", 1, _ => applied.Remove("c"), _ => applied.Add("c")));
        applied.Add("c");

        // "a" was evicted (depth cap 2): undoing everything possible should
        // only be able to undo "c" then "b", never reach "a".
        Assert.True(history.CanUndo);
        history.Undo(doc);
        history.Undo(doc);
        Assert.False(history.CanUndo);
        Assert.Contains("a", applied); // "a" was never undone — its effect remains
    }

    [Fact]
    public void MemoryBudget_EvictsOldestEntriesWhenExceeded()
    {
        var history = new History { MemoryBudgetBytes = 25, MaxDepth = 1000 };
        history.Push(new FakeEdit("a", 10, _ => { }, _ => { }));
        history.Push(new FakeEdit("b", 10, _ => { }, _ => { }));
        history.Push(new FakeEdit("c", 10, _ => { }, _ => { }));

        // Total pushed = 30 bytes > 25 budget: oldest ("a", 10 bytes) is evicted,
        // leaving "b" + "c" (20 bytes, within budget).
        using var doc = new Document(2, 2);
        int undoCount = 0;
        while (history.CanUndo)
        {
            history.Undo(doc);
            undoCount++;
        }
        Assert.Equal(2, undoCount);
    }

    [Fact]
    public void Clear_ResetsEverythingIncludingSavedMarker()
    {
        using var doc = new Document(2, 2);
        var history = new History();
        history.Push(new FakeEdit("a", 10, _ => { }, _ => { }));
        history.MarkSaved();
        history.Push(new FakeEdit("b", 10, _ => { }, _ => { }));

        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.IsModified);
    }
}
