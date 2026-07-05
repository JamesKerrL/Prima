# Brush System — presets, selection UI, sharing, design tool

Status: **planned — not yet implemented.** Builds on the completed Milestone 1
brush engine (`BrushEngine` + `BrushParams`, working end to end through
interop, `Prima.App`, and `CanvasControl`).

## Goals

1. **Savable brushes** — named presets persisted on disk, surviving restarts.
2. **Brush selection UI** — a panel to browse all brushes and curate a personal
   "quick brushes" list (favorites, user-ordered).
3. **Stroke previews** — each brush in the UI shows a rendered sample stroke.
4. **Shareable brushes** — export/import preset files so users can exchange
   brushes.
5. **Brush shape feedback** — the canvas cursor shows the brush's outline
   (size/shape at current zoom) instead of a generic pointer.
6. **Brush design tool** — an editor to tweak every parameter with a live
   preview and save the result as a preset.

### Design decisions (fixed up front)

- **A preset stores shape/dynamics only, never color.** Color always comes from
  the color picker at stroke time. (An opt-in "include color" flag can come
  later; do not build it now.)
- **Preset file format is JSON with a `formatVersion` field** (`.primabrush`
  extension). Same file for local storage and sharing — sharing is just
  copying the file. Readers ignore unknown fields and refuse files with a
  newer major version. This lets stamp/texture brushes extend the format later
  without breaking old files.
- **Previews are rendered by the real engine, headlessly.** No hand-drawn
  thumbnail art: a preview is a canned stroke run through `BrushEngine` on a
  small canvas. This keeps previews truthful and testable without a display.
- **Everything below the panel widgets lives in `Prima.App`** (preset model,
  serializer, library, preview renderer) so it is headless and xUnit-testable,
  per the architecture rules. No engine or interop changes in any stage.

### Out of scope (all stages)

