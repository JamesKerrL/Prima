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

## Working conventions (parallel agents)

Multiple agents work on this codebase concurrently. To keep that safe:

- Top-level areas have clear ownership boundaries: `engine/` (C++), `interop/`, `app/` (C#), `ui/`, `tests/`. Keep a change within one layer where possible.
- Components stay small and decoupled; a change in one layer should not ripple into others. The interop boundary is the contract — change it deliberately and rarely.
- Build and test commands must be deterministic and scriptable (document them here once scaffolding exists).
- Keep the repo worktree-friendly: no machine-specific absolute paths in config, all generated files gitignored.
- When a structural or architectural decision lands, update this file in the same change.

## Decisions

- **UI framework** — Avalonia. Criteria: startup time, ability to embed a custom high-performance canvas/render surface, desktop coverage first.
- **C++ build system and test framework** — CMake; GoogleTest.
- **.NET test framework** — xUnit.
- **C++ toolchain (this machine)** — MinGW-w64 (WinLibs GCC, UCRT). `prima_c.dll` links the runtimes statically (`-static`), so it depends only on system/UCRT DLLs.
- **Interop mechanism** — opaque `PrimaCanvas*` handle across a `extern "C"` ABI; C# uses `LibraryImport` (source-generated P/Invoke). Pixels shared via `prima_canvas_pixels` (pointer into the engine's own buffer), never copied across the boundary.
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

Milestone 0 (walking skeleton) is complete and verified end to end: create canvas → brush dab → render, exercised through the UI (mouse), the headless CLI, and tests at every layer. Next: **Milestone 1 — drawing core** (brush engine, layers, undo/redo, pan/zoom, color) per the plan.
