#ifndef PRIMA_BRUSH_MATH_H
#define PRIMA_BRUSH_MATH_H

#include <algorithm>
#include <cmath>
#include <cstdint>

#include "prima/canvas.h"

namespace prima {

// Maps a 0..1 pressure value to a multiplicative factor.
struct PressureResponse {
    float minFactor = 1.f;  // value at pressure 0, as fraction of base
    float gamma = 1.f;      // response curve exponent

    float apply(float pressure) const {
        float p = std::clamp(pressure, 0.f, 1.f);
        return minFactor + (1.f - minFactor) * std::pow(p, gamma);
    }
};

// Analytic anti-aliased coverage of a round brush dab at distance d from center.
// The AA half-width is sqrt(2)/2 ≈ 0.7071 so the band is ~1.414 px wide at
// hardness=1, matching the ideal pixel-area integral for a circle.
inline float dabCoverage(float d, float radius, float hardness) {
    constexpr float kHalf = 0.70710678f;
    float inner = hardness * std::max(radius - kHalf, 0.f);
    float outer = radius + kHalf;

    if (d <= inner) return 1.f;
    if (d >= outer) return 0.f;

    if (outer <= inner) {
        // Degenerate: fall back to a clamped linear falloff over [0, outer].
        if (outer <= 0.f) return 0.f;
        return std::clamp(1.f - d / outer, 0.f, 1.f);
    }

    float t = (d - inner) / (outer - inner);
    float cov = 1.f - (3.f * t * t - 2.f * t * t * t);
    return std::clamp(cov, 0.f, 1.f);
}

// Uniform Catmull-Rom spline: position at parameter t in [0,1] along the
// segment between p1 and p2, using p0 and p3 as the neighboring control
// points that shape the tangents at p1 and p2. Passes through p1 exactly at
// t=0 and p2 exactly at t=1. Applied independently per-dimension (x, y,
// pressure all use the same t and their own 4 control values).
inline float catmullRom(float p0, float p1, float p2, float p3, float t) {
    float t2 = t * t;
    float t3 = t2 * t;
    return 0.5f * ((2.f * p1) +
                   (-p0 + p2) * t +
                   (2.f * p0 - 5.f * p1 + 4.f * p2 - p3) * t2 +
                   (-p0 + 3.f * p1 - 3.f * p2 + p3) * t3);
}

// Saturating flow-weighted accumulation into a 16-bit fixed-point wet buffer
// where 65535 represents 1.0. Never decreases below the input c.
inline uint16_t accumulateCoverage(uint16_t c, float flow, float cov) {
    int remaining = 65535 - c;
    float add = flow * cov * remaining;
    int delta = static_cast<int>(std::round(add));
    if (delta > remaining) delta = remaining;
    if (delta == 0 && add > 0.f && c < 65535) delta = 1;
    return static_cast<uint16_t>(c + delta);
}

// Anti-aliased shape union: running max of per-dab geometric coverage. As
// spacing shrinks this converges on the swept-stroke outline's exact coverage,
// so AA edges survive any number of overlapping dabs. Any cov > 0 yields at
// least 1 so touched-pixel dirty rects stay exact (same clamp as
// accumulateCoverage).
inline uint16_t unionCoverage(uint16_t c, float cov) {
    int v = static_cast<int>(std::lround(cov * 65535.f));
    if (v == 0 && cov > 0.f) v = 1;
    if (v > 65535) v = 65535;
    return v > c ? static_cast<uint16_t>(v) : c;
}

// Resolved per-pixel stroke coverage: flow build-up capped by the geometric
// union of dab shapes. Interiors keep airbrush build-up; edge pixels never
// exceed their true area coverage, so the AA fringe can't saturate.
inline uint16_t resolveStrokeCoverage(uint16_t shape, uint16_t buildup) {
    return buildup < shape ? buildup : shape;
}

// Straight-alpha (non-premultiplied) source-over compositing of one pixel.
// sa is the resolved blend alpha in [0,1]; srcColor.a is ignored for alpha math.
inline Rgba blendSourceOver(Rgba dst, Rgba srcColor, float sa) {
    float s = std::clamp(sa, 0.f, 1.f);
    float dA = dst.a / 255.f;
    float outA = s + dA * (1.f - s);

    auto blend = [&](uint8_t sc, uint8_t dc) -> uint8_t {
        if (outA <= 0.f) return 0;
        float outC = (sc * s + dc * dA * (1.f - s)) / outA;
        return static_cast<uint8_t>(std::clamp(std::round(outC), 0.f, 255.f));
    };

    Rgba out;
    out.r = blend(srcColor.r, dst.r);
    out.g = blend(srcColor.g, dst.g);
    out.b = blend(srcColor.b, dst.b);
    out.a = static_cast<uint8_t>(std::clamp(std::round(outA * 255.f), 0.f, 255.f));
    return out;
}

}  // namespace prima

#endif  // PRIMA_BRUSH_MATH_H