- Stamp/image/textured brushes, per-brush blend modes (engine work, separate
  feature — the format's `formatVersion` is the extension point).
- Tilt/rotation/speed dynamics (engine already reserves the fields).
- Brush organization beyond one flat list + quick list (folders/tags deferred).
- Online brush marketplace/sync — sharing is file-based only.

## Architecture overview

```
ui/Prima.Desktop
  Controls/BrushPanel        list + quick list + previews (Stage 2/3)
  Controls/BrushCursor       shape outline overlay in CanvasControl (Stage 4)
  Windows/BrushDesigner      parameter editor + live preview (Stage 6)
  Controls/LabeledSlider     shared component (Stage 1) — reused everywhere

app/Prima.App
  BrushPreset                id + name + BrushParams fields (no color)
  BrushPresetSerializer      JSON <-> BrushPreset (.primabrush)
  BrushLibrary               scan/load/save/rename/delete in AppPaths.Brushes
  QuickBrushes               ordered id list, persisted as quickbrushes.json
  StrokePreviewRenderer      canned stroke -> RGBA8 pixels via BrushEngine
```

On-disk layout (extends the existing `AppPaths` pattern used by palettes):

```
%LocalAppData%/Prima/
  brushes/<guid>.primabrush     one preset per file
  quickbrushes.json             ordered list of preset ids
```

`.primabrush` format (v1):

```json
{
  "formatVersion": 1,
  "id": "9f1c...-guid",
  "name": "Soft Airbrush",
  "radius": 24.0,
  "hardness": 0.25,
  "opacity": 0.9,
  "flow": 0.35,
  "spacing": 0.12,
  "sizePressureMin": 0.2,
  "sizePressureGamma": 1.0,
  "flowPressureMin": 0.5,
  "flowPressureGamma": 1.4
}
```

---

## Stage 0 — Foundations (do this first; everything depends on it)

Pure app-layer work, no UI. Each task is small, self-contained, and lands with
xUnit tests. Mirror the existing palette code (`SwatchPalette`,
`PaletteSerializer`, `AppPaths`) — it is the exact precedent.

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 0.1 | `BrushPreset` record: `Guid Id`, `string Name`, plus the nine numeric fields above. Method `ToBrushParams(Rgba color)` producing an engine-ready `BrushParams`, and static `FromBrushParams(name, params)` (drops color). | `app/Prima.App/BrushPreset.cs` | Round-trip `FromBrushParams` → `ToBrushParams` preserves every numeric field; color comes only from the argument. |
| 0.2 | `BrushPresetSerializer`: `Serialize`/`Deserialize` + `SaveToFile`/`LoadFromFile`, modeled 1:1 on `PaletteSerializer.cs`. Writes `formatVersion: 1`. Deserialize ignores unknown JSON fields; throws `FormatException` on missing required fields or `formatVersion > 1`. | `app/Prima.App/BrushPresetSerializer.cs` | Round-trip equals input; unknown-field JSON loads; `formatVersion: 2` file is rejected with a clear message. |
| 0.3 | Extend `AppPaths` with `Brushes` dir and `QuickBrushesJson` path; create the dir in `EnsureDirectories()`. | `app/Prima.App/AppPaths.cs` | Matches existing `Palettes` pattern exactly. |
| 0.4 | `BrushLibrary`: constructor takes a directory path (tests pass a temp dir; app passes `AppPaths.Brushes`). `IReadOnlyList<BrushPreset> Presets`, `Load()` (scan `*.primabrush`, skip corrupt files without throwing), `Save(preset)`, `Rename(id, newName)`, `Delete(id)`, `Get(id)`. File name is `<id>.primabrush`. | `app/Prima.App/BrushLibrary.cs` | Load after Save returns the preset; corrupt file in dir is skipped and the rest load; Delete removes the file. |
| 0.5 | Built-in defaults: `BrushLibrary.SeedDefaultsIfEmpty()` writes 4–6 sensible presets (e.g. Hard Round, Soft Round, Airbrush, Inker) only when the directory has no presets. Fixed well-known ids so re-seeding is idempotent. | `app/Prima.App/BrushLibrary.cs` | Empty dir → seeded once; second call is a no-op; dir with any preset → untouched. |
| 0.6 | `QuickBrushes`: ordered `List<Guid>`, `Add`/`Remove`/`Move(index)`, JSON persistence at a given path, and `Prune(library)` dropping ids that no longer exist. | `app/Prima.App/QuickBrushes.cs` | Order survives save/load; pruning removes dangling ids only. |
| 0.7 | `StrokePreviewRenderer.Render(BrushPreset, Rgba color, int width, int height)` → `byte[]` RGBA8. Creates a `Document`/canvas of that size (white background), builds a fixed S-curve sample path with a 0→1→0.6 pressure ramp scaled to the size, runs it through the real `BrushEngine` (Begin/AddSamples/End), returns the pixels. Deterministic: same inputs, same bytes. | `app/Prima.App/StrokePreviewRenderer.cs` | Output is non-blank (some pixels differ from background); two calls with identical inputs are byte-identical; bigger radius touches more pixels. |

Tests for all of the above go in `tests/app/Prima.App.Tests/` (one test file
per class, following the existing test project layout).

## Stage 1 — Shared UI components (small; unblocks every UI stage)

Per the GUI conventions, controls used by more than one surface are shared
components. Both the brush panel and the designer need these.

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 1.1 | `LabeledSlider` control: label text, min/max, value, optional value-format string (e.g. `0.00` or `px`), fires a value-changed event. Styled from `Themes/Theme.axaml` tokens only — no inline colors. | `ui/Prima.Desktop/Controls/LabeledSlider.axaml` + `.cs` | Reusable via XAML with `Label`/`Minimum`/`Maximum`/`Value` properties; if existing toolbar sliders exist, they can be migrated to it (migration itself is task 1.3). |
| 1.2 | `BrushPreviewImage` control: takes RGBA8 `byte[]` + width/height, blits into a `WriteableBitmap` (same pattern as `CanvasControl`), redraws when the pixels property changes. No engine calls inside the control. | `ui/Prima.Desktop/Controls/BrushPreviewImage.axaml.cs` | Given preview bytes from `StrokePreviewRenderer`, displays the stroke; handles size changes without leaking bitmaps. |
| 1.3 | Migrate any existing ad-hoc brush sliders (size/opacity/etc. in the toolbar or MainWindow) to `LabeledSlider`. | `ui/Prima.Desktop/MainWindow.axaml` (+ wherever sliders live) | Behavior unchanged; one slider implementation remains in the codebase. |

## Stage 2 — Savable brushes + minimal selection panel

First user-visible payoff: save the current brush, pick it again later.

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 2.1 | Wire `BrushLibrary` into app startup: construct with `AppPaths.Brushes`, `EnsureDirectories()`, `SeedDefaultsIfEmpty()`, `Load()`. Keep it off the blocking startup path (lazy or fire-and-forget after first render), per the performance tenets. | `ui/Prima.Desktop/MainWindow.axaml.cs` (or app-level composition root) | Fresh machine: defaults exist on first open of the brush panel; startup time unaffected. |
| 2.2 | `BrushPanel` control, v1: vertical list of preset names from the library; clicking one applies it to the canvas (`CanvasControl.BrushSize/Hardness/Opacity/Flow` + pressure fields — add setters for spacing/pressure fields to `CanvasControl` if missing). Highlight the active preset. | `ui/Prima.Desktop/Controls/BrushPanel.axaml` + `.cs` | Click preset → next stroke uses its parameters; current color is untouched. |
| 2.3 | "Save brush" flow: a button in `BrushPanel` opens a small name dialog, captures current canvas brush settings via `BrushPreset.FromBrushParams`, saves through the library, refreshes the list. | `BrushPanel.axaml.cs` + a tiny `Windows/TextPromptDialog.axaml` (shared component) | New preset appears in the list and survives app restart. |
| 2.4 | Context menu on a preset: Rename (reuses `TextPromptDialog`) and Delete (confirmation). Built-in defaults are deletable like any other preset. | `BrushPanel.axaml.cs` | Rename persists; Delete removes file + list entry and prunes quick list. |
| 2.5 | Dock `BrushPanel` into `MainWindow` (right side, same pattern as the reference-image panel plan) with a View-menu toggle. | `ui/Prima.Desktop/MainWindow.axaml` + `.cs` | Panel toggles; layout doesn't disturb the canvas. |

## Stage 3 — Quick brushes + stroke previews

Turns the flat list into the curated, visual picker.

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 3.1 | Preview cache in the app layer: `BrushPreviewCache` keyed by preset id + a hash of its numeric fields; `GetOrRender(preset, color, w, h)` memoizes `StrokePreviewRenderer` output in memory and invalidates when the hash changes. Renders use a fixed neutral color (theme foreground), not the current paint color. | `app/Prima.App/BrushPreviewCache.cs` + tests | Second call with unchanged preset does not re-render (verifiable via a render-count hook); editing a field re-renders. |
| 3.2 | Show previews in `BrushPanel`: each list row becomes name + `BrushPreviewImage` thumbnail (~180×48). Render previews lazily/async off the UI thread; rows show a blank placeholder until ready. | `BrushPanel.axaml` + `.cs` | Scrolling a long list stays smooth; previews match the preset (soft brush looks soft). |
| 3.3 | Quick-brushes strip: horizontal row of preview thumbnails (backed by `QuickBrushes`) shown at the top of `BrushPanel` (or toolbar). Click = select. | `BrushPanel.axaml` + `.cs` | Quick list persists across restarts; selection works from the strip. |
| 3.4 | Curate the quick list: "add to / remove from quick brushes" on the row context menu, and drag (or up/down buttons — buttons are fine for v1) to reorder. Persist via `QuickBrushes` on every change. | `BrushPanel.axaml.cs` | Order changes survive restart; removing a preset from the library also removes it from the strip. |

## Stage 4 — Brush shape cursor feedback

Independent of stages 2–3; can be built any time after Stage 0 (it only needs
current `BrushParams`, not presets).

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 4.1 | Cursor outline overlay in `CanvasControl`: draw a circle at the pointer position with screen radius = brush radius × viewport zoom, via Avalonia's `DrawingContext` in `Render` (vector overlay on top of the bitmap — not into the canvas pixels). Two-tone stroke (light + dark 1px) so it reads on any background. Hide the OS cursor over the canvas; restore on exit. | `ui/Prima.Desktop/Controls/CanvasControl.cs` | Outline tracks the pointer, scales with zoom and with the size slider; no allocation per pointer-move (cache pens/geometry); stroke rendering latency unaffected. |
| 4.2 | Hardness feedback: when hardness < 1, draw a second inner circle at radius × hardness with a fainter stroke, visualizing the falloff band. Below ~4px screen radius, draw a small crosshair instead of circles. | `CanvasControl.cs` | Soft brushes visibly differ from hard ones; tiny brushes stay locatable. |
| 4.3 | Keep the overlay honest during a stroke: while pressure varies, the outline keeps showing the *base* radius (not per-sample resolved radius) — document this choice in a comment; hide the outline while panning. | `CanvasControl.cs` | No flicker or lag while drawing fast. |

## Stage 5 — Sharing (export/import)

Small stage; the format work in Stage 0 did the heavy lifting.

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 5.1 | `BrushLibrary.Import(path)`: load an external `.primabrush`; if the id already exists with different content, assign a fresh id (never overwrite silently); if identical, no-op. Returns the imported preset. App-layer + tests. | `app/Prima.App/BrushLibrary.cs` + tests | Importing the same file twice yields one preset; importing a modified copy yields a second preset; bad file throws `FormatException`. |
| 5.2 | UI: "Import brush…" (file dialog, multi-select) and per-preset "Export brush…" (save dialog, default name `<Name>.primabrush`) in `BrushPanel`. | `BrushPanel.axaml.cs` | Export → import on a second machine (or after deleting) reproduces the identical brush; errors surface as a message, not a crash. |

## Stage 6 — Brush design tool

The full editor. Everything it needs already exists: `LabeledSlider`,
`BrushPreviewImage`, `StrokePreviewRenderer`, `BrushLibrary`.

| # | Task | Files | Acceptance criteria |
|---|---|---|---|
| 6.1 | `BrushDesigner` window skeleton: opened from `BrushPanel` ("New brush" and per-preset "Edit…"). Holds a working `BrushPreset` copy (edits don't touch the library until saved). Large `BrushPreviewImage` on top, parameter area below, Save / Save as copy / Cancel buttons. | `ui/Prima.Desktop/Windows/BrushDesigner.axaml` + `.cs` | Opens pre-filled from the selected preset or from defaults for "New". Cancel discards. |
| 6.2 | Parameter sliders: `LabeledSlider`s for radius (0.3–200, log scale if easy — linear acceptable), hardness, opacity, flow (0–1), spacing (0.02–1). | `BrushDesigner.axaml` + `.cs` | Every slider edits the working copy and is reflected in the preview. |
| 6.3 | Pressure-dynamics section: sliders for `sizePressureMin`, `sizePressureGamma`, `flowPressureMin`, `flowPressureGamma`, grouped under "Size dynamics" / "Flow dynamics" headers. (The preview's built-in pressure ramp makes their effect visible.) | `BrushDesigner.axaml` + `.cs` | Setting sizePressureMin low visibly tapers the preview stroke ends. |
| 6.4 | Live preview: on any change, re-render via `StrokePreviewRenderer` debounced (~50 ms) on a background thread, then update `BrushPreviewImage` on the UI thread. Never block input. | `BrushDesigner.axaml.cs` | Dragging a slider stays fluid; preview settles immediately after release. |
| 6.5 | Save semantics: **Save** overwrites the edited preset (or creates, for "New"); **Save as copy** always creates with a fresh id and prompts for a name. `BrushPanel` refreshes, preview cache invalidates (hash change handles it). | `BrushDesigner.axaml.cs`, `BrushPanel.axaml.cs` | Edits appear in the panel with an updated thumbnail; other presets untouched. |
| 6.6 | Scratch pad strip (nice-to-have, do last): a mini paintable canvas inside the designer — its own small `Document` + `BrushEngine`, reusing `CanvasControl` if it composes cleanly, else a stripped-down copy of its pointer→stroke wiring. Clear button. | `BrushDesigner.axaml` + `.cs` | User can doodle with the in-progress brush; skippable without blocking 6.1–6.5. |

---

## Sequencing & independence

```
Stage 0 (app-layer foundations)
   ├── Stage 1 (shared UI controls)
   │      ├── Stage 2 (save + basic panel)
   │      │      ├── Stage 3 (quick list + previews)
   │      │      └── Stage 5 (sharing)      ← only needs 2
   │      └── Stage 6 (designer)            ← needs 1 + 2 (list refresh)
   └── Stage 4 (cursor feedback)            ← independent of 1/2/3/5/6
```

Stop-anywhere points: after Stage 2 you have savable brushes; after Stage 3
the full selection UX; 4, 5, 6 are each independently shippable increments.

## Notes for implementers (simpler-LLM guardrails)

- Copy existing patterns before inventing: `PaletteSerializer` for
  serialization, `RecentPalettes`/`SwatchPalette` for library-ish state,
  `CanvasControl`'s `WriteableBitmap` blit for previews, the reference-image
  plan (`docs/features/reference-image.md`) for panel docking.
- Never call `NativeMethods` directly from UI code — go through the
  `Prima.App` wrappers (`BrushEngine`, `Document`).
- No engine (`engine/`) or interop (`interop/`) changes in any stage. If a
  task seems to need one, stop and flag it.
- All colors/spacing in new XAML come from `Themes/Theme.axaml` resources.
- Every app-layer task lands with xUnit tests in
  `tests/app/Prima.App.Tests/`; run `./test.ps1` before considering a task
  done. UI tasks are verified manually (`dotnet run` in `ui/Prima.Desktop`).
- File paths in tests: always use temp directories, never `AppPaths` real
  locations.
