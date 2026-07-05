# Brush Engine

Status: **planned — not yet implemented.** This is the in-flight design for the
ROADMAP Milestone 1 item "Real brush engine: size, hardness, opacity, spacing
along a stroke (not just one dab)".

## Requirements

1. **Tablet support** — stylus pressure mapped to brush size and opacity/flow.
   Pressure is wired end-to-end now; the input sample struct reserves
   tilt/rotation/time fields so adding tilt later is a UI-only change.
2. **High-quality strokes** — anti-aliased dab edges, smooth continuous strokes
   (not a chain of visibly separate dabs).
3. **High performance** — allocation-free hot path (stroke input → render),
   coarse batched interop calls, dirty-rect partial redraws.
4. **Render-backend agnostic** — strokes rasterize into the canvas; any
   `Renderer` backend presents them. Dirty rects enable partial updates
   (software sub-rect render now, GPU `glTexSubImage2D` later).
5. **Future-proof for user-customizable brushes** — first milestone is a
   parametric round brush (size, hardness, opacity, flow, spacing, pressure
   curves) behind a pluggable `DabSource` so stamp/image brushes slot in later
   without rework.
6. **Heavily unit-testable** — every algorithm is an isolated unit (pure math
   kernels, a pixel-free path walker) testable without a canvas; the
   orchestrator that touches pixels is integration-tested.

## Architecture summary

A stateful, engine-side **stroke pipeline**: the app feeds batched input
samples (position + pressure); the engine interpolates the path, emits
anti-aliased dabs at spacing intervals into a per-stroke coverage buffer,
composites at stroke opacity against a stroke-start canvas snapshot, and
returns a dirty rect per call.

| Question | Decision | Rationale |
|---|---|---|
| Stroke model | Stateful `begin/addSamples/end` in a `BrushEngine` object behind its own interop handle (mirrors the `PrimaRenderer` pattern) | Spacing carry + wet buffer are inherently stateful; engine-side keeps interop coarse and gives headless hosts identical strokes. Not on `Canvas` — the interop `PrimaCanvas*` is a bare cast of `Canvas*`, and Canvas stays a dumb pixel surface (the tiled canvas reshapes it in Milestone 2) |
| Interpolation | Linear between samples now; Catmull-Rom later (isolated to `DabEmitter`, keep a 4-sample window) | Simple, deterministic; upgrade path costs nothing now |
| Anti-aliasing | Analytic coverage: distance + smoothstep falloff between `inner = hardness·(radius−0.5)` and `outer = radius+0.5` | Exact for circles, ~1 sqrt/pixel, deterministic; supersampling costs 4–16× for no gain on analytic shapes. `hardness = 1` degenerates to a crisp edge with a ~1px AA ramp — one formula serves hard and soft brushes |
| Blending | Per-stroke 16-bit coverage ("wet") buffer + baseline canvas snapshot, **in milestone 1, not deferred** | Flow = per-dab deposit (builds up); opacity = stroke-level cap (a self-crossing stroke never darkens past it). Industry-standard semantics (Photoshop/Krita); retrofitting later would change every stroke's appearance |
| Alpha model | Canvas stays straight-alpha RGBA8 | Locked by the existing ABI, `WriteableBitmap(AlphaFormat.Unpremul)`, and tests. Compositing un-premultiplies in float per pixel; correct over transparent baseline (no dark fringes — regression-tested) |
| Pressure mapping | In the engine, evaluated once per dab (never per pixel): `factor(p) = minFactor + (1−minFactor)·pow(p, gamma)` | Headless replay gives identical strokes; two floats per response cross the ABI trivially; a control-point LUT can be added later via additive struct growth |
| Dirty rects | Out-param `PrimaRect` on every stroke call; return-value plumbing, no canvas-side dirty state | Backend-neutral: software renders just the damaged sub-rect (no renderer ABI change — `prima_render` already takes an arbitrary target pointer/size/stride); a GPU backend later sub-uploads the same rect |

## Algorithm isolation (testability)

