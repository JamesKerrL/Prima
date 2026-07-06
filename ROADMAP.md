# Prima Roadmap

Planned features, in rough priority order. This is a living backlog — edit
freely (add, reorder, check off, delete). See [CLAUDE.md](CLAUDE.md) for
architecture/decisions; this file is just what to build next.

Check off items as they land: `- [x] done thing`.

## Milestone

## Milestone 1 — Drawing core (next up)

- [x] Real brush engine: size, hardness, opacity, spacing along a stroke (not
      just one dab) — analytic AA, pressure→size/flow, coverage buffer with
      flow/opacity semantics, dirty-rect partial invalidation
- [x] Flood fill (paint bucket) tool — isolated 4-connected engine algorithm
      with per-channel tolerance, undoable via `RegionEdit`, toolbar bucket icon
- [ ] Layers: add/remove/reorder, per-layer opacity, blend modes
- [x] Undo/redo (command pattern in the app layer)
- [x] Canvas pan/zoom (delivered via the render-backend work below)
- [ ] Color picker + palette (use open source color library if necessary)

## Milestone 1a — Canvas view & render backend (in progress)

- [x] Engine-side `Renderer` abstraction + software (CPU) backend
- [x] `Viewport` (pan/zoom) mapping in the engine, headless-tested
- [x] Reusable Avalonia `CanvasControl` (pan/zoom/paint) on a physical-pixel bitmap
- [x] GPU backend #1 plumbing: Direct3D 11 device (hardware → WARP fallback),
      behind the existing `Renderer` seam, ABI + `Renderer.CreateBest()`
      software fallback — headless-tested via WARP
- [ ] D3D11 GPU compositing: shader viewport sampling + zero-copy present via
      Avalonia composition/swapchain interop; switch `CanvasControl` to
      `CreateBest()` when it lands

## Milestone 2 — Document model & persistence

- [ ] architecturally bake in the ability to undo/redo
- [ ] Tiled canvas representation (support large images without one giant
      buffer)
- [ ] Native project file format + save/load
- [ ] Async/cancellable engine operations off the UI thread

## Milestone 3 — Photo manipulation

- [x] PNG/JPEG import/export
- [ ] Selections
- [ ] Adjustments: brightness/contrast/levels/blur (batch C++ algorithms)
- [ ] Transforms: crop/resize/rotate

## Cross-cutting

- [ ] CI running both test suites (C++ + .NET) on Windows/macOS/Linux
- [ ] Startup/shutdown performance budget checks


## General future ideas

Brainstorm backlog — unprioritized, prune freely.

- **Render backends:** D3D11 present path (zero-copy shared-texture via
  Avalonia composition interop) → Vulkan/Metal for other platforms; OpenGL
  (`OpenGlControlBase`) as a cross-platform option if ever needed;
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

- **Reference image:** reference-image.md
- **Brush system (presets/UI/sharing/designer):** docs/features/BrushSystem.md
- **3d model engine:** separate 3d model engine that allows posing a reference character
- **Image segmentation:** use a small image segmentation ml algo
- **Image upscaling:** use a small image segmentation ml algo