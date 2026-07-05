# Prima

## Vision

Prima is a cross-platform application for digital drawing and photo manipulation. A high-performance C++ engine handles rendering and image algorithms; a C# layer provides application functionality on top of it.

Non-negotiable product qualities:

- **Extreme responsiveness** — the app must feel instant. Fast startup and fast shutdown are design constraints, not optimizations to add later.
- **Headless-capable** — everything below the UI shell must run without a display.
- **High test coverage** — across both the C++ engine and the C# application layer.

Target platforms: Windows, macOS, and Linux desktop. Mobile (iOS/Android) is a possible future target and should not be designed out.

## Architecture principles

Strict layering, top to bottom:

1. **UI shell** — thin, replaceable frontend. No business logic lives here.
2. **C# application layer** — commands, tools, session state, undo/redo, file I/O orchestration. Fully usable headlessly; a CLI/headless host is a first-class consumer of this layer, not an afterthought.
3. **Interop boundary** — narrow, C-style ABI between C# and C++ (P/Invoke-friendly). Coarse-grained calls only; never chatty per-pixel traffic across the boundary. Pixel data crosses via shared buffers, not marshaled copies.
4. **C++ engine** — rendering, image algorithms, document/canvas model. No knowledge of the UI or of C#.

Dependencies point downward only. The engine never calls up; the UI never reaches past the application layer into the engine.

## GUI conventions

