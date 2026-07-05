# Prima Roadmap

Planned features, in rough priority order. This is a living backlog — edit
freely (add, reorder, check off, delete). See [CLAUDE.md](CLAUDE.md) for
architecture/decisions; this file is just what to build next.

Check off items as they land: `- [x] done thing`.

## Milestone

## Milestone 1 — Drawing core (next up)

- [ ] Real brush engine: size, hardness, opacity, spacing along a stroke (not
      just one dab)
- [ ] Layers: add/remove/reorder, per-layer opacity, blend modes
- [ ] Undo/redo (command pattern in the app layer)
- [x] Canvas pan/zoom (delivered via the render-backend work below)
- [ ] Color picker + palette (use open source color library if necessary)

## Milestone 1a — Canvas view & render backend (in progress)

- [x] Engine-side `Renderer` abstraction + software (CPU) backend
- [x] `Viewport` (pan/zoom) mapping in the engine, headless-tested
- [x] Reusable Avalonia `CanvasControl` (pan/zoom/paint) on a physical-pixel bitmap
- [ ] GPU backend #1: OpenGL via `OpenGlControlBase` (then Vulkan/Metal)

## Milestone 2 — Document model & persistence

- [ ] architecturally bake in the ability to undo/redo
- [ ] Tiled canvas representation (support large images without one giant
      buffer)
- [ ] Native project file format + save/load
- [ ] Async/cancellable engine operations off the UI thread

## Milestone 3 — Photo manipulation

- [ ] PNG/JPEG import/export
- [ ] Selections
- [ ] Adjustments: brightness/contrast/levels/blur (batch C++ algorithms)
- [ ] Transforms: crop/resize/rotate

## Cross-cutting

- [ ] CI running both test suites (C++ + .NET) on Windows/macOS/Linux
- [ ] Startup/shutdown performance budget checks


## General future ideas

Brainstorm backlog — unprioritized, prune freely.

- **Render backends:** OpenGL (`OpenGlControlBase`) → Vulkan/Metal via
  composition-surface texture interop; zero-copy shared-texture present;
  bilinear/high-quality sampling; checkerboard transparency; pixel grid at high
  zoom; canvas rotate/flip view.
- **Input:** stylus pressure & tilt; stroke stabilizer/smoothing.
- **Brush system:** textured/stamp brushes, custom brush import, per-brush blend
  modes.
- **Layers:** masks, clipping masks, groups, non-destructive adjustment layers.
- **Color/depth:** ICC color management, wide-gamut/HDR, 16/32-bit-per-channel
  canvases.
- **Selections/transform:** marquee/lasso/magic-wand/smart-select (per the
  mockup), transform tool; text and vector layers.
- **Scale/perf:** tiled/virtualized canvas for huge documents, memory budgeting,
  multi-threaded stroke rendering, async cancellable filter pipeline.
- **Robustness:** autosave/crash recovery, fast-exit state persistence.
- **Ecosystem:** plugin API for tools/filters; export pipeline
  (PNG/JPEG/WebP/PSD); light/dark theming; i18n; accessibility.

