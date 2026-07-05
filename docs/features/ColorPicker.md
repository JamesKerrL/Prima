# Color Picker — Phase 1 (Ring + Triangle, HSV)

> In-flight feature spec. Research/rationale: `ColorPickerResearch.md`.
> Target look: `ColorPickerReference.png` (Colorus 2). This doc is written to be
> executed step by step by an implementer (human or LLM).

## Context

Prima's current color picker (`ui/Prima.Desktop/Controls/ColorPickerControl.axaml[.cs]`)
is a thin wrapper around Avalonia's stock `ColorPicker` — a generic RGBA box that
does nothing for a **digital painter**. This phase replaces its internals with a
custom, themed **hue ring + saturation/value triangle** picker matching the
reference, while keeping the wrapper's public contract intact.

### Scope decisions (locked)

- **Geometry:** outer hue ring + inner Derry-style **triangle** (vertices: white,
  black, fully-saturated hue).
- **Color model:** **HSV only** for Phase 1 — *no OKLCH/OKLab, no luminosity/chroma
  lock.*
- **No physical (Kubelka-Munk) pigment mixing.**
- **Numeric fields:** HEX, RGB, HSV only.
- **Placement:** static docked panel (same slot as today). Movable/tear-off HUD is
  a later phase.
- **Color science isolated in C++**, unit-tested with GoogleTest. UI stays thin.
- **Public contract preserved:** `ColorPickerControl` keeps its `SelectedColor`
  (`Prima.App.Rgba`) property + `ColorChanged` event, so `MainWindow` is untouched.

## Phase 1 deliverable

1. Outer **hue ring** + inner **saturation/value triangle**, both draggable.
2. **Foreground/background split preview** (previous color vs. newly targeted color).
3. **Numeric fields:** HEX (`#RRGGBB`), RGB (0–255), HSV (H 0–360, S/V 0–100).
4. **Swatches + recent-color history** strip.

## Architecture (top-down; dependencies point down only)

### 1. C++ engine — `engine/` (color-science core, GoogleTest)

**`engine/include/prima/color.h` + `engine/src/color.cpp`** (new):

- `struct Hsv { double h; double s; double v; };` — h in [0,360), s/v in [0,1].
- Pure conversions: `Hsv HsvFromRgba(Rgba)`, `Rgba RgbaFromHsv(Hsv)`.
  (`Rgba` already exists in `engine/include/prima/canvas.h`.)
- Pure geometry helpers (so hit-testing is testable with no UI):
  - `double HueFromAngle(double radians)` and its inverse.
  - Triangle mapping: normalized triangle coords ↔ `(s, v)`. Interior points are
    the **barycentric blend of the three vertex colors** (white, black, pure-hue)
    — the standard SV-triangle.

**`engine/include/prima/color_wheel.h` + `engine/src/color_wheel.cpp`** (new):
an opaque, engine-owned renderer mirroring the existing `Canvas` /
`prima_canvas_pixels` buffer pattern (UI presents these zero-copy, exactly like
the canvas bitmap today):

- `ColorWheel(int outerSizePx, int ringThicknessPx)` — allocates ring + triangle
  RGBA8 buffers.
- Ring bitmap generated once (depends only on size). Triangle bitmap regenerated
  on `SetHue(double)`.
- Accessors expose raw `const uint8_t*` + width/height for both bitmaps.

*Rationale:* ring/triangle fills are image algorithms; conversions are the color
science — both belong below the interop boundary and are headless-testable.

### 2. Interop — `interop/` (narrow C ABI; buffer-sharing, no per-pixel chatter)

Extend the existing `extern "C"` shim, following the current `prima_canvas_*`
style (opaque handle in, pointer-to-engine-buffer out):

```c
void  prima_color_rgba_to_hsv(uint8_t r, uint8_t g, uint8_t b,
                              double* h, double* s, double* v);
void  prima_color_hsv_to_rgba(double h, double s, double v,
                              uint8_t* r, uint8_t* g, uint8_t* b);

PrimaColorWheel* prima_colorwheel_create(int outerSize, int ringThickness);
void  prima_colorwheel_set_hue(PrimaColorWheel*, double hue);
const uint8_t* prima_colorwheel_ring_pixels(PrimaColorWheel*, int* w, int* h);
const uint8_t* prima_colorwheel_triangle_pixels(PrimaColorWheel*, int* w, int* h);
void  prima_colorwheel_destroy(PrimaColorWheel*);
```

### 3. C# app layer — `app/Prima.App/` (headless, xUnit)

- `NativeMethods.cs`: `LibraryImport` P/Invoke declarations for the above
  (mirror existing entries).
- `Hsv.cs`: `readonly record struct Hsv(double H, double S, double V)` with
  `ToRgba()`/`FromRgba()` calling the native conversions. **HEX parse/format stays
  in C#** (trivial string work; not worth crossing the ABI).
- `ColorWheel.cs`: `IDisposable` wrapper over `PrimaColorWheel*` (same shape as
  `Document`/`Renderer`), exposing ring/triangle pixel spans + `SetHue`.