- **Consistent style and color palette** — a single shared theme (colors, spacing, typography) drives every surface in the app. No screen or panel defines its own one-off colors or styling.
- **Reusable components** — GUI elements are built as shared, composable components rather than bespoke per-screen markup. If a control is needed twice, it belongs in the shared component set, not copy-pasted.
- There is a UI mockup follow only loosely
- **Undo/redo is a first-class concern for every feature that mutates the document.** When adding any UI feature or command, decide explicitly which of two buckets it falls in and say so in the PR/commit:
  - *Document state* (canvas pixels, and later layers, selections, transforms, filters, layer order/visibility/opacity, document metadata) — MUST be recorded as an `IUndoableEdit` through the per-`Document` `History` so a single Ctrl+Z reverts it. Coalesce continuous gestures (slider/handle drags) into one edit on gesture-end, not one per value change.
  - *Tool/UI state* (active brush color, selected tool, brush size, zoom/pan, panel layout, palette/swatch recall) — intentionally NOT on the document undo stack; that matches standard drawing apps. Give it its own recall mechanism if useful (e.g. the color picker's own `ColorHistory`), but keep it out of `History`.
  - If unsure which bucket applies, treat "does the exported image change?" as the test: yes → undoable document edit; no → tool state.

## Performance tenets

- Startup and shutdown paths are budgeted: lazy-initialize everything possible, no blocking I/O or heavy allocation on the startup path, state persistence must be fast to write on exit.
- The UI thread never blocks on engine work. Long-running engine operations are asynchronous and cancellable.
- Prefer designs that keep the hot path (stroke input → render) short and allocation-free.

## Testing policy

- The engine gets C++ unit tests via GoogleTest; the application layer gets .NET unit tests via xUnit.
- Headless-first design exists partly to enable this: integration tests exercise the full app layer + engine stack without any UI.
- Tests must be fast and runnable with a single command. Every change lands with tests.

## Working conventions (parallel agents)

Multiple agents work on this codebase concurrently. To keep that safe:

- make work trees with sensible names for each feature

- Top-level areas have clear ownership boundaries: `engine/` (C++), `interop/`, `app/` (C#), `ui/`, `tests/`. Keep a change within one layer where possible.
- Components stay small and decoupled; a change in one layer should not ripple into others. The interop boundary is the contract — change it deliberately and rarely.
- Build and test commands must be deterministic and scriptable (document them here once scaffolding exists).
- Keep the repo worktree-friendly: no machine-specific absolute paths in config, all generated files gitignored.
- When a structural or architectural decision lands, update this file in the same change.
- Active feature plans live in `docs/features/<slug>.md`; delete a plan when its feature ships and its [ROADMAP.md](ROADMAP.md) box is checked (git history retains it). `ROADMAP.md` is the backlog; `docs/features/` is only in-flight work.

## Logging conventions

When debugging or tracing behavior:

- Prefix debug logs with `// DEBUG:` (C++) or `// DEBUG: ` (C#) to make them easy to find and delete later.
- Use logs liberally during development to understand control flow, state changes, and data values—they're temporary aids.
- Before committing, do a final grep for `DEBUG:` and remove the logs you no longer need. Intentional debug output belongs in a structured logging system (deferred).
- Logs left behind with the debug prefix are fair game for later cleanup—they're acknowledged as temporary, not production code.

## Decisions

- **UI framework** — Avalonia. Criteria: startup time, ability to embed a custom high-performance canvas/render surface, desktop coverage first.
- **C++ build system and test framework** — CMake; GoogleTest.
- **.NET test framework** — xUnit.
- **C++ toolchain (this machine)** — MinGW-w64 (WinLibs GCC, UCRT). `prima_c.dll` links the runtimes statically (`-static`), so it depends only on system/UCRT DLLs.
- **Interop mechanism** — opaque `PrimaCanvas*` handle across a `extern "C"` ABI; C# uses `LibraryImport` (source-generated P/Invoke). Pixels shared via `prima_canvas_pixels` (pointer into the engine's own buffer), never copied across the boundary.
- **Render backend** — the engine owns rendering behind an abstract `Renderer` (`engine/include/prima/renderer.h`); backends are pluggable. Backend #1 is `SoftwareRenderer` (CPU, headless-testable). A `Viewport` (pan/zoom) maps target→canvas in the engine. The UI presents by rendering into its own bitmap buffer (zero-copy target). GPU backends slot in behind the same interface later — OpenGL (Avalonia `OpenGlControlBase`) first, then Vulkan/Metal via composition-surface texture interop.
- **Brush engine** — stateful `BrushEngine` (engine-side) with `beginStroke/addSamples/endStroke` behind its own interop handle `PrimaBrushEngine`. Stroke model: linear interpolation, analytic AA (smoothstep distance falloff), 16-bit per-stroke coverage buffer composited against a begin-stroke canvas snapshot (prevents self-overlap darkening). Flow builds up (airbrush), opacity caps the stroke. Engine algorithm units are pure and isolated: `DabEmitter` (pixel-free spacing walk, batching-invariant), `RoundDabSource` (coverage stamp), pure math kernels in `brush_math.h`. The hot path is allocation-free; scratch buffers are pooled per-`BrushEngine`.
- **Undo/redo** — app-layer `History` (`app/Prima.App/History/`) owned per-`Document`; a generic `IUndoableEdit` (`RegionEdit`, `FillEdit`, `CompositeEdit`) keeps it extensible to future ops (layers, filters, transforms). Edits store only the dirty-rect region's before/after pixels (not the whole canvas), bounded by a memory/depth budget that evicts the oldest entries first. Stroke "before" pixels are read straight out of the brush engine's existing begin-stroke baseline snapshot via `prima_brush_engine_read_baseline_region` — no extra full-canvas copy on the hot path. `History` also tracks a saved-position marker (`IsModified`/`MarkSaved()`) — the seam the eventual Milestone-2 project file format hooks into for dirty-state tracking. RAM-reduction options considered but deferred (compression, disk spill, command-replay journaling, tile-based COW, edit coalescing) live in git history at the now-removed `docs/features/undo-redo.md`.
- **Flood fill (paint bucket)** — pure, Canvas-free engine algorithm `floodFill()` (`engine/include/prima/flood_fill.h`): contiguous 4-connected scanline fill over a raw RGBA8 buffer with an explicit stack (no recursion) and a `visited` mask (correct even when the fill color still matches the seed within tolerance). Per-channel `tolerance` (0 = exact) is designed through every layer but the UI passes 0 (no tolerance slider yet). Exposed coarse-grained as `prima_canvas_flood_fill` (void + `PrimaRect` out-dirty, like `prima_stroke_add`). App layer `Document.FloodFill()` records it as a `RegionEdit` on `History` — **document state → undoable** (one Ctrl+Z reverts a fill); it snapshots the whole canvas transiently to recover the dirty region's "before" pixels (fill is a discrete click, not a hot path). UI: `ToolType.FloodFill`, a `ToolPanel` bucket button (vector `Path` icon, theme-driven), single-click dispatch in `CanvasControl`.
- **Version control** — git, initialized. Enables worktree-based parallel agent work.

## Open decisions

- **Error propagation across the boundary** — no status/error channel yet; the ABI is void-returning. Revisit when operations can fail.
- **Mobile targets** — deferred.

## Layout

```
engine/    C++ core (STL only) — Canvas, image algorithms      → static lib prima_engine
interop/   C ABI shim over the engine                          → shared lib prima_c.dll
app/       Prima.App — managed wrapper (Document), no UI deps
host/      Prima.Cli — headless console host: canned demo (PNG/PPM) + `script` mode
           that drives the app layer (new/clear/brush/stroke/undo/redo/pixel/status/save)
           for headless operation & verification
ui/        Prima.Desktop — Avalonia app; shared theme in Themes/Theme.axaml
tests/     engine/ (GoogleTest) + app/Prima.App.Tests (xUnit, drives the real DLL)
```

The native lib is emitted to `build/native/bin/prima_c.dll`; `Directory.Build.props` copies it next to every .NET output so P/Invoke resolves it.

## Build & test

Run from the repo root (both scripts refresh PATH so cmake/gcc are visible):

- `./build.ps1` — CMake+MinGW native build, then `dotnet build Prima.slnx`.
- `./test.ps1` — CTest (C++), then `dotnet test` (xUnit interop/integration).

Requires: .NET 10 SDK, CMake, MinGW-w64 (gcc/g++) on PATH. The solution is `Prima.slnx` (new XML format).

## Current state

Milestone 0 (walking skeleton) is complete and verified end to end: create canvas → brush dab → render, exercised through the UI (mouse), the headless CLI, and tests at every layer.

Milestone 1 brush engine is complete end to end. The parametric round brush
(`DabEmitter` + analytic AA + coverage buffer with flow/opacity semantics +
pressure→size/flow + dirty rects) ships across all layers: C++ engine tests,
interop ABI (5 functions), C# `BrushEngine` wrapper, xUnit app-layer tests, CLI
headless demo, and UI wiring (pointer capture, `GetIntermediatePoints`, pen
pressure, dirty-rect partial bitmap render). Catmull-Rom smoothing,
tilt/rotation/speed dynamics, stamp/image brushes, GPU dab rendering, and the
tiled canvas baseline copy-on-write are deferred and explicitly designed for.

See [ROADMAP.md](ROADMAP.md) for planned features and what's next.