Every algorithm is factored into a pure, isolated unit — free of `Canvas`, I/O,
and (where possible) pixel buffers. `BrushEngine` is a thin orchestrator wiring
the units together; only it touches the canvas.

| Unit | File | Purity | Testable without |
|---|---|---|---|
| `RectI` geometry | `rect.h` | pure value type | anything |
| `PressureResponse::apply` | `brush_math.h` | pure `float → float` | anything |
| `dabCoverage(d, radius, hardness)` AA falloff | `brush_math.h` | pure `float → float` | pixels |
| `accumulateCoverage(c, flow, cov)` wet-buffer math | `brush_math.h` | pure scalar fixed-point op | pixels |
| `blendSourceOver(dst, srcColor, sa)` straight-alpha composite | `brush_math.h` | pure per-pixel value op | canvas |
| `DabEmitter` spacing walk | `dab_emitter.h/.cpp` | stateful but **pixel-free**: samples in → `DabPoint`s out | any buffer at all |
| `RoundDabSource::stamp` | `brush_engine.cpp` | operates on a bare `uint16_t*` coverage array | Canvas |
| `BrushEngine` | `brush_engine.cpp` | orchestrator: DabEmitter → DabSource → recomposite | — integration-tested |

## Engine design (`engine/`)

### `engine/include/prima/rect.h` (new)

```cpp
namespace prima {
// Integer pixel rect; width <= 0 or height <= 0 means empty.
struct RectI {
    int x = 0, y = 0, width = 0, height = 0;
    bool empty() const { return width <= 0 || height <= 0; }
    void unionWith(const RectI& o);           // grows this to cover o
    RectI intersect(int w, int h) const;      // clamp to canvas bounds
};
}
```

### `engine/include/prima/brush_math.h` (new)

The pure algorithm kernels as free functions (header-only, `constexpr`/inline
where possible). Each gets exhaustive unit tests: known-value tables, boundary
cases, monotonicity/range properties.

```cpp
namespace prima {

// How a 0..1 pressure value scales a brush property.
// factor(p) = minFactor + (1 - minFactor) * pow(p, gamma)
// Defaults (minFactor = 1) mean "no pressure response".
struct PressureResponse {
    float minFactor = 1.f;  // value at pressure 0, as fraction of base
    float gamma = 1.f;      // response curve exponent
    float apply(float pressure) const;
};

// Analytic AA coverage of a round dab at distance d from center:
//   inner = hardness * max(radius - 0.5, 0)   // fully covered core
//   outer = radius + 0.5                      // zero past here
//   cov = 1 for d <= inner; smoothstep ramp (3t²−2t³, inverted) between;
//         0 for d >= outer
float dabCoverage(float d, float radius, float hardness);

// Saturating flow-weighted accumulation into the 16-bit wet buffer
// (65535 == 1.0):  c' = c + flow·cov·(1 − c)
uint16_t accumulateCoverage(uint16_t c, float flow, float cov);

// Straight-alpha source-over of one pixel against a destination value:
//   outA = sa + dA·(1−sa);  outC = (srcC·sa + dC·dA·(1−sa)) / outA  (0 if outA==0)
Rgba blendSourceOver(Rgba dst, Rgba srcColor, float sa);

}
```

### `engine/include/prima/dab_emitter.h` + `src/dab_emitter.cpp` (new)

The spacing/interpolation algorithm with **no pixel knowledge**. This is where
spacing, linear interpolation, pressure→radius/flow resolution, leftover-
distance carry, and the tap guarantee live — all testable by collecting emitted
`DabPoint`s into a vector, no pixels involved. Catmull-Rom later changes only
this class.

