# Undo / Redo

Status: **planned** (Milestone 1 — "Undo/redo (command pattern in the app
layer)"). This doc is the implementation hand-off: it can be executed
task-by-task. Delete it once the feature ships and the ROADMAP box is checked.

## Why

Prima has no undo/redo today. Brush strokes mutate the canvas directly from the
UI ([CanvasControl.cs](../../ui/Prima.Desktop/Controls/CanvasControl.cs)) with no
command indirection, and `Document.Clear` overwrites the pixel buffer
irreversibly. CLAUDE.md assigns undo/redo to the C# application layer, and it
must be fully usable headlessly (the CLI host and xUnit tests exercise it with no
UI).

The goal: a small, generic command/history framework in `app/Prima.App` whose
first two concrete edits are **brush strokes** and **Clear**, that is cheap on
RAM by default, and that leaves clean seams for saving/loading projects.

## Design decisions

- **Generic framework now, two edits first.** We architect an `IUndoableEdit`
  abstraction (so layers, fills, filters, transforms plug in later) but only ship
  stroke + clear edits in v1.
- **Region snapshots, not full-canvas snapshots.** Each edit stores only the
  pixels inside its dirty rectangle, plus a memory/depth budget that evicts the
  oldest history first. See [RAM reduction](#ram-reduction-options).
- **Reuse the engine's existing snapshot for "before" pixels.** The brush engine
  already copies the whole canvas into `baseline_` at `beginStroke` and tracks an
  exact `strokeDirty_` union rect
  ([brush_engine.cpp](../../engine/src/brush_engine.cpp)). We add exactly **one**
  native function to read a region out of that baseline — no extra snapshot, no
  new full-canvas copy on the hot path.
- **Region read/write is pure C#.** `Document.Pixels` already exposes the engine
  buffer as a `Span<byte>`, so copying a rectangle in/out needs no interop change.

## Architecture

All history logic lives in `app/Prima.App` (no UI dependencies). The UI only
captures input, calls undo/redo, and re-renders the returned dirty rect.

### Core types (new, in `app/Prima.App`)

```csharp
public interface IUndoableEdit
{
    string Name { get; }                 // for menu labels / debugging
    long SizeBytes { get; }              // for the memory budget
    DirtyRect Undo(Document doc);        // returns the rect to re-render
    DirtyRect Redo(Document doc);
}
```

- **`RegionEdit : IUndoableEdit`** — the workhorse. Holds a `DirtyRect Region`
  and two tightly-packed pixel buffers `byte[] Before` and `byte[] After`
  (`Region.Width * 4` bytes per row, `Region.Height` rows). `Undo` writes
  `Before` back into the canvas region; `Redo` writes `After`. `SizeBytes` =
  `Before.Length + After.Length` + overhead.
- **`FillEdit : IUndoableEdit`** — for `Clear` and future flood fills. Stores the
  whole-canvas `Before` buffer plus the fill `Rgba`. `Redo` re-fills with the
  color (no `After` buffer needed), so it costs one full-canvas snapshot instead
  of two.
- **`CompositeEdit : IUndoableEdit`** — groups several edits into a single undo
  step (future-proofing for multi-region ops, layers, filters, transforms).
  `Undo` iterates children in reverse, `Redo` in forward order, unioning the
  returned rects.
- **`History`** — owns the undo list and redo stack and enforces the budget:

```csharp
public sealed class History
{
    public long MemoryBudgetBytes { get; set; } = 256L * 1024 * 1024;
    public int  MaxDepth          { get; set; } = 200;

    public bool CanUndo { get; }
    public bool CanRedo { get; }
    public bool IsModified { get; }        // current position != saved marker
    public event Action? Changed;          // UI hook (buttons, title bar)

    public void      Push(IUndoableEdit edit); // clears redo; evicts oldest over budget/depth
    public DirtyRect Undo(Document doc);
    public DirtyRect Redo(Document doc);
    public void      Clear();
    public void      MarkSaved();          // record current position as "clean"
}
```

Internally: `List<IUndoableEdit>` for undo (bottom = oldest, so we evict from
index 0), `Stack<IUndoableEdit>` for redo, a running `SizeBytes` total, and a
**monotonic position counter** plus a **saved-position marker**. `IsModified` is
`currentPosition != savedPosition` — comparing positions (not list contents)
means evicting already-saved entries never falsely flips the modified flag.

### Document integration

`Document` (partial class, new file `Document.History.cs`) gains:

- `DirtyRect ReadRegion(DirtyRect r)` — clamps `r` to bounds and copies its rows
  out of `Pixels` into a packed `byte[]`, honoring `Stride`.
- `void WriteRegion(DirtyRect r, ReadOnlySpan<byte> src)` — the inverse.
- `History History { get; }` — an owned instance, created with the Document.
- `DirtyRect Undo()` / `DirtyRect Redo()` — delegate to `History`.
- `void PushStrokeEdit(BrushEngine engine, DirtyRect strokeDirty)` — see below.
- A `Clear` path that records a `FillEdit`.
- `bool IsModified { get; }` and `void MarkSaved()` — delegate to `History`.

Reuse the existing `DirtyRect` (`app/Prima.App/Brush.cs`) for all region
addressing; do not introduce a new rect type.

### The one interop change: reading the stroke baseline

To undo a stroke we need the pixels of the final dirty rect **as they were before
the stroke**. Those live in the brush engine's `baseline_` snapshot. Expose a
region of it:

- **Engine** ([brush.h](../../engine/include/prima/brush.h) /
  [brush_engine.cpp](../../engine/src/brush_engine.cpp)): store
  `int baselineWidth_`, `int baselineHeight_` when `beginStroke` snapshots the
  canvas, and add
  `bool readBaselineRegion(int x, int y, int w, int h, uint8_t* dst) const`.
  It returns `false` if `baseline_` is empty or the rect is out of bounds;
  otherwise it copies `h` rows of `w * 4` bytes from `baseline_` starting at
  `(y * baselineWidth_ + x) * 4` into `dst` (packed). Valid after a stroke has
  begun and until the next `beginStroke`.
- **Interop** ([prima_c.h](../../interop/include/prima_c/prima_c.h) /
  [prima_c.cpp](../../interop/src/prima_c.cpp)):
  `int prima_brush_engine_read_baseline_region(PrimaBrushEngine* engine, int x,
  int y, int w, int h, uint8_t* dst)` — returns `0` on success, non-zero on a bad
  rect.
- **C#** ([BrushEngine.cs](../../app/Prima.App/BrushEngine.cs)):
  `byte[] ReadBaselineRegion(DirtyRect r)` — allocates `r.Height * r.Width * 4`,
  pins, calls the native fn, returns the buffer (empty rect → empty array).

### Stroke → edit flow

During a stroke the UI accumulates the stroke's **total** dirty rect (the union
of every `AddSamples` and `EndStroke` result) in a `_strokeDirty` field. On
pointer release, after `EndStroke`, it calls the app-layer helper:

