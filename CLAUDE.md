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

## Performance tenets

- Startup and shutdown paths are budgeted: lazy-initialize everything possible, no blocking I/O or heavy allocation on the startup path, state persistence must be fast to write on exit.
- The UI thread never blocks on engine work. Long-running engine operations are asynchronous and cancellable.
- Prefer designs that keep the hot path (stroke input → render) short and allocation-free.

## Testing policy

- The engine gets C++ unit tests; the application layer gets .NET unit tests (frameworks TBD — see Open decisions).
- Headless-first design exists partly to enable this: integration tests exercise the full app layer + engine stack without any UI.
- Tests must be fast and runnable with a single command. Every change lands with tests.

## Working conventions (parallel agents)

Multiple agents work on this codebase concurrently. To keep that safe:

- Top-level areas have clear ownership boundaries: `engine/` (C++), `interop/`, `app/` (C#), `ui/`, `tests/`. Keep a change within one layer where possible.
- Components stay small and decoupled; a change in one layer should not ripple into others. The interop boundary is the contract — change it deliberately and rarely.
- Build and test commands must be deterministic and scriptable (document them here once scaffolding exists).
- Keep the repo worktree-friendly: no machine-specific absolute paths in config, all generated files gitignored.
- When a structural or architectural decision lands, update this file in the same change.

## Open decisions

- **UI framework** — Avalonia vs .NET MAUI vs custom native shells. Criteria: startup time, ability to embed a custom high-performance canvas/render surface, desktop coverage first, mobile path second.
- **C++ build system and test framework** — CMake is the likely default; GoogleTest vs Catch2 undecided.
- **.NET test framework** — xUnit vs NUnit.
- **Interop details** — exact ABI conventions, buffer-sharing mechanism, error propagation across the boundary.
- **Mobile targets** — deferred.
- **Version control** — the repo is not yet under git. Run `git init` when scaffolding starts; git is also what enables worktree-based parallel agent work.

## Current state

Vision-only. No code, build system, or git repository exists yet — do not search for implementations that aren't there. First implementation session should scaffold the repo skeleton per the layout above and replace this section.