```cpp
namespace prima {

// One point of stylus/mouse input, in canvas coordinates (float — subpixel
// positions are essential for smooth strokes). Tilt/rotation/time are plumbed
// end-to-end from day one but unused by the milestone-1 round brush.
struct InputSample {
    float x = 0.f, y = 0.f;
    float pressure = 1.f;   // 0..1; pressure-less devices pass 1
    float tiltX = 0.f;      // reserved: radians from vertical, X plane
    float tiltY = 0.f;      // reserved
    float rotation = 0.f;   // reserved: barrel rotation, radians
    double timeMs = 0.0;    // reserved: for speed dynamics / stabilizer
};

struct BrushParams {
    float radius = 12.f;    // base radius, canvas px (at pressure 1)
    float hardness = 0.8f;  // 0..1: 1 = crisp AA edge, 0 = soft falloff
    float opacity = 1.f;    // 0..1 stroke cap — self-overlap never exceeds it
    float flow = 1.f;       // 0..1 per-dab deposit — builds toward opacity
    float spacing = 0.15f;  // dab interval as fraction of dab diameter
    PressureResponse sizeResponse;   // pressure -> radius
    PressureResponse flowResponse;   // pressure -> flow
    Rgba color{0, 0, 0, 255};
};

struct DabPoint { float x, y, radius, flow; };  // fully resolved (pressure applied)

class DabEmitter {
public:
    void begin(const BrushParams& params);
    // Walks last→sample segments, emits spacing-separated dabs into `out`.
    // Carries leftover distance across segments AND across calls, so output is
    // independent of how samples are batched (tested explicitly).
    template <typename F>  // F: void(const DabPoint&)
    void addSamples(const InputSample* samples, int count, F&& out);
    template <typename F>
    void end(F&& out);     // guarantees >= 1 dab for a zero-length stroke (tap)
};

}
```

Emission rules:
- Dab interval `step = max(0.5f, spacing * 2 * currentRadius)`, recomputed at
  each emitted dab (radius varies with pressure along the segment).
- x/y/pressure interpolate linearly along each segment.
- The first sample of a stroke stamps immediately (pen-down mark).
- Radius is clamped to ≥ ~0.3 px; sub-pixel dabs scale coverage by area so
  pressure tapers fade out instead of popping.

### `engine/include/prima/brush.h` + `src/brush_engine.cpp` (new)

```cpp
namespace prima {

// Produces one dab's coverage into the stroke's accumulation buffer.
// Milestone 1 ships RoundDabSource only; stamp/image brushes implement this
// later. One virtual call per dab (not per pixel) — negligible cost.
class DabSource {
public:
    virtual ~DabSource() = default;
    // Accumulate coverage (flow-weighted, saturating) into `coverage`
    // (canvas-sized, 16-bit fixed point) and return the canvas-clamped rect of
    // touched pixels.
    virtual RectI stamp(float cx, float cy, float radius, float hardness,
                        float flow, uint16_t* coverage,
                        int canvasWidth, int canvasHeight) = 0;
};

class RoundDabSource final : public DabSource;  // loops dabCoverage() over the
                                                // dab's bounding box

// Thin orchestrator: DabEmitter -> DabSource -> recomposite. Owns reusable
// scratch buffers (baseline snapshot + coverage), sized lazily to the canvas
// on first beginStroke and reused — the add-samples hot path is allocation-free.
class BrushEngine {
public:
    BrushEngine();  // uses RoundDabSource; ctor taking a DabSource later

    // Snapshots the canvas into baseline_. If a stroke is already active it is
    // implicitly ended first (ABI stays void-returning per the open
    // error-channel decision).
    void beginStroke(Canvas& canvas, const BrushParams& params);
    // Feed a batch of samples; returns the canvas rect modified by this call
    // (empty if no dab was emitted).
    RectI addSamples(const InputSample* samples, int count);
    // Finalize; guarantees a tap leaves exactly one dab. Returns final dirty rect.
    RectI endStroke();
    bool strokeActive() const;

private:
    RectI stampDab(const DabPoint& dab);   // DabSource::stamp + recomposite
    void recomposite(const RectI& r);      // baseline ⊕ coverage·opacity → canvas

    std::unique_ptr<DabSource> dabSource_;
    DabEmitter emitter_;
    Canvas* canvas_ = nullptr;             // valid only between begin/end
    BrushParams params_;
    std::vector<uint8_t> baseline_;        // RGBA8 canvas snapshot at stroke start
    std::vector<uint16_t> coverage_;       // accumulated stroke coverage
    RectI strokeDirty_{};                  // union of all dabs → lazy coverage clear
};

}
```