```csharp
// Document.PushStrokeEdit(engine, strokeDirty)
if (strokeDirty.IsEmpty) return;                       // taps that touched nothing
byte[] before = engine.ReadBaselineRegion(strokeDirty); // pre-stroke pixels
byte[] after  = ReadRegion(strokeDirty);                // post-stroke pixels
History.Push(new RegionEdit(strokeDirty, before, after, "Brush stroke"));
```

Keeping this in the app layer means the CLI host can record stroke edits too.

### UI wiring

- `Ctrl+Z` → `Document.Undo()`, `Ctrl+Shift+Z` / `Ctrl+Y` → `Document.Redo()`.
  Re-render the returned dirty rect (partial render, falling back to a full render
  if the rect is empty).
- Subscribe to `History.Changed` to update the window title's modified marker and
  the enabled state of undo/redo/save affordances.

## Integration with saving / loading projects

Today "save" is raster **export** (`SaveAsPng` / `SaveAsJpeg` in
[Document.IO.cs](../../app/Prima.App/Document.IO.cs)) and "load" is image
**import** (`Document.LoadFromFile` builds a *new* `Document`). A native project
file format is Milestone 2 and does not exist yet, and there is no
"modified since save" tracking. The undo design leaves clean seams for both:

1. **History is per-Document and session-scoped.** `History` is owned by the
   `Document`, so `LoadFromFile` / `new Document` automatically start with empty
   history — no stale undo entries survive an open/new. The UI's `Document` setter
   already disposes the old document.
2. **Saved marker drives `IsModified`.** `History.MarkSaved()` records the current
   position; `IsModified` is true whenever the current position differs from it.
   Undoing back to the saved point clears the flag (a genuinely pristine state).
   This is the single source of truth for the title-bar "•", the enabled state of
   Save, and the "discard unsaved changes?" prompt on new/open/close.
3. **Flush the active stroke before saving.** Saving mid-stroke must first
   `EndStroke` and push the stroke edit so the file and the history agree. The UI
   Save handler calls a small "commit pending stroke" step before writing.
