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

## Performance tenets

- Startup and shutdown paths are budgeted: lazy-initialize everything possible, no blocking I/O or heavy allocation on the startup path, state persistence must be fast to write on exit.
- The UI thread never blocks on engine work. Long-running engine operations are asynchronous and cancellable.
- Prefer designs that keep the hot path (stroke input → render) short and allocation-free.

## Testing policy

- The engine gets C++ unit tests via GoogleTest; the application layer gets .NET unit tests via xUnit.
- Headless-first design exists partly to enable this: integration tests exercise the full app layer + engine stack without any UI.
- Tests must be fast and runnable with a single command. Every change lands with tests.
- **Pixel algorithms (brush, fill, and future tools) get visual PNG tests, not just numeric assertions.** Numeric unit tests catch regressions in known values but don't show whether output actually *looks* right. Add `DISABLED_Visual*` GoogleTest cases (see `tests/engine/brush_engine_test.cpp`) that render a representative operation to a PNG via `saveImagePng` for human inspection; run them explicitly with `--gtest_also_run_disabled_tests --gtest_filter="*Visual*"`. When simulating input-driven tools (brush strokes, etc.), model realistic fast-input conditions (few, widely-spaced raw samples) as well as slow/dense ones — sparse input exercises different code paths (e.g. interpolation/smoothing) than densely-sampled input and can hide bugs the dense case never reaches.

## Working conventions (parallel agents)

Multiple agents work on this codebase concurrently. To keep that safe:

- Top-level areas have clear ownership boundaries: `engine/` (C++), `interop/`, `app/` (C#), `ui/`, `tests/`. Keep a change within one layer where possible.
- Components stay small and decoupled; a change in one layer should not ripple into others. The interop boundary is the contract — change it deliberately and rarely.
- Build and test commands must be deterministic and scriptable (document them here once scaffolding exists).
- Keep the repo worktree-friendly: no machine-specific absolute paths in config, all generated files gitignored.
- When a structural or architectural decision lands, update this file in the same change.
- Active feature plans live in `docs/features/<slug>.md`; delete a plan when its feature ships and its [ROADMAP.md](ROADMAP.md) box is checked (git history retains it). `ROADMAP.md` is the backlog; `docs/features/` is only in-flight work.

## Decisions

- **UI framework** — Avalonia. Criteria: startup time, ability to embed a custom high-performance canvas/render surface, desktop coverage first.
- **C++ build system and test framework** — CMake; GoogleTest.
- **.NET test framework** — xUnit.
- **C++ toolchain (this machine)** — MinGW-w64 (WinLibs GCC, UCRT). `prima_c.dll` links the runtimes statically (`-static`), so it depends only on system/UCRT DLLs.
- **Interop mechanism** — opaque `PrimaCanvas*` handle across a `extern "C"` ABI; C# uses `LibraryImport` (source-generated P/Invoke). Pixels shared via `prima_canvas_pixels` (pointer into the engine's own buffer), never copied across the boundary.
- **Render backend** — the engine owns rendering behind an abstract `Renderer` (`engine/include/prima/renderer.h`); backends are pluggable. Backend #1 is `SoftwareRenderer` (CPU, headless-testable). A `Viewport` (pan/zoom) maps target→canvas in the engine. The UI presents by rendering into its own bitmap buffer (zero-copy target). GPU backends slot in behind the same interface later — OpenGL (Avalonia `OpenGlControlBase`) first, then Vulkan/Metal via composition-surface texture interop.
- **Brush engine** — stateful `BrushEngine` (engine-side) with `beginStroke/addSamples/endStroke` behind its own interop handle `PrimaBrushEngine`. Stroke model: Catmull-Rom curve fit through raw input samples (each pending segment `[p1,p2]` is finalized once the next sample supplies `p3` as its tangent neighbor, with duplicated end caps at stroke start/end), subdivided into micro-segments walked by the original linear spacing logic — this keeps dab placement/spacing math untouched while fixing corner-chording on fast strokes with sparse raw samples. Analytic AA (smoothstep distance falloff), 16-bit per-stroke coverage buffer composited against a begin-stroke canvas snapshot (prevents self-overlap darkening). Flow builds up (airbrush), opacity caps the stroke. Engine algorithm units are pure and isolated: `DabEmitter` (pixel-free spacing walk, batching-invariant), `RoundDabSource` (coverage stamp), pure math kernels in `brush_math.h` (including `catmullRom`). The hot path is allocation-free; scratch buffers are pooled per-`BrushEngine`.
- **Version control** — git, initialized. Enables worktree-based parallel agent work.

## Open decisions

- **Error propagation across the boundary** — no status/error channel yet; the ABI is void-returning. Revisit when operations can fail.
- **Mobile targets** — deferred.

## Layout

```
engine/    C++ core (STL only) — Canvas, image algorithms      → static lib prima_engine
interop/   C ABI shim over the engine                          → shared lib prima_c.dll
app/       Prima.App — managed wrapper (Document), no UI deps
host/      Prima.Cli — headless console host (writes a PPM)
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
pressure, dirty-rect partial bitmap render). `DabEmitter` Catmull-Rom-fits a
curve through raw input samples so fast strokes with sparse, widely-spaced
samples round off direction changes smoothly instead of chording into sharp
corners. Tilt/rotation/speed dynamics, stamp/image brushes, GPU dab rendering,
and the tiled canvas baseline copy-on-write are deferred and explicitly
designed for.

See [ROADMAP.md](ROADMAP.md) for planned features and what's next.