Per-stroke mechanics:
1. `beginStroke`: snapshot canvas → `baseline_` (one memcpy, ~4 MB at 1024²,
   ~1–2 ms — once per stroke, never on the per-sample hot path). Zero
   `coverage_` only over the *previous* stroke's dirty rect (cheap).
2. Per dab: `DabSource::stamp` accumulates via `accumulateCoverage` — flow 1
   saturates instantly (no overlap darkening); flow < 1 builds asymptotically
   (airbrush), still capped.
3. Recomposite the dab's rect: effective `sa = coverage · opacity · color.a/255`,
   `blendSourceOver` against **`baseline_`** (not the live canvas — that is
   what prevents double-compositing on self-overlap).
4. `endStroke`: record `strokeDirty_`, drop the canvas pointer. The baseline
   buffer is, conveniently, exactly the "before" image undo/redo will want in
   the next roadmap item.

Memory: `baseline_` (4 B/px) + `coverage_` (2 B/px) = 6 B/px scratch, lazily
allocated, reused for the document's lifetime. Revisit when the tiled canvas
lands (per-tile copy-on-write replaces the full snapshot).

`Canvas::brushDab` stays untouched in this change (the interop boundary changes
"deliberately and rarely"); it is removed in a small follow-up once CLI/tests
migrate.

## Interop ABI additions (`interop/`)

`interop/include/prima_c/prima_c.h` — new blittable structs and five functions;
implemented in `interop/src/prima_c.cpp` following the existing handle-cast
shim pattern.

```c
/* All fields mirror prima::BrushParams / InputSample. */
typedef struct PrimaBrushParams {
    int32_t struct_size;          /* = sizeof(PrimaBrushParams); enables additive growth */
    float radius, hardness, opacity, flow, spacing;
    float size_pressure_min, size_pressure_gamma;
    float flow_pressure_min, flow_pressure_gamma;
    uint8_t r, g, b, a;
} PrimaBrushParams;

typedef struct PrimaInputSample {
    float x, y;                   /* canvas coords, subpixel */
    float pressure;               /* 0..1; pass 1.0 if the device has none */
    float tilt_x, tilt_y, rotation;   /* reserved, pass 0 */
    double time_ms;               /* reserved, pass 0 */
} PrimaInputSample;

typedef struct PrimaRect { int32_t x, y, width, height; } PrimaRect;  /* width<=0 => empty */

/* Opaque handle to a brush/stroke engine. One per document is typical. */
typedef struct PrimaBrushEngine PrimaBrushEngine;

PRIMA_C_API PrimaBrushEngine* prima_brush_engine_create(void);
PRIMA_C_API void prima_brush_engine_destroy(PrimaBrushEngine* engine);

/* Begin a stroke on `canvas` with `params` (copied). If a stroke is already
 * active it is ended first. `canvas` must outlive the stroke. */
PRIMA_C_API void prima_stroke_begin(PrimaBrushEngine* engine, PrimaCanvas* canvas,
                                    const PrimaBrushParams* params);

/* Feed `count` samples in one batched call — the ONLY per-input-event call;
 * one call per UI pointer event, samples coalesced inside. out_dirty
 * (optional) receives the canvas rect modified by this call. */
PRIMA_C_API void prima_stroke_add(PrimaBrushEngine* engine,
                                  const PrimaInputSample* samples, int count,
                                  PrimaRect* out_dirty);

PRIMA_C_API void prima_stroke_end(PrimaBrushEngine* engine, PrimaRect* out_dirty);
```

Coarse and batched by construction; no per-dab or per-pixel traffic across the
boundary. `prima_canvas_brush_dab` remains untouched this change.

## App layer (`app/Prima.App/`)

New files, all headless-usable, no UI deps:

