#ifndef PRIMA_DAB_EMITTER_H
#define PRIMA_DAB_EMITTER_H

#include <functional>

#include "prima/brush_math.h"
#include "prima/canvas.h"

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
    float opacity = 1.f;    // 0..1 stroke cap
    float flow = 1.f;       // 0..1 per-dab deposit
    float spacing = 0.15f;  // dab interval as fraction of dab diameter
    PressureResponse sizeResponse;   // pressure -> radius
    PressureResponse flowResponse;   // pressure -> flow
    Rgba color{0, 0, 0, 255};
};

struct DabPoint { float x, y, radius, flow; };  // fully resolved (pressure applied)

// Pixel-free spacing/interpolation walker. Consumes a stream of InputSamples and
// emits DabPoints at spacing intervals along the interpolated path. Holds no
// pixel state — it only interpolates positions/pressure and resolves radius/flow
// through the BrushParams pressure curves.
//
// The single most important property is batching-invariance: feeding the same
// samples as one big addSamples() call or as many single-sample calls produces
// an IDENTICAL DabPoint sequence. This is achieved by carrying the last stored
// point (x/y/pressure) and the leftover distance-until-next-dab across both
// segment boundaries and separate addSamples() calls.
class DabEmitter {
public:
    // Minimum resolved dab radius (px). Below this, low-pressure dabs would
    // vanish or degenerate; the coverage stage scales sub-pixel area separately.
    static constexpr float kMinRadius = 0.3f;

    void begin(const BrushParams& params);

    // Walks last->sample segments, emitting spacing-separated dabs into `out`.
    // F is any callable void(const DabPoint&).
    template <typename F>
    void addSamples(const InputSample* samples, int count, F&& out) {
        std::function<void(const DabPoint&)> sink(std::forward<F>(out));
        addSamplesImpl(samples, count, sink);
    }

    // Finalizes the stroke. Guarantees >= 1 dab for a "tap" (a stroke whose only
    // sample never advanced past the pen-down position). In the normal case the
    // pen-down dab already satisfies this, so end() emits nothing extra.
    template <typename F>
    void end(F&& out) {
        std::function<void(const DabPoint&)> sink(std::forward<F>(out));
        endImpl(sink);
    }

private:
    // Resolve pressure -> (radius, flow) at one interpolated point.
    DabPoint resolveDab(float x, float y, float pressure) const;

    // Emits a dab at (x, y, pressure) and refreshes the leftover distance from
    // that dab's freshly resolved radius. Marks the stroke started.
    void emitDab(float x, float y, float pressure,
                 const std::function<void(const DabPoint&)>& out);

    // Walk one segment from the stored last point to (bx, by, bp), emitting dabs
    // and carrying leftover distance. Updates the stored last point to the
    // segment end regardless of whether any dab landed.
    void walkSegment(float bx, float by, float bp,
                     const std::function<void(const DabPoint&)>& out);

    void addSamplesImpl(const InputSample* samples, int count,
                        const std::function<void(const DabPoint&)>& out);
    void endImpl(const std::function<void(const DabPoint&)>& out);

    BrushParams params_{};
    bool hasLast_ = false;          // a stored last point exists (pen-down done)
    bool strokeStarted_ = false;    // at least one dab has been emitted
    float lastX_ = 0.f;
    float lastY_ = 0.f;
    float lastPressure_ = 1.f;
    float distanceToNextDab_ = 0.f; // leftover distance carried across segments/calls
};

}  // namespace prima

#endif  // PRIMA_DAB_EMITTER_H