- `SwatchPalette.cs` / `ColorHistory.cs`: session-state models holding `Rgba`
  lists (saved swatches, recent colors). Pure app-layer logic.

*Alpha:* `Rgba.A` is passed through unchanged in Phase 1 (opacity lives in the
brush toolbar). An alpha slider is a later add.

### 4. UI — `ui/Prima.Desktop/` (thin; reusable components; shared theme only)

Replace the internals of `Controls/ColorPickerControl.axaml[.cs]`, keeping its
public `SelectedColor`/`ColorChanged` surface. New reusable components under
`Controls/` (per the "needed twice → shared component" convention):

- `HueRingTriangleControl` — custom `Control` that blits the two `WriteableBitmap`s
  from `ColorWheel`, draws the ring + triangle drag handles, converts pointer
  drags → hue / (s,v) → `Rgba`. The interactive heart.
- `ColorPreviewSplit` — FG/BG previous-vs-new split swatch.
- `LabeledColorField` — one reusable labeled numeric/text input, instanced per
  HEX / R / G / B / H / S / V row (no per-field copy-paste).
- `SwatchStrip` — renders `SwatchPalette` + `ColorHistory`, click-to-select.

All colors/spacing come from `Themes/Theme.axaml`; add any new tokens there (no
one-off values). The stock `ColorPicker`/`ColorView` style overrides in the theme
become dead once the wrapper stops using them — remove them, and optionally drop
the `Avalonia.Controls.ColorPicker` package reference (smaller startup footprint)
after confirming nothing else needs it.

## Tests (every change lands with tests)

- **GoogleTest** (`tests/engine/`): HSV↔RGBA round-trips and known values (pure
  red = H0/S1/V1; gray = S0), triangle corner pixels (top→white, bottom→black,
  apex→pure hue), ring pixel color at a sampled angle, triangle `(x,y)↔(s,v)`
  inverse.
- **xUnit** (`tests/app/Prima.App.Tests/`): `ColorWheel` wrapper drives the real
  DLL (buffers non-empty, corners correct, `SetHue` changes the triangle);
  `SwatchPalette`/`ColorHistory` add/dedupe/cap behavior.

## Verification (end to end)

1. `./build.ps1` then `./test.ps1` — both suites green.
2. Run `ui/Prima.Desktop` (`dotnet run`): Color panel shows the ring+triangle;
   dragging the ring rotates hue and repaints the triangle; dragging in the
   triangle changes S/V; HEX/RGB/HSV fields update live and are editable; FG/BG
   split reflects previous vs. current; clicking a swatch/history entry sets the
   brush color; painting uses the picked color (existing `OnBrushColorChanged`
   path in `MainWindow`).

## Critical files

- `engine/include/prima/color.h`, `engine/src/color.cpp` (new)
- `engine/include/prima/color_wheel.h`, `engine/src/color_wheel.cpp` (new)
- `interop/` C-ABI shim (extend; alongside `prima_canvas_*`)
- `app/Prima.App/NativeMethods.cs`, `Hsv.cs`, `ColorWheel.cs`,
  `SwatchPalette.cs`/`ColorHistory.cs`
- `ui/Prima.Desktop/Controls/ColorPickerControl.axaml[.cs]` (rewrite internals) +
  new `HueRingTriangleControl`, `ColorPreviewSplit`, `LabeledColorField`,
  `SwatchStrip`; `Themes/Theme.axaml`
- `tests/engine/` (GoogleTest), `tests/app/Prima.App.Tests/` (xUnit)

## Task breakdown & suggested execution model (Phase 1 only)

Tasks are ordered by dependency (C++ core → interop → C# app → UI → tests →
verification → housekeeping). "Suggested model" is a starting point, not a rule —
bump up a tier if a task turns out gnarlier than expected, drop down if it turns
out simpler. Tiers used: **Haiku** (mechanical/boilerplate, low ambiguity),
**Sonnet** (standard implementation work, some judgment calls), **Opus**
(correctness-critical math or interactive/coordinate-heavy code where subtle
bugs are easy to introduce and expensive to catch later).

