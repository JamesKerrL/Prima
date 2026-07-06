// Tests for the Direct3D 11 backend plumbing. The whole file is compiled only
// when the backend is (belt and braces: CMake already excludes it otherwise).
#ifdef PRIMA_HAS_D3D11

#include "prima/d3d11_renderer.h"

#include <cstddef>
#include <cstring>
#include <memory>
#include <vector>

#include <gtest/gtest.h>

#include "prima/canvas.h"
#include "prima/renderer.h"
#include "prima/viewport.h"

using prima::Canvas;
using prima::createD3d11Renderer;
using prima::D3d11Driver;
using prima::Renderer;
using prima::RenderTarget;
using prima::Rgba;
using prima::SoftwareRenderer;
using prima::Viewport;

namespace {

// A simple RGBA8 target buffer for tests.
struct Buffer {
    int w, h;
    std::vector<uint8_t> px;
    explicit Buffer(int width, int height)
        : w(width), h(height), px(static_cast<std::size_t>(width) * height * 4, 0) {}
    RenderTarget target() { return RenderTarget{px.data(), w, h, w * 4}; }
};

// Fill a canvas so pixel (x,y) encodes its coordinates: R=x, G=y, B=0, A=255.
Canvas coordCanvas(int w, int h) {
    Canvas c(w, h);
    uint8_t* p = c.pixels();
    for (int y = 0; y < h; ++y) {
        for (int x = 0; x < w; ++x) {
            uint8_t* px = p + (static_cast<std::size_t>(y) * w + x) * 4;
            px[0] = static_cast<uint8_t>(x);
            px[1] = static_cast<uint8_t>(y);
            px[2] = 0;
            px[3] = 255;
        }
    }
    return c;
}

}  // namespace

// WARP ships with Windows and needs no GPU or display, so creating a WARP
// device must always succeed on a supported machine. This is the assertion
// headless CI relies on — a hard failure here means the plumbing is broken,
// not that the environment lacks a GPU.
TEST(D3d11RendererTest, WarpDeviceCreates) {
    std::unique_ptr<Renderer> r = createD3d11Renderer(D3d11Driver::Warp);
    ASSERT_NE(r, nullptr);
    EXPECT_STREQ(r->name(), "d3d11");
}

TEST(D3d11RendererTest, AutoDeviceCreatesHardwareOrWarp) {
    std::unique_ptr<Renderer> r = createD3d11Renderer(D3d11Driver::Auto);
    if (!r) GTEST_SKIP() << "D3D11 unavailable on this machine";
    EXPECT_STREQ(r->name(), "d3d11");
}

// The plumbing milestone's contract: output is byte-identical to the software
// backend (the GPU round-trip is a lossless R8G8B8A8_UNORM copy), across
// viewports that exercise identity, fractional pan+zoom, negative pan, and
// background fill outside the canvas.
TEST(D3d11RendererTest, MatchesSoftwareRendererOutput) {
    std::unique_ptr<Renderer> d3d = createD3d11Renderer(D3d11Driver::Warp);
    ASSERT_NE(d3d, nullptr);
    SoftwareRenderer sw;

    Canvas c = coordCanvas(16, 12);
    const Rgba bg{9, 8, 7, 200};
    const Viewport viewports[] = {
        Viewport{0, 0, 1.0},        // identity (nearest)
        Viewport{1.25, 2.75, 2.5},  // fractional pan, zoom in (bilinear)
        Viewport{-3, -2, 1.0},      // negative pan (background at top-left)
        Viewport{0, 0, 0.5},        // zoom out (box filter)
        Viewport{0.4, -1.1, 1.5},   // fractional zoom in (bilinear)
        Viewport{-0.7, 0.3, 0.3},   // deep zoom out (box filter)
        Viewport{0, 0, 4.5},        // >= 4x stays nearest
    };

    for (const Viewport& vp : viewports) {
        // Target larger than the canvas so background fill is exercised too.
        Buffer expected(24, 20);
        Buffer actual(24, 20);
        sw.render(c, expected.target(), vp, bg);
        d3d->render(c, actual.target(), vp, bg);
        EXPECT_EQ(expected.px, actual.px)
            << "pan=(" << vp.panX << "," << vp.panY << ") zoom=" << vp.zoom;
    }
}

// Successive renders at different target sizes must recreate the cached
// texture pair correctly (size-change path).
TEST(D3d11RendererTest, HandlesTargetSizeChanges) {
    std::unique_ptr<Renderer> d3d = createD3d11Renderer(D3d11Driver::Warp);
    ASSERT_NE(d3d, nullptr);
    SoftwareRenderer sw;

    Canvas c = coordCanvas(8, 8);
    const Rgba bg{0, 0, 0, 0};
    const Viewport vp{0, 0, 1.0};

    for (int size : {4, 12, 4, 32}) {
        Buffer expected(size, size);
        Buffer actual(size, size);
        sw.render(c, expected.target(), vp, bg);
        d3d->render(c, actual.target(), vp, bg);
        EXPECT_EQ(expected.px, actual.px) << "target size " << size;
    }
}

#endif  // PRIMA_HAS_D3D11
