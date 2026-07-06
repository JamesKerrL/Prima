#include "prima/renderer.h"

#include <cmath>
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

// Like coordCanvas but with a spread factor so filtered averages land away
// from .5 rounding boundaries: R = x*scale, G = y*scale.
Canvas scaledCoordCanvas(int w, int h, int scale) {
    Canvas c(w, h);
    uint8_t* p = c.pixels();
    for (int y = 0; y < h; ++y) {
        for (int x = 0; x < w; ++x) {
            uint8_t* px = p + (static_cast<std::size_t>(y) * w + x) * 4;
            px[0] = static_cast<uint8_t>(x * scale);
            px[1] = static_cast<uint8_t>(y * scale);
            px[2] = 0;
            px[3] = 255;
        }
    }
    return c;
}

Canvas uniformCanvas(int w, int h, Rgba color) {
    Canvas c(w, h);
    c.clear(color);
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

TEST(SoftwareRendererTest, ZoomOutAveragesFootprint) {
    Canvas c = scaledCoordCanvas(8, 8, 10);
    Buffer buf(4, 4);
    SoftwareRenderer r;
    // 0.5x zoom: each target pixel box-averages its 2x2 canvas footprint.
    r.render(c, buf.target(), Viewport{0, 0, 0.5}, Rgba{0, 0, 0, 0});
    EXPECT_EQ(buf.at(0, 0)[0], 5);   // avg of R = {0, 10}
    EXPECT_EQ(buf.at(0, 0)[1], 5);   // avg of G = {0, 10}
    EXPECT_EQ(buf.at(1, 0)[0], 25);  // avg of R = {20, 30}
    EXPECT_EQ(buf.at(3, 2)[0], 65);  // avg of R = {60, 70}
    EXPECT_EQ(buf.at(3, 2)[1], 45);  // avg of G = {40, 50}
}

TEST(SoftwareRendererTest, BoxFilterAveragesExactFootprint) {
    Canvas c = scaledCoordCanvas(8, 8, 10);
    Buffer buf(2, 2);
    SoftwareRenderer r;
    // 0.25x zoom: each target pixel averages a 4x4 canvas block.
    r.render(c, buf.target(), Viewport{0, 0, 0.25}, Rgba{0, 0, 0, 0});
    EXPECT_EQ(buf.at(0, 0)[0], 15);  // avg of R = {0, 10, 20, 30}
    EXPECT_EQ(buf.at(0, 0)[1], 15);
    EXPECT_EQ(buf.at(1, 1)[0], 55);  // avg of R = {40..70}
    EXPECT_EQ(buf.at(1, 1)[1], 55);
    EXPECT_EQ(buf.at(1, 1)[3], 255);
}

TEST(SoftwareRendererTest, BilinearFractionalZoomInterpolates) {
    Canvas c = scaledCoordCanvas(4, 4, 30);
    Buffer buf(6, 6);
    SoftwareRenderer r;
    // 1.5x zoom is fractional magnification -> bilinear.
    r.render(c, buf.target(), Viewport{0, 0, 1.5}, Rgba{0, 0, 0, 0});

    // Target (2,2): sample center maps to canvas (1.6667, 1.6667), i.e. 1/6 of
    // the way from texel 1 to texel 2 -> R = G = 30*(5/6) + 60*(1/6) = 35.
    EXPECT_NEAR(buf.at(2, 2)[0], 35, 1);
    EXPECT_NEAR(buf.at(2, 2)[1], 35, 1);
    // Target (3,3): 2/3 of the way -> 30*(1/6) + 60*(5/6) = 55.
    EXPECT_NEAR(buf.at(3, 3)[0], 55, 1);
    EXPECT_NEAR(buf.at(3, 3)[1], 55, 1);
    EXPECT_EQ(buf.at(2, 2)[3], 255);
}

TEST(SoftwareRendererTest, IntegerZoomUsesNearest) {
    Canvas c = scaledCoordCanvas(4, 4, 30);
    Buffer buf(12, 12);
    SoftwareRenderer r;
    // Integer zoom keeps crisp nearest-neighbor blocks: every output value is
    // an exact canvas value, never an interpolated in-between.
    r.render(c, buf.target(), Viewport{0, 0, 3.0}, Rgba{0, 0, 0, 0});
    for (int y = 0; y < 12; ++y) {
        for (int x = 0; x < 12; ++x) {
            EXPECT_EQ(buf.at(x, y)[0], (x / 3) * 30) << x << "," << y;
            EXPECT_EQ(buf.at(x, y)[1], (y / 3) * 30) << x << "," << y;
        }
    }
}

TEST(SoftwareRendererTest, ZoomAtLeastFourUsesNearest) {
    Canvas c = scaledCoordCanvas(4, 4, 30);
    Buffer buf(10, 10);
    SoftwareRenderer r;
    // Fractional zoom >= 4 stays nearest (pixel inspection).
    r.render(c, buf.target(), Viewport{0, 0, 4.5}, Rgba{0, 0, 0, 0});
    for (int y = 0; y < 10; ++y) {
        for (int x = 0; x < 10; ++x) {
            int expected =
                static_cast<int>(std::floor((x + 0.5) / 4.5)) * 30;
            EXPECT_EQ(buf.at(x, y)[0], expected) << x << "," << y;
        }
    }
}

TEST(SoftwareRendererTest, BorderPolicyClampsTapsAndKeepsBackgroundCut) {
    Rgba red{100, 0, 0, 255};
    Rgba bg{9, 8, 7, 200};
    SoftwareRenderer r;

    // Box filter at a canvas border: the footprint is clipped to the canvas
    // and normalized by the clipped area, so border pixels keep full
    // brightness instead of darkening toward the background.
    {
        Canvas c = uniformCanvas(2, 2, red);
        Buffer buf(4, 4);
        r.render(c, buf.target(), Viewport{-1, -1, 0.5}, bg);
        // Target (0,0) center maps to canvas (0,0): inside; footprint
        // [-1,1)x[-1,1) clips to [0,1)x[0,1).
        EXPECT_EQ(buf.at(0, 0)[0], 100);
        EXPECT_EQ(buf.at(0, 0)[3], 255);
        // Target (2,2) center maps to canvas (4,4): outside -> background.
        EXPECT_EQ(buf.at(2, 2)[0], 9);
        EXPECT_EQ(buf.at(2, 2)[3], 200);
    }

    // Bilinear at a canvas border: taps clamp to the edge (no background
    // bleed), and the inside/outside cut stays the nearest-center test.
    {
        Canvas c = uniformCanvas(2, 2, red);
        Buffer buf(6, 6);
        r.render(c, buf.target(), Viewport{-0.5, -0.5, 1.5}, bg);
        // Target (0,0) center maps to canvas (-0.1667): outside -> background.
        EXPECT_EQ(buf.at(0, 0)[0], 9);
        // Target (1,1) center maps to canvas (0.1667): inside; the left/top
        // taps fall outside and clamp to column/row 0 -> pure red.
        EXPECT_EQ(buf.at(1, 1)[0], 100);
        EXPECT_EQ(buf.at(1, 1)[1], 0);
        EXPECT_EQ(buf.at(1, 1)[3], 255);
    }
}

// Pins the dirty-rect partial-render seam: rendering a sub-rect of the target
// through a shifted sub-viewport (pan + txOffset/zoom, exactly how
// CanvasControl builds it) must byte-match the same region of a full render,
// for every filter.
TEST(SoftwareRendererTest, PartialViewportMatchesFullRender) {
    Canvas c = coordCanvas(8, 8);
    SoftwareRenderer r;
    Rgba bg{9, 8, 7, 200};

    // Awkward fractional zooms/pans so no sample lands exactly on a pixel
    // boundary: box (<1), bilinear (fractional in 1..4), nearest (>=4).
    const double zooms[] = {0.61, 1.37, 4.53};
    const double panX = 0.123, panY = -0.377;

    for (double zoom : zooms) {
        Buffer full(16, 12);
        r.render(c, full.target(), Viewport{panX, panY, zoom}, bg);

        const int tx = 5, ty = 3, w = 7, h = 6;
        Buffer sub(w, h);
        Viewport subVp{panX + tx / zoom, panY + ty / zoom, zoom};
        r.render(c, sub.target(), subVp, bg);

        for (int y = 0; y < h; ++y) {
            for (int x = 0; x < w; ++x) {
                const uint8_t* a = full.at(tx + x, ty + y);
                const uint8_t* b = sub.at(x, y);
                for (int ch = 0; ch < 4; ++ch) {
                    ASSERT_EQ(a[ch], b[ch])
                        << "zoom " << zoom << " px " << x << "," << y
                        << " ch " << ch;
                }
            }
        }
    }
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