```csharp
// Brush.cs — blittable mirrors of the C structs
[StructLayout(LayoutKind.Sequential)]
public struct BrushParams {
    internal int StructSize;                 // set by BrushEngine before the call
    public float Radius, Hardness, Opacity, Flow, Spacing;
    public float SizePressureMin, SizePressureGamma;
    public float FlowPressureMin, FlowPressureGamma;
    public byte R, G, B, A;
    public static BrushParams Default(Rgba color, float radius = 12f);
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct InputSample(
    float X, float Y, float Pressure = 1f,
    float TiltX = 0f, float TiltY = 0f, float Rotation = 0f, double TimeMs = 0.0);

public readonly record struct DirtyRect(int X, int Y, int Width, int Height) {
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public DirtyRect Union(DirtyRect other);
}

// BrushEngine.cs — owns the native handle; same lifecycle pattern as Renderer
public sealed unsafe class BrushEngine : IDisposable {
    public static BrushEngine Create();
    public void BeginStroke(Document document, in BrushParams parameters);
    public DirtyRect AddSamples(ReadOnlySpan<InputSample> samples);  // fixed() over the span — zero alloc
    public DirtyRect EndStroke();
    public bool StrokeActive { get; }
}
```

`NativeMethods.cs` gains the five `LibraryImport` entries (pointer-based, spans
pinned with `fixed`). `host/Prima.Cli/Program.cs` gets an optional demo stroke
(hardcoded pressure-varying sample array → PPM) — a manual AA smoke test that
also proves the headless contract.

## UI wiring (`ui/Prima.Desktop/Controls/CanvasControl.cs`)

- Own a `BrushEngine`; expose `BrushSize` (float, replaces int `BrushRadius`),
  `BrushHardness`, `BrushOpacity`, `BrushFlow` properties feeding `BrushParams`.
- **Pressed** (left, not panning): `e.Pointer.Capture(this)` so strokes survive
  leaving the control; `BeginStroke`; feed the first sample.
- **Moved** while stroking: `e.GetIntermediatePoints(this)` — Avalonia's
  coalesced high-frequency tablet points. Fill a **reused** `InputSample[]`
  field (grown geometrically, never per event — allocation-free hot path); each
  point → `Viewport.TargetToCanvasX/Y` as floats (no flooring — subpixel);
  pressure from `Properties.Pressure`, trusted only when
  `e.Pointer.Type == PointerType.Pen` (mouse/touch report a constant 0.5 —
  map them to 1.0). One `AddSamples` call per event.
- **Released**: `EndStroke`, release capture.
- **Partial invalidation with dirty rects**: accumulate returned rects into a
  `_pendingDirty` field; `InvalidateVisual()` as today, but `RenderIntoBitmap`
  checks it — if bitmap size and viewport are unchanged and `_pendingDirty` is
  non-empty, map the canvas rect → physical-pixel target rect (pad 1 px for
  rounding) and render only that sub-rect: offset the framebuffer pointer to
  `fb.Address + y·RowBytes + x·4`, pass sub-rect width/height with the same
  `RowBytes` stride, and shift the viewport pan by `x/(zoom·scaling)`.
  Pan/zoom/resize still trigger a full re-render. **No renderer or ABI change
  required** — `prima_render` already accepts an arbitrary target
  pointer/size/stride.
- Tilt later is UI-only: Avalonia exposes `Properties.XTilt/YTilt`; the sample
  struct already carries the fields.

## Testing

Engine tests are layered to match the algorithm isolation — most coverage lands
on pure units that need no canvas. All added to `tests/engine/CMakeLists.txt`.

**`tests/engine/brush_math_test.cpp`** (pure kernels, exhaustive):
- `PressureResponse::apply` at p = 0/0.5/1 × min/gamma combinations;
  monotonicity; output ∈ [minFactor, 1].
- `dabCoverage` known-value table: d = 0 → 1, d ≥ radius+0.5 → 0, midpoint of
  the AA band, hardness 0/0.5/1 shapes; monotone-decreasing in d.