| # | Task | File(s) | Suggested model | Why |
|---|------|---------|------------------|-----|
| 1 | HSV↔RGBA conversions + hue/triangle geometry math | `engine/include/prima/color.h`, `engine/src/color.cpp` | Sonnet | Standard, well-specified algorithms; test cases are already enumerated below, so correctness is checkable, but it's foundational — every other task depends on it being right. |
| 2 | GoogleTest for `color.cpp` (round-trips, known values, corner/inverse checks) | `tests/engine/` | Sonnet | Test cases are already listed in this doc; mostly translating spec → assertions. |
| 3 | Ring/triangle bitmap renderer (`ColorWheel` engine class) | `engine/include/prima/color_wheel.h`, `engine/src/color_wheel.cpp` | **Opus** | Barycentric triangle fill + polar ring fill is easy to get subtly wrong at the edges (aliasing, off-by-one on angle wrap, vertex color bleed); errors here are visual and easy to miss without careful eyeballing. |
| 4 | GoogleTest for `color_wheel.cpp` (corner pixels, sampled ring angle, mapping inverse) | `tests/engine/` | Sonnet | Straightforward once #3 exists and test cases are enumerated. |
| 5 | Interop C ABI extensions (`prima_color_*`, `prima_colorwheel_*`) | `interop/` | Haiku | Pure mechanical mirror of the existing `prima_canvas_*` shim pattern — no new design decisions. |
| 6 | C# P/Invoke declarations | `app/Prima.App/NativeMethods.cs` | Haiku | Mechanical mirror of existing entries in the same file. |
| 7 | `Hsv` record struct + HEX parse/format | `app/Prima.App/Hsv.cs` | Haiku | Small, self-contained, no design ambiguity. |
| 8 | `ColorWheel` C# wrapper (native handle lifecycle) | `app/Prima.App/ColorWheel.cs` | Sonnet | Mirrors `Document`/`Renderer` pattern, but `IDisposable`/native-handle lifetime correctness (double-free, use-after-dispose) deserves more care than a pure mechanical task. |
| 9 | `SwatchPalette` / `ColorHistory` session models | `app/Prima.App/SwatchPalette.cs`, `ColorHistory.cs` | Sonnet | Small design choices baked in (dedupe rule, history cap) that aren't fully specified — needs judgment. |
| 10 | xUnit tests for `ColorWheel` wrapper + swatch/history models | `tests/app/Prima.App.Tests/` | Sonnet | Test cases are enumerated, but driving the real DLL and asserting on dedupe/cap behavior needs a bit more care than pure boilerplate. |
| 11 | `HueRingTriangleControl` (drag handling, hit-testing, bitmap blit → `Rgba`) | `ui/Prima.Desktop/Controls/HueRingTriangleControl.cs` | **Opus** | The hardest piece in the whole feature: pointer-to-color coordinate math (angle↔hue, triangle inverse-mapping) combined with interactive drag state. Subtle bugs (dead zones, snapping at wrap-around, wrong handle after resize) are the kind that "look right" in a screenshot but are wrong in actual use. |
| 12 | `ColorPreviewSplit` (FG/BG split swatch) | `ui/Prima.Desktop/Controls/ColorPreviewSplit.cs` | Haiku | Simple static-layout custom control, no interaction. |
| 13 | `LabeledColorField` (reusable labeled input) | `ui/Prima.Desktop/Controls/LabeledColorField.axaml[.cs]` | Haiku | Small, reused as-is across HEX/R/G/B/H/S/V rows — no per-instance logic. |
| 14 | `SwatchStrip` (palette + history, click-to-select) | `ui/Prima.Desktop/Controls/SwatchStrip.axaml[.cs]` | Sonnet | List rendering + click wiring against #9's models; modest judgment on layout/selection state. |
| 15 | Rewire `ColorPickerControl` internals, preserving `SelectedColor`/`ColorChanged` | `ui/Prima.Desktop/Controls/ColorPickerControl.axaml[.cs]` | Sonnet | Integration point for #11–#14; must not break the existing `MainWindow` contract — needs careful event-wiring review, not just assembly. |
| 16 | `Theme.axaml` cleanup (remove dead `ColorPicker`/`ColorView` styles, add new tokens) | `ui/Prima.Desktop/Themes/Theme.axaml` | Haiku | Mechanical addition/removal following the existing token pattern. |
| 17 | End-to-end verification (build/test scripts + interactive drag/field/swatch check) | n/a (manual + `build.ps1`/`test.ps1`) | Sonnet | Requires judgment to interpret whether interactive behavior actually feels right, not just that it compiles/passes unit tests. |
| 18 | Housekeeping (`ROADMAP.md` checkbox, `CLAUDE.md` decisions, delete this doc) | `ROADMAP.md`, `CLAUDE.md`, this file | Haiku | Bookkeeping only. |

**Rule of thumb for the two Opus tasks (#3, #11):** these are the only places
where a wrong answer is both *plausible-looking* and *expensive to unwind*
(visual gradients, live drag interaction) — worth the extra reasoning budget.
Everything else is either mechanical or has its correctness spelled out by the
test list in this doc, so Sonnet/Haiku should be reliable there.

## Housekeeping on ship

- Check off "Color picker + palette" in `ROADMAP.md`.
- Update `CLAUDE.md` "Decisions" if the color module / colorwheel handle changes
  the interop contract meaningfully.
- Delete this doc when the feature ships (git history retains it).

## Later phases (roadmap only — not built now)

- **P2 — painter power:** luminosity/chroma lock (requires adding an OKLCH core to
  the C++ color module), harmony markers, composited eyedropper sampling.
- **P3 — planning tools:** gamut masking, 4-corner intermediate grid, approximate
  color panel.
- **P4 — ecosystem/ergonomics:** movable/tear-off + on-canvas follow HUD,
  wide-gamut proofing, swatch import/export (`.ase/.aco/.kpl/.gpl`).
