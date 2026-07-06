#ifndef PRIMA_SRC_CPU_SAMPLING_H
#define PRIMA_SRC_CPU_SAMPLING_H

#include <cmath>
#include <cstddef>

#include "prima/renderer.h"

namespace prima {

// Nearest-neighbor viewport sampling of `canvas` into `target`. Shared by
// SoftwareRenderer and the D3D11 backend's CPU-compose stage (private header;
// not part of the engine's public include surface).
inline void sampleCanvasToTarget(const Canvas& canvas, const RenderTarget& target,
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
                dst[0] = background.r;
                dst[1] = background.g;
                dst[2] = background.b;
                dst[3] = background.a;
            }
        }
    }
}

}  // namespace prima

#endif  // PRIMA_SRC_CPU_SAMPLING_H
