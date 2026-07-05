#include "prima/dab_emitter.h"

#include <algorithm>
#include <cmath>

namespace prima {

void DabEmitter::begin(const BrushParams& params) {
    params_ = params;
    hasLast_ = false;
    strokeStarted_ = false;
    lastX_ = 0.f;
    lastY_ = 0.f;
    lastPressure_ = 1.f;
    distanceToNextDab_ = 0.f;
    hasPending_ = false;
}

DabPoint DabEmitter::resolveDab(float x, float y, float pressure) const {
    float p = std::clamp(pressure, 0.f, 1.f);
    float radius = params_.radius * params_.sizeResponse.apply(p);
    radius = std::max(radius, kMinRadius);
    float flow = params_.flow * params_.flowResponse.apply(p);
    flow = std::clamp(flow, 0.f, 1.f);
    return DabPoint{x, y, radius, flow};
}

void DabEmitter::emitDab(float x, float y, float pressure,
                         const std::function<void(const DabPoint&)>& out) {
    DabPoint dab = resolveDab(x, y, pressure);
    out(dab);
    strokeStarted_ = true;
    // Recompute the spacing step from THIS dab's resolved radius: the interval
    // tracks the (pressure-varying) size along the stroke.
    float step = std::max(0.5f, params_.spacing * 2.f * dab.radius);
    distanceToNextDab_ = step;
}

void DabEmitter::walkSegment(float bx, float by, float bp,
                             const std::function<void(const DabPoint&)>& out) {
    float ax = lastX_;
    float ay = lastY_;
    float ap = lastPressure_;

    float dx = bx - ax;
    float dy = by - ay;
    float segLen = std::sqrt(dx * dx + dy * dy);

    // Zero-length segment: no walking distance, but adopt the new endpoint's
    // pressure so a same-position sample can still update pressure. It must not
    // produce a duplicate dab or consume any leftover distance.
    if (segLen <= 0.f) {
        lastX_ = bx;
        lastY_ = by;
        lastPressure_ = bp;
        return;
    }

    float invLen = 1.f / segLen;
    float traveled = 0.f;  // distance already consumed along this segment

    // Walk forward, dropping a dab every time the carried leftover distance is
    // used up. Interpolate x/y/pressure linearly at each landing point.
    // Use an epsilon to handle float drift in accumulated traveled (e.g. when
    // the spacing divides evenly into the segment length).
    while (distanceToNextDab_ <= segLen - traveled + 1e-5f) {
        traveled += distanceToNextDab_;
        float t = traveled * invLen;
        float px = ax + dx * t;
        float py = ay + dy * t;
        float pp = ap + (bp - ap) * t;
        emitDab(px, py, pp, out);  // resets distanceToNextDab_ from new radius
    }

    // Consume the remaining segment length against the leftover distance so the
    // carry is correct going into the next segment / next addSamples call.
    distanceToNextDab_ -= (segLen - traveled);

    lastX_ = bx;
    lastY_ = by;
    lastPressure_ = bp;
}

void DabEmitter::emitCurveSegment(const RawPoint& p0, const RawPoint& p1,
                                  const RawPoint& p2, const RawPoint& p3,
                                  const std::function<void(const DabPoint&)>& out) {
    if (p1.x == p2.x && p1.y == p2.y) {
        // Degenerate zero-length pending segment (duplicate consecutive raw
        // samples): nothing to walk. Adopt p2's pressure at the current point,
        // same as walkSegment's own zero-length skip.
        lastX_ = p2.x;
        lastY_ = p2.y;
        lastPressure_ = p2.pressure;
        return;
    }
    for (int i = 1; i <= kCurveSubdivisions; ++i) {
        float t = static_cast<float>(i) / static_cast<float>(kCurveSubdivisions);
        float x = catmullRom(p0.x, p1.x, p2.x, p3.x, t);
        float y = catmullRom(p0.y, p1.y, p2.y, p3.y, t);
        float pr = std::clamp(
            catmullRom(p0.pressure, p1.pressure, p2.pressure, p3.pressure, t),
            0.f, 1.f);
        walkSegment(x, y, pr, out);
    }
}

void DabEmitter::addSamplesImpl(const InputSample* samples, int count,
                                const std::function<void(const DabPoint&)>& out) {
    if (samples == nullptr || count <= 0) return;

    int start = 0;
    if (!hasLast_) {
        // First sample of the stroke: stamp a pen-down dab immediately at its
        // exact position — no walking needed.
        const InputSample& s = samples[0];
        lastX_ = s.x;
        lastY_ = s.y;
        lastPressure_ = s.pressure;
        hasLast_ = true;
        emitDab(s.x, s.y, s.pressure, out);
        start = 1;
    }

    for (int i = start; i < count; ++i) {
        const InputSample& s = samples[i];
        RawPoint cur{s.x, s.y, s.pressure};
        if (!hasPending_) {
            // Second raw sample of the stroke: opens the first pending
            // segment [p1_, p2_], with p0_ duplicating p1_ since no real
            // point exists before the pen-down sample.
            p1_ = RawPoint{lastX_, lastY_, lastPressure_};
            p0_ = p1_;
            p2_ = cur;
            hasPending_ = true;
        } else {
            // cur supplies p3, the tangent neighbor that lets the pending
            // segment [p1_, p2_] be curve-fit and walked now.
            emitCurveSegment(p0_, p1_, p2_, cur, out);
            p0_ = p1_;
            p1_ = p2_;
            p2_ = cur;
        }
    }
}

void DabEmitter::endImpl(const std::function<void(const DabPoint&)>& out) {
    // Tap guarantee: a stroke that received at least one sample (hasLast_) but
    // never emitted a dab must still leave exactly one dab. In practice the
    // pen-down stamp in addSamplesImpl already emits that first dab, so this is a
    // defensive backstop and normally does nothing.
    //
    // Degenerate begin()+end() with no addSamples call: hasLast_ is false, so
    // there is no known coordinate to stamp — we deliberately emit nothing. The
    // "tap" contract is about an addSamples() call carrying a single point
    // followed by end(), which already stamped its pen-down dab.
    if (hasLast_ && !strokeStarted_) {
        emitDab(lastX_, lastY_, lastPressure_, out);
    }

    if (hasPending_) {
        // Finalize the final pending segment with a duplicated end tangent —
        // no real point exists after the last raw sample.
        emitCurveSegment(p0_, p1_, p2_, p2_, out);
        hasPending_ = false;
    }
}

}  // namespace prima