4. **v1 does not serialize history into files.** Export (PNG/JPEG) is pixels only.
   When the Milestone-2 project format lands it persists the authoritative canvas
   state (pixels/tiles) and load starts with a clean, empty history — matching how
   Photoshop and Krita treat undo. The project save/load path just calls
   `MarkSaved()`.
5. **Optional future: persisted cross-session undo.** The command-replay /
   journaling RAM option (below) doubles as the mechanism here — a project file
   could store a bounded input-sample/command journal so undo survives reopen. Not
   built in v1; the `IUndoableEdit` abstraction is shaped so a journal-backed edit
   can implement the same interface later.

Net effect: the Milestone-2 project-format work only has to call `MarkSaved()`
(and optionally `History.Clear()`); nothing in the undo core changes.

## RAM reduction options

A full-canvas snapshot per step is ~8 MB at 1920×1080 — 200 steps would be
~1.6 GB. v1 avoids that. Options, in order of what we build vs. document:

1. **Region snapshots** — *implemented in v1.* Store only the exact dirty-rect
   pixels, not the whole canvas. A typical stroke touches a small fraction of the
   canvas, so entries are usually kilobytes, not megabytes.
2. **Memory budget + depth cap with oldest-first eviction** — *implemented in v1.*
   Bound total history bytes (`MemoryBudgetBytes`) and entry count (`MaxDepth`);
   drop the oldest undo entries when either is exceeded.
3. **Region compression** — *future, easy opt-in.* Deflate via built-in
   `System.IO.Compression`, or LZ4/zstd via a native helper. `RegionEdit` can hold
   compressed bytes plus a flag; painted regions compress well.
4. **Cold-tier disk spill** — *future.* Page the oldest region buffers out to the
   scratch/temp directory and reload them on undo. Keeps hot entries in RAM while
   allowing effectively unbounded depth.
5. **Command replay / journaling** — *future.* Store `InputSample[]` +
   `BrushParams` instead of pixels; redo replays the stroke, undo restores the
   nearest keyframe and replays forward. Lowest RAM, highest CPU; relies on the
   engine's deterministic replay (already covered by `BrushEngineTests`). Also the
   basis for persisted cross-session undo (see save/load §5).
6. **Delta / XOR + RLE** — *future.* Store only the pixels that actually changed
   within the rect. Cheap for thin strokes over flat areas.
7. **Tile-based copy-on-write baseline** — *future, Milestone 2.* When the canvas
   becomes tiled, snapshot only touched tiles and share unchanged ones; undo
   stores tile deltas. Aligns with the deferred tiled-canvas work.
8. **Edit coalescing** — *future.* Merge rapid consecutive small edits into one
   history entry to cut per-entry overhead.

## Task breakdown

Ordered and self-contained; each names its files and an acceptance check. Tasks
1–9 are the v1 core; 10–13 finish integration, wiring, tests, and docs.

1. **Engine — expose baseline region.**
   In [brush.h](../../engine/include/prima/brush.h) add
   `int baselineWidth_ = 0, baselineHeight_ = 0;` and
   `bool readBaselineRegion(int x, int y, int w, int h, uint8_t* dst) const`.
   In [brush_engine.cpp](../../engine/src/brush_engine.cpp) set the two dims in
   `beginStroke` (from `canvas.width()/height()`) and implement the copy: reject
   if `baseline_` is empty or the rect is out of `[0,w)×[0,h)`; otherwise copy `h`
   rows of `w * 4` bytes from `baseline_` at offset `(y * baselineWidth_ + x) * 4`
   into `dst`.
   *Accept:* a new GoogleTest paints a stroke, reads a sub-rect via
   `readBaselineRegion`, and asserts it equals the pre-stroke pixels; an
   out-of-bounds rect returns `false`.

2. **Interop — new ABI function.**
   Declare `prima_brush_engine_read_baseline_region` in
   [prima_c.h](../../interop/include/prima_c/prima_c.h) and implement it in
   [prima_c.cpp](../../interop/src/prima_c.cpp) (cast the opaque handle to
   `BrushEngine*`, forward, return `0` on success / non-zero on a bad rect).

3. **P/Invoke binding.**
   Add the `[LibraryImport]` for it in
   [NativeMethods.cs](../../app/Prima.App/NativeMethods.cs), mirroring the C
   signature (`nint engine, int x, int y, int w, int h, byte* dst` → `int`).

