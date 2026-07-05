#ifndef PRIMA_RENDERER_H
#define PRIMA_RENDERER_H

#include <cstdint>

#include "prima/canvas.h"
#include "prima/viewport.h"

namespace prima {

// A caller-owned RGBA8 destination for rendering. `pixels` may point into memory
// the engine does not own (e.g. a UI framebuffer), enabling zero-copy present.
struct RenderTarget {
    uint8_t* pixels;
    int width;
    int height;
    int stride;  // bytes per row
};

// A render backend composites a document into a RenderTarget through a Viewport.
// Software is backend #1; GPU backends (OpenGL, Vulkan, Metal) implement this
// same interface later without changing callers.
class Renderer {
public:
    virtual ~Renderer() = default;

    // Composite `canvas` into `target` under `viewport`. Target pixels that map
    // outside the canvas are filled with `background`.
    virtual void render(const Canvas& canvas, const RenderTarget& target,
                        const Viewport& viewport, Rgba background) = 0;

    virtual const char* name() const = 0;
};

// CPU raster backend: nearest-neighbor sampling of the canvas through the
// viewport. No external dependencies; fully headless and unit-testable.
class SoftwareRenderer : public Renderer {
public:
    void render(const Canvas& canvas, const RenderTarget& target,
                const Viewport& viewport, Rgba background) override;

    const char* name() const override { return "software"; }
};

}  // namespace prima

#endif  // PRIMA_RENDERER_H
