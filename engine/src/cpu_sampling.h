#ifndef PRIMA_SRC_CPU_SAMPLING_H
#define PRIMA_SRC_CPU_SAMPLING_H

#include <algorithm>
#include <cmath>
#include <cstddef>

#include "prima/renderer.h"

namespace prima {

// Filter used to resample the canvas into the target. Shared by
// SoftwareRenderer and the D3D11 backend's CPU-compose stage (private header;
// not part of the engine's public include surface), so both backends stay
// byte-identical by construction.
enum class SampleFilter {
    Nearest,   // integer zooms and zoom >= 4 (crisp pixel inspection)
    Bilinear,  // fractional zooms in (1, 4)
    Box,       // zoom < 1: area-average over the source footprint
};

inline SampleFilter chooseSampleFilter(double zoom) {
    if (zoom < 1.0) return SampleFilter::Box;
    if (zoom >= 4.0) return SampleFilter::Nearest;
    // Epsilon snap: exact floor(zoom)==zoom would miss 1.9999999….
    if (std::abs(zoom - std::round(zoom)) < 1e-9) return SampleFilter::Nearest;
    return SampleFilter::Bilinear;
}

namespace detail {

inline void writeRgba(uint8_t* dst, Rgba c) {
    dst[0] = c.r;
    dst[1] = c.g;
    dst[2] = c.b;
    dst[3] = c.a;
}

// Nearest-neighbor loop — byte-for-byte the original sampling, kept for
// integer zooms so pixel-exact rendering (and its tests) are unchanged.
inline void sampleNearest(const Canvas& canvas, const RenderTarget& target,
                          const Viewport& viewport, Rgba background) {
    const int cw = canvas.width();
    const int ch = canvas.height();
    const int cstride = canvas.stride();
    const uint8_t* src = canvas.pixels();

    for (int ty = 0; ty < target.height; ++ty) {
        uint8_t* dstRow =
            target.pixels + static_cast<std::size_t>(ty) * target.stride;
        // Sample at the target pixel's center, mapped into canvas space.
        const int cy =
            static_cast<int>(std::floor(viewport.targetToCanvasY(ty + 0.5)));
        const bool rowInside = (cy >= 0 && cy < ch);

        for (int tx = 0; tx < target.width; ++tx) {
            uint8_t* dst =
                dstRow + static_cast<std::size_t>(tx) * Canvas::kBytesPerPixel;
            const int cx =
                static_cast<int>(std::floor(viewport.targetToCanvasX(tx + 0.5)));

            if (rowInside && cx >= 0 && cx < cw) {
                const uint8_t* s =
                    src + static_cast<std::size_t>(cy) * cstride +
                    static_cast<std::size_t>(cx) * Canvas::kBytesPerPixel;
                dst[0] = s[0];
                dst[1] = s[1];
                dst[2] = s[2];
                dst[3] = s[3];
            } else {
                writeRgba(dst, background);
            }
        }
    }
}

// Bilinear magnification. The inside/outside decision stays the nearest-center
// test so the canvas/background boundary remains a crisp cut; taps clamp to
// the canvas edge (no background bleed). Accumulation is premultiplied —
// averaging straight RGBA across differing alphas drags in transparent
// pixels' RGB and causes dark halos.
inline void sampleBilinear(const Canvas& canvas, const RenderTarget& target,
                           const Viewport& viewport, Rgba background) {
    const int cw = canvas.width();
    const int ch = canvas.height();
    const int cstride = canvas.stride();
    const uint8_t* src = canvas.pixels();

    for (int ty = 0; ty < target.height; ++ty) {
        uint8_t* dstRow =
            target.pixels + static_cast<std::size_t>(ty) * target.stride;
        const double cyCenter = viewport.targetToCanvasY(ty + 0.5);
        const int cyNearest = static_cast<int>(std::floor(cyCenter));
        const bool rowInside = (cyNearest >= 0 && cyNearest < ch);

        const double sy = cyCenter - 0.5;
        const int iy = static_cast<int>(std::floor(sy));
        const float fy = static_cast<float>(sy - iy);
        const int y0 = std::clamp(iy, 0, ch - 1);
        const int y1 = std::clamp(iy + 1, 0, ch - 1);

        for (int tx = 0; tx < target.width; ++tx) {
            uint8_t* dst =
                dstRow + static_cast<std::size_t>(tx) * Canvas::kBytesPerPixel;
            const double cxCenter = viewport.targetToCanvasX(tx + 0.5);
            const int cxNearest = static_cast<int>(std::floor(cxCenter));

            if (!rowInside || cxNearest < 0 || cxNearest >= cw) {
                writeRgba(dst, background);
                continue;
            }

            const double sx = cxCenter - 0.5;
            const int ix = static_cast<int>(std::floor(sx));
            const float fx = static_cast<float>(sx - ix);
            const int x0 = std::clamp(ix, 0, cw - 1);
            const int x1 = std::clamp(ix + 1, 0, cw - 1);

            const uint8_t* s00 = src + static_cast<std::size_t>(y0) * cstride +
                                 static_cast<std::size_t>(x0) * Canvas::kBytesPerPixel;
            const uint8_t* s10 = src + static_cast<std::size_t>(y0) * cstride +
                                 static_cast<std::size_t>(x1) * Canvas::kBytesPerPixel;
            const uint8_t* s01 = src + static_cast<std::size_t>(y1) * cstride +
                                 static_cast<std::size_t>(x0) * Canvas::kBytesPerPixel;
            const uint8_t* s11 = src + static_cast<std::size_t>(y1) * cstride +
                                 static_cast<std::size_t>(x1) * Canvas::kBytesPerPixel;

            const float w00 = (1.f - fx) * (1.f - fy);
            const float w10 = fx * (1.f - fy);
            const float w01 = (1.f - fx) * fy;
            const float w11 = fx * fy;

            const float a00 = s00[3], a10 = s10[3], a01 = s01[3], a11 = s11[3];
            const float outA = w00 * a00 + w10 * a10 + w01 * a01 + w11 * a11;

            if (outA <= 0.f) {
                dst[0] = 0;
                dst[1] = 0;
                dst[2] = 0;
                dst[3] = 0;
                continue;
            }

            for (int c = 0; c < 3; ++c) {
                const float pm = w00 * s00[c] * a00 + w10 * s10[c] * a10 +
                                 w01 * s01[c] * a01 + w11 * s11[c] * a11;
                dst[c] = static_cast<uint8_t>(
                    std::clamp(std::lround(pm / outA), 0L, 255L));
            }
            dst[3] = static_cast<uint8_t>(
                std::clamp(std::lround(outA), 0L, 255L));
        }
    }
}

// Box-filter minification: area-weighted average of the canvas pixels under
// each target pixel's source footprint, with fractional edge weights.
// Normalized by the *clipped* footprint area so canvas borders don't darken.
// Premultiplied accumulation for the same halo reason as bilinear. Cost is
// O(visible source pixels) per frame — the same order as a zoom-1 full
// render; a mip/downsample cache is a noted future optimization.
inline void sampleBox(const Canvas& canvas, const RenderTarget& target,
                      const Viewport& viewport, Rgba background) {
    const int cw = canvas.width();
    const int ch = canvas.height();
    const int cstride = canvas.stride();
    const uint8_t* src = canvas.pixels();

    for (int ty = 0; ty < target.height; ++ty) {
        uint8_t* dstRow =
            target.pixels + static_cast<std::size_t>(ty) * target.stride;
        const int cyNearest = static_cast<int>(
            std::floor(viewport.targetToCanvasY(ty + 0.5)));
        const bool rowInside = (cyNearest >= 0 && cyNearest < ch);

        // Vertical footprint of this target row, clipped to the canvas.
        const double y0f = std::max(viewport.targetToCanvasY(ty), 0.0);
        const double y1f = std::min(viewport.targetToCanvasY(ty + 1.0),
                                    static_cast<double>(ch));
        const int cy0 = static_cast<int>(std::floor(y0f));
        const int cy1 = static_cast<int>(std::ceil(y1f));  // exclusive

        for (int tx = 0; tx < target.width; ++tx) {
            uint8_t* dst =
                dstRow + static_cast<std::size_t>(tx) * Canvas::kBytesPerPixel;
            const int cxNearest = static_cast<int>(
                std::floor(viewport.targetToCanvasX(tx + 0.5)));

            if (!rowInside || cxNearest < 0 || cxNearest >= cw ||
                y1f <= y0f) {
                writeRgba(dst, background);
                continue;
            }

            const double x0f = std::max(viewport.targetToCanvasX(tx), 0.0);
            const double x1f = std::min(viewport.targetToCanvasX(tx + 1.0),
                                        static_cast<double>(cw));
            if (x1f <= x0f) {
                writeRgba(dst, background);
                continue;
            }
            const int cx0 = static_cast<int>(std::floor(x0f));
            const int cx1 = static_cast<int>(std::ceil(x1f));  // exclusive

            double sumW = 0.0;
            double sumA = 0.0;
            double sumPm[3] = {0.0, 0.0, 0.0};

            for (int cy = cy0; cy < cy1; ++cy) {
                const double wy = std::min<double>(cy + 1.0, y1f) -
                                  std::max<double>(cy, y0f);
                if (wy <= 0.0) continue;
                const uint8_t* srow =
                    src + static_cast<std::size_t>(cy) * cstride;
                for (int cx = cx0; cx < cx1; ++cx) {
                    const double wx = std::min<double>(cx + 1.0, x1f) -
                                      std::max<double>(cx, x0f);
                    if (wx <= 0.0) continue;
                    const double w = wx * wy;
                    const uint8_t* s =
                        srow + static_cast<std::size_t>(cx) * Canvas::kBytesPerPixel;
                    const double a = s[3];
                    sumW += w;
                    sumA += w * a;
                    sumPm[0] += w * s[0] * a;
                    sumPm[1] += w * s[1] * a;
                    sumPm[2] += w * s[2] * a;
                }
            }

            if (sumW <= 0.0 || sumA <= 0.0) {
                dst[0] = 0;
                dst[1] = 0;
                dst[2] = 0;
                dst[3] = 0;
                continue;
            }

            for (int c = 0; c < 3; ++c) {
                dst[c] = static_cast<uint8_t>(
                    std::clamp(std::lround(sumPm[c] / sumA), 0L, 255L));
            }
            dst[3] = static_cast<uint8_t>(
                std::clamp(std::lround(sumA / sumW), 0L, 255L));
        }
    }
}

}  // namespace detail

// Filtered viewport sampling of `canvas` into `target`. The filter is chosen
// once per call from the viewport zoom (constant per render): box average for
// minification, bilinear for fractional magnification below 4x, nearest for
// integer zooms and >= 4x. A target pixel's filtered value depends only on
// its absolute canvas-space sample position, so partial (sub-viewport) renders
// compose seamlessly with full renders.
inline void sampleCanvasToTarget(const Canvas& canvas, const RenderTarget& target,
                                 const Viewport& viewport, Rgba background) {
    switch (chooseSampleFilter(viewport.zoom)) {
        case SampleFilter::Box:
            detail::sampleBox(canvas, target, viewport, background);
            break;
        case SampleFilter::Bilinear:
            detail::sampleBilinear(canvas, target, viewport, background);
            break;
        case SampleFilter::Nearest:
        default:
            detail::sampleNearest(canvas, target, viewport, background);
            break;
    }
}

}  // namespace prima

#endif  // PRIMA_SRC_CPU_SAMPLING_H