- `accumulateCoverage`: saturation at flow 1, asymptotic build at flow < 1,
  never exceeds 65535.
- `blendSourceOver`: over transparent/opaque/partial destinations; no color
  fringe over transparent; round-trip identities.

**`tests/engine/dab_emitter_test.cpp`** (pixel-free — asserts on collected `DabPoint`s):
- **Batching determinism**: one `addSamples` call vs one sample per call →
  identical dab lists (proves leftover-distance carry).
- Spacing: expected dab count and positions on a straight polyline.
- Leftover carry across segments and across calls.
- Pressure ramp 0→1 → monotonically increasing radii.
- Tap (begin + end, no movement) emits exactly one dab.
- Zero-length segments handled without duplicate or missing dabs.

**`tests/engine/brush_engine_test.cpp`** (integration — pixels):
- Single-dab AA edges: center a = 255, beyond radius+0.5 a = 0, ring pixel at
  d ≈ radius has 0 < a < 255; 4-way symmetry (exact byte equality).
- Opacity cap: self-crossing stroke at opacity 0.5, flow 1 — crossing pixel
  alpha == non-crossing alpha (no darkening).
- Flow build-up: repeated dabs at one point with flow 0.25 increase alpha
  monotonically, never exceeding opacity.
- Straight-alpha correctness: dab over fully transparent canvas → stored RGB
  equals brush RGB (no fringe / black bleed).
- Dirty-rect exactness: returned rect == bounding box of actually-changed
  pixels (diff against a pre-stroke copy); empty rect when no dab was emitted.
- Stroke isolation: two sequential strokes == same strokes on fresh engines;
  end-to-end batching determinism on the final pixel buffer.

**`tests/app/Prima.App.Tests/BrushEngineTests.cs`** (xUnit, drives the real DLL):
- Begin/add/end roundtrip changes pixels; dirty rect non-empty, within bounds.
- Replay determinism: a canned recorded stroke (the future CLI/undo contract)
  produces an identical pixel hash across two fresh documents.
