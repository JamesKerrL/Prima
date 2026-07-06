#include "prima/renderer.h"

#include "cpu_sampling.h"

namespace prima {

void SoftwareRenderer::render(const Canvas& canvas, const RenderTarget& target,
                              const Viewport& viewport, Rgba background) {
    sampleCanvasToTarget(canvas, target, viewport, background);
}

}  // namespace prima
