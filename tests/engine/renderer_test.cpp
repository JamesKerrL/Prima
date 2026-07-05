#include "prima/renderer.h"

#include <cstddef>
#include <vector>

#include <gtest/gtest.h>

#include "prima/canvas.h"
#include "prima/viewport.h"

using prima::Canvas;
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
    const uint8_t* at(int x, int y) const {
        return px.data() + (static_cast<std::size_t>(y) * w + x) * 4;
    }
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

TEST(SoftwareRendererTest, IdentityViewportReproducesCanvas) {
    Canvas c = coordCanvas(4, 3);
    Buffer buf(4, 3);
    SoftwareRenderer r;
    r.render(c, buf.target(), Viewport{0, 0, 1.0}, Rgba{0, 0, 0, 0});

    for (int y = 0; y < 3; ++y) {
        for (int x = 0; x < 4; ++x) {
            const uint8_t* p = buf.at(x, y);
            EXPECT_EQ(p[0], x);
            EXPECT_EQ(p[1], y);
            EXPECT_EQ(p[3], 255);
        }
    }
}

TEST(SoftwareRendererTest, PanShiftsSampledPixels) {
    Canvas c = coordCanvas(8, 8);
    Buffer buf(4, 4);
    SoftwareRenderer r;
    // Pan so canvas (2,3) sits at the target origin.
    r.render(c, buf.target(), Viewport{2, 3, 1.0}, Rgba{0, 0, 0, 0});

    const uint8_t* origin = buf.at(0, 0);
    EXPECT_EQ(origin[0], 2);  // canvas x
    EXPECT_EQ(origin[1], 3);  // canvas y
    const uint8_t* p11 = buf.at(1, 1);
    EXPECT_EQ(p11[0], 3);
    EXPECT_EQ(p11[1], 4);
}

TEST(SoftwareRendererTest, ZoomInMagnifies) {
    Canvas c = coordCanvas(4, 4);
    Buffer buf(8, 8);
    SoftwareRenderer r;
    // 2x zoom: each canvas pixel covers a 2x2 block of target pixels.
    r.render(c, buf.target(), Viewport{0, 0, 2.0}, Rgba{0, 0, 0, 0});

    // Target (0,0),(1,0),(0,1),(1,1) all sample canvas (0,0).
    for (int y = 0; y < 2; ++y)
        for (int x = 0; x < 2; ++x)
            EXPECT_EQ(buf.at(x, y)[0], 0) << "target " << x << "," << y;
    // Target (2,0) samples canvas (1,0).
    EXPECT_EQ(buf.at(2, 0)[0], 1);
    EXPECT_EQ(buf.at(3, 0)[0], 1);
}

TEST(SoftwareRendererTest, ZoomOutSkipsPixels) {
    Canvas c = coordCanvas(8, 8);
    Buffer buf(4, 4);
    SoftwareRenderer r;
    // 0.5x zoom: samples every other canvas pixel.
    r.render(c, buf.target(), Viewport{0, 0, 0.5}, Rgba{0, 0, 0, 0});
    EXPECT_EQ(buf.at(0, 0)[0], 1);  // (0+0.5)/0.5 = 1
    EXPECT_EQ(buf.at(1, 0)[0], 3);  // (1+0.5)/0.5 = 3
}

TEST(SoftwareRendererTest, OutsideCanvasGetsBackground) {
    Canvas c = coordCanvas(2, 2);
    Buffer buf(4, 4);
    SoftwareRenderer r;
    Rgba bg{9, 8, 7, 200};
    // Pan negative so the top-left of the target is outside the canvas.
    r.render(c, buf.target(), Viewport{-1, -1, 1.0}, bg);

    // Target (0,0) maps to canvas (-1,-1) -> background.
    const uint8_t* p = buf.at(0, 0);
    EXPECT_EQ(p[0], 9);
    EXPECT_EQ(p[1], 8);
    EXPECT_EQ(p[2], 7);
    EXPECT_EQ(p[3], 200);
    // Target (1,1) maps to canvas (0,0) -> canvas content.
    const uint8_t* q = buf.at(1, 1);
    EXPECT_EQ(q[0], 0);
    EXPECT_EQ(q[3], 255);
}