- Batching equivalence across the ABI (marshaling doesn't perturb output).
- `Marshal.SizeOf<BrushParams>()` / `InputSample` match expected native sizes
  (struct-layout drift guard).

## File-by-file change list

| File | Change |
|---|---|
| `engine/include/prima/rect.h` | **new** — `RectI` |
| `engine/include/prima/brush_math.h` | **new** — pure kernels: `PressureResponse`, `dabCoverage`, `accumulateCoverage`, `blendSourceOver` |
| `engine/include/prima/dab_emitter.h` + `engine/src/dab_emitter.cpp` | **new** — `InputSample`, `BrushParams`, `DabPoint`, `DabEmitter` (pixel-free spacing walk) |
| `engine/include/prima/brush.h` + `engine/src/brush_engine.cpp` | **new** — `DabSource`, `RoundDabSource`, `BrushEngine` orchestrator |
| `engine/CMakeLists.txt` | add new sources |
| `interop/include/prima_c/prima_c.h` | add `PrimaBrushParams`, `PrimaInputSample`, `PrimaRect`, `PrimaBrushEngine` + 5 functions |
| `interop/src/prima_c.cpp` | implement the shims |
| `app/Prima.App/Brush.cs` | **new** — `BrushParams`, `InputSample`, `DirtyRect` |
| `app/Prima.App/BrushEngine.cs` | **new** — managed handle wrapper |
| `app/Prima.App/NativeMethods.cs` | 5 new `LibraryImport`s |
| `ui/Prima.Desktop/Controls/CanvasControl.cs` | stroke lifecycle, pressure, `GetIntermediatePoints`, pointer capture, dirty-rect partial bitmap render |
| `host/Prima.Cli/Program.cs` | replay a canned pressure-varying stroke into the PPM |
| `tests/engine/brush_math_test.cpp`, `dab_emitter_test.cpp`, `brush_engine_test.cpp` + `tests/engine/CMakeLists.txt` | engine test suites above |
| `tests/app/Prima.App.Tests/BrushEngineTests.cs` | app test suite above |
| `CLAUDE.md` / `ROADMAP.md` | record the brush-architecture decision; check the M1 box on ship; delete this doc when shipped |

**Sequencing**: engine + engine tests → interop → app wrapper + app tests → UI
→ CLI demo → docs. Each stage lands green via `./build.ps1` + `./test.ps1`.

## Milestone slicing

**Milestone 1 (this feature)** — parametric round brush end to end: `DabEmitter`
(linear interpolation, spacing carry), analytic AA, coverage buffer with
flow/opacity semantics, pressure→size/flow, dirty rects, the 5-function ABI,
C# `BrushEngine`, UI pressure + coalesced input + capture + partial
invalidation, CLI replay, full test list. `Canvas::brushDab` /
`prima_canvas_brush_dab` kept but no longer used by the UI.

**Deferred (explicitly designed-for):**
- Catmull-Rom smoothing — swap inside `DabEmitter` only.
- Tilt/rotation/speed dynamics — fields already cross every layer.
- Stamp/image brushes — implement `DabSource`.
- Stroke stabilizer — app-layer sample filter; samples are already a stream.
- Per-brush blend modes — extend the recomposite step.
- Pressure-curve LUT — additive `BrushParams` fields via `struct_size`.
- GPU dab rendering — dirty rects are already backend-neutral.
- Tiled-canvas baseline copy-on-write — Milestone 2.
- Removal of the legacy dab ABI.

## Performance conformance

- Hot path (`addSamples`) is allocation-free: engine scratch buffers and the UI
  sample array are pooled/reused.
- Interop is one batched call per pointer event; never per-dab or per-pixel.
- The only O(canvas) costs are once-per-stroke: the `beginStroke` baseline
  memcpy (~1–2 ms at 1024²) and a coverage clear bounded to the previous
  stroke's dirty rect.
- No `pow` or virtual calls inside pixel loops — pressure curves evaluate once
  per dab; `DabSource::stamp` is one virtual call per dab.

## Task breakdown (with model recommendations)

Tasks are ordered to respect the sequencing (engine → interop → app → UI → CLI →
docs) and dependency edges. "Model" is the tier best suited to the task:
**Opus** for correctness-critical algorithms, tricky numerics, the ABI contract,
and the stateful hot path; **Sonnet** for mechanical-but-nontrivial mirroring,
wrappers, and test suites with clear specs; **Haiku** for boilerplate/glue.

| # | Task | Files | Model | Why this tier |
|---|---|---|---|---|
| 1 | `RectI` value type — `unionWith`, `intersect`, `empty` | `engine/include/prima/rect.h` | **Haiku** | Tiny, self-contained value type with obvious semantics; no numerics or cross-layer risk. |
| 2 | Pure math kernels — `PressureResponse::apply`, `dabCoverage` (analytic AA falloff), `accumulateCoverage` (16-bit saturating), `blendSourceOver` (straight-alpha, un-premul) | `engine/include/prima/brush_math.h` | **Opus** | Correctness-critical numerics; straight-alpha un-premultiply is the classic dark-fringe trap; smoothstep band + inner/outer must be exact. Everything above depends on these. |
| 3 | Exhaustive kernel unit tests (known-value tables, monotonicity, range, no-fringe) | `tests/engine/brush_math_test.cpp` | **Sonnet** | Spec is fully enumerated in §Testing; disciplined table-writing, not novel design. Consider Opus if edge-case tables need judgment. |
| 4 | `DabEmitter` — spacing walk, linear interp, pressure→radius/flow, leftover-distance carry across segments **and** calls, tap guarantee | `engine/include/prima/dab_emitter.h`, `engine/src/dab_emitter.cpp` + `InputSample`/`BrushParams`/`DabPoint` structs | **Opus** | Stateful algorithm whose whole value is batching-independence and sub-pixel correctness; off-by-one in carry silently corrupts every stroke. Isolated but subtle. |
| 5 | `DabEmitter` pixel-free tests (batching determinism, spacing counts, carry, pressure ramp, tap, zero-length) | `tests/engine/dab_emitter_test.cpp` | **Sonnet** | Assertions are well-specified; the hard thinking lives in task 4. |
| 6 | `DabSource`/`RoundDabSource` + `BrushEngine` orchestrator — baseline snapshot, coverage accumulation, recomposite-against-baseline, lazy reused scratch buffers, dirty-rect union | `engine/include/prima/brush.h`, `engine/src/brush_engine.cpp` | **Opus** | The allocation-free hot path and the self-overlap-correct compositing (composite against baseline, not live canvas) are the crux of the feature; performance + correctness coupled. |
| 7 | Integration pixel tests (AA edges, opacity cap, flow build-up, no-fringe, dirty-rect exactness, stroke isolation, batching determinism on pixels) | `tests/engine/brush_engine_test.cpp` | **Sonnet** | Detailed spec exists; mostly wiring canvases and asserting. Opus only if the exact-byte symmetry assertions get fiddly. |
| 8 | Wire new engine sources into the build | `engine/CMakeLists.txt`, `tests/engine/CMakeLists.txt` | **Haiku** | Add-file glue, deterministic. |
| 9 | Interop ABI — `PrimaBrushParams`/`PrimaInputSample`/`PrimaRect`/`PrimaBrushEngine` + 5 functions + handle-cast shims | `interop/include/prima_c/prima_c.h`, `interop/src/prima_c.cpp` | **Opus** | This is the contract that "changes deliberately and rarely"; blittable-struct layout, `struct_size` growth field, and out-param dirty-rect plumbing must be exactly right — drift here breaks every consumer. |
| 10 | Managed struct mirrors — `BrushParams`, `InputSample`, `DirtyRect` (+ `Default`/`Union` helpers) | `app/Prima.App/Brush.cs` | **Sonnet** | Blittable layout must match native byte-for-byte (a size-guard test exists), but it's disciplined mirroring, not design. |
| 11 | `BrushEngine` managed wrapper + P/Invoke entries (span pinning via `fixed`, handle lifecycle) | `app/Prima.App/BrushEngine.cs`, `app/Prima.App/NativeMethods.cs` | **Sonnet** | Follows the existing `Renderer` pattern; `LibraryImport` + zero-alloc `fixed` over spans is well-trodden. |
| 12 | App-layer xUnit tests (roundtrip, replay determinism hash, batching equivalence, struct-size drift guard) | `tests/app/Prima.App.Tests/BrushEngineTests.cs` | **Sonnet** | Spec'd; drives the real DLL with clear assertions. |
| 13 | UI stroke wiring — pointer capture, `GetIntermediatePoints` coalesced input, pen-only pressure, reused sample array, **dirty-rect partial bitmap render** (framebuffer pointer offset + viewport pan shift) | `ui/Prima.Desktop/Controls/CanvasControl.cs` | **Opus** | The sub-rect render (pointer arithmetic into the framebuffer, stride/pan bookkeeping, 1px pad) is easy to get subtly wrong and off-thread-sensitive; also the allocation-free-per-event constraint. |
| 14 | CLI canned pressure-varying stroke → PPM (headless AA smoke test) | `host/Prima.Cli/Program.cs` | **Haiku** | Hardcoded sample array through the finished app API; minimal logic. |
| 15 | Record the brush-architecture decision; check the M1 box; delete this doc on ship | `CLAUDE.md`, `ROADMAP.md`, this file | **Haiku** | Doc bookkeeping. |

**Suggested batching for parallel/sequential agents**

- **Serial spine (Opus):** 2 → 4 → 6 → 9 → 13. These are the load-bearing tasks;
  each unblocks the next layer and carries the correctness/perf risk.
- **Fan-out after each Opus task (Sonnet):** tests 3/5/7/12 and wrappers 10/11 can
  run alongside once their subject exists.
- **Cheap glue (Haiku):** 1, 8, 14, 15 slot in with little coordination.

Every task lands green via `./build.ps1` + `./test.ps1` before the next dependent
task starts.