4. **C# `BrushEngine.ReadBaselineRegion(DirtyRect)`.**
   In [BrushEngine.cs](../../app/Prima.App/BrushEngine.cs) allocate
   `r.Height * r.Width * 4`, pin it, call the native fn, and return it; an empty
   rect returns an empty array. Throw if the native call reports failure.

5. **Document region helpers.**
   Add `ReadRegion(DirtyRect)` and `WriteRegion(DirtyRect, ReadOnlySpan<byte>)` to
   [Document.cs](../../app/Prima.App/Document.cs) using the `Pixels` span and
   `Stride`, reusing `DirtyRect` for addressing. Clamp the rect to canvas bounds.
   *Accept:* xUnit round-trip — write a known pattern, read it back, assert equal.

6. **Edit types.**
   New file `app/Prima.App/History/Edits.cs` with `IUndoableEdit`, `RegionEdit`,
   `FillEdit`, and `CompositeEdit`, each implementing `Undo`/`Redo` in terms of
   `Document.WriteRegion` / `Document.Clear`.

7. **`History` class.**
   New file `app/Prima.App/History/History.cs`: undo list + redo stack, running
   `SizeBytes`, budget/depth eviction from the oldest end, `Changed` event,
   `Push`/`Undo`/`Redo`/`Clear`, plus the saved marker (`MarkSaved()`,
   `IsModified`, position sequence id).
   *Accept:* xUnit — pushing past `MaxDepth`/budget evicts oldest; `Undo`/`Redo`
   move entries between the list and the stack; `Push` clears redo.

8. **Document ↔ History.**
   New partial file `app/Prima.App/Document.History.cs` adding the `History`
   property, `Undo()`/`Redo()`, `PushStrokeEdit(BrushEngine, DirtyRect)`, and
   `IsModified`/`MarkSaved()`.

9. **Undoable `Clear`.**
   Add a `Clear` path that snapshots the whole canvas into a `FillEdit` (before +
   color) and pushes it, so clearing is reversible.

10. **Save/load integration.**
    Call `MarkSaved()` on success in `SaveAsPng`/`SaveAsJpeg`
    ([Document.IO.cs](../../app/Prima.App/Document.IO.cs)). Confirm a freshly
    created/loaded `Document` reports `IsModified == false` with empty history
    (headless test). Provide the "commit pending stroke" step the UI Save handler
    calls before writing.

11. **UI wiring.**
    In [CanvasControl.cs](../../ui/Prima.Desktop/Controls/CanvasControl.cs):
    accumulate `_strokeDirty` across the stroke; on pointer release call
    `Document.PushStrokeEdit(...)`; add `Ctrl+Z` / `Ctrl+Shift+Z` / `Ctrl+Y`
    handlers that undo/redo and re-render the returned rect. In
    [MainWindow.axaml.cs](../../ui/Prima.Desktop/MainWindow.axaml.cs): reflect
    `IsModified` in the window title, call `MarkSaved()` after a successful save,
    and prompt to discard unsaved changes on new/open/close (subscribe to
    `History.Changed`).

12. **Tests.**
    xUnit in `tests/app/Prima.App.Tests`: `RegionEdit` round-trip (paint →
    capture → undo restores exact bytes → redo reapplies), budget/depth eviction,
    `FillEdit`/clear undo, a multi-stroke undo/redo sequence, and `IsModified` /
    `MarkSaved` transitions (edit → modified; save → clean; undo back to the saved
    point → clean) — all headless. Plus the GoogleTest from Task 1.

13. **Docs.**
    Tick the Undo/redo box in [ROADMAP.md](../../ROADMAP.md); add an undo
    architecture bullet to the CLAUDE.md **Decisions** section (noting the saved
    marker is the seam for the Milestone-2 project format); delete this file when
    the feature ships.

## Verification

- `./build.ps1` then `./test.ps1` — CTest (baseline-region GoogleTest) plus
  `dotnet test` (region round-trip, budget eviction, clear undo, multi-stroke,
  modified-marker transitions).
- **Manual (drawing):** run the desktop app, paint several strokes, `Ctrl+Z`
  repeatedly to peel them off, `Ctrl+Y` / `Ctrl+Shift+Z` to reapply; confirm
  pixels and the dirty-rect re-render are correct; `Clear` then undo restores the
  drawing.
- **Manual (save/load):** paint → title shows modified → Save → modified clears;
  paint again → undo back to the saved point → modified clears; new/open with
  unsaved changes → discard prompt appears; a freshly loaded image starts clean
  with empty history.
