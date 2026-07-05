#ifndef PRIMA_BRUSH_H
#define PRIMA_BRUSH_H

#include <cstdint>
#include <memory>
#include <vector>

#include "prima/canvas.h"
#include "prima/dab_emitter.h"
#include "prima/rect.h"

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

// Loops dabCoverage() over the dab's bounding box, accumulating flow-weighted
// coverage into the wet buffer.
class RoundDabSource final : public DabSource {
public:
    RectI stamp(float cx, float cy, float radius, float hardness, float flow,
                uint16_t* coverage, int canvasWidth, int canvasHeight) override;
};

// Thin orchestrator: DabEmitter -> DabSource -> recomposite. Owns reusable
// scratch buffers (baseline snapshot + coverage), sized lazily to the canvas on
// first beginStroke and reused — the add-samples hot path is allocation-free.
class BrushEngine {
public:
    BrushEngine();  // constructs a RoundDabSource internally

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

    // Copy a packed RGBA8 region out of the stroke's frozen "before" snapshot
    // (baseline_), for undo history. Valid once a stroke has begun and until the
    // next beginStroke. Returns false if no baseline exists yet or the rect
    // falls outside [0, baselineWidth_) x [0, baselineHeight_).
    bool readBaselineRegion(int x, int y, int w, int h, uint8_t* dst) const;

private:
    RectI stampDab(const DabPoint& dab);   // DabSource::stamp + recomposite
    void recomposite(const RectI& r);      // baseline ⊕ coverage·opacity → canvas

    std::unique_ptr<DabSource> dabSource_;
    DabEmitter emitter_;
    Canvas* canvas_ = nullptr;             // valid only between begin/end
    BrushParams params_;
    std::vector<uint8_t> baseline_;        // RGBA8 canvas snapshot at stroke start
    int baselineWidth_ = 0;
    int baselineHeight_ = 0;
    std::vector<uint16_t> coverage_;       // accumulated stroke coverage
    RectI strokeDirty_{};                  // union of all dabs → lazy coverage clear
};

}  // namespace prima

#endif  // PRIMA_BRUSH_H
