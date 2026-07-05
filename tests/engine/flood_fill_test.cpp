#include "prima/flood_fill.h"

#include <cstdint>
#include <vector>

#include <gtest/gtest.h>

#include "prima/canvas.h"

using prima::Canvas;
using prima::floodFill;
using prima::RectI;
using prima::Rgba;

namespace {

// A tiny RGBA8 test image with helpers, so tests read against a plain buffer
// (the flood fill core is Canvas-free by design).
struct Image {
    int w, h;
    std::vector<uint8_t> px;

    Image(int width, int height, Rgba fill)
        : w(width), h(height), px(static_cast<std::size_t>(width) * height * 4) {
        for (int i = 0; i < width * height; ++i) set(i % width, i / width, fill);
    }

    int stride() const { return w * 4; }

    void set(int x, int y, Rgba c) {
        std::size_t i = (static_cast<std::size_t>(y) * w + x) * 4;
        px[i + 0] = c.r;
        px[i + 1] = c.g;
        px[i + 2] = c.b;
        px[i + 3] = c.a;
    }

    Rgba get(int x, int y) const {
        std::size_t i = (static_cast<std::size_t>(y) * w + x) * 4;
        return Rgba{px[i + 0], px[i + 1], px[i + 2], px[i + 3]};
    }

    RectI fill(int x, int y, Rgba c, int tol) {
        return floodFill(px.data(), w, h, stride(), x, y, c, tol);
    }
};

bool eq(Rgba a, Rgba b) {
    return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
}

const Rgba kRed{255, 0, 0, 255};
const Rgba kGreen{0, 255, 0, 255};
const Rgba kBlue{0, 0, 255, 255};

}  // namespace

TEST(FloodFillTest, UniformCanvasFillsEveryPixel) {
    Image img(5, 5, kRed);
    RectI dirty = img.fill(2, 2, kGreen, 0);

    EXPECT_EQ(dirty.x, 0);
    EXPECT_EQ(dirty.y, 0);
    EXPECT_EQ(dirty.width, 5);
    EXPECT_EQ(dirty.height, 5);
    for (int y = 0; y < 5; ++y)
        for (int x = 0; x < 5; ++x) EXPECT_TRUE(eq(img.get(x, y), kGreen));
}

TEST(FloodFillTest, StopsAtColorBoundary) {
    // Blue horizontal wall on row 2 splits the canvas into top and bottom.
    Image img(5, 5, kRed);
    for (int x = 0; x < 5; ++x) img.set(x, 2, kBlue);

    RectI dirty = img.fill(2, 0, kGreen, 0);

    // Top half (rows 0-1) is filled; wall and bottom untouched.
    for (int x = 0; x < 5; ++x) {
        EXPECT_TRUE(eq(img.get(x, 0), kGreen));
        EXPECT_TRUE(eq(img.get(x, 1), kGreen));
        EXPECT_TRUE(eq(img.get(x, 2), kBlue));
        EXPECT_TRUE(eq(img.get(x, 3), kRed));
        EXPECT_TRUE(eq(img.get(x, 4), kRed));
    }
    EXPECT_EQ(dirty.x, 0);
    EXPECT_EQ(dirty.y, 0);
    EXPECT_EQ(dirty.width, 5);
    EXPECT_EQ(dirty.height, 2);
}

TEST(FloodFillTest, EnclosedRegionDoesNotLeakOutside) {
    // A blue ring around a single red interior pixel at (2,2) on a red field.
    Image img(5, 5, kRed);
    for (int i = 1; i <= 3; ++i) {
        img.set(i, 1, kBlue);
        img.set(i, 3, kBlue);
        img.set(1, i, kBlue);
        img.set(3, i, kBlue);
    }

    RectI dirty = img.fill(2, 2, kGreen, 0);

    EXPECT_TRUE(eq(img.get(2, 2), kGreen));
    // Everything outside the ring stays red.
    EXPECT_TRUE(eq(img.get(0, 0), kRed));
    EXPECT_TRUE(eq(img.get(4, 4), kRed));
    // Only the one interior pixel changed.
    EXPECT_EQ(dirty.x, 2);
    EXPECT_EQ(dirty.y, 2);
    EXPECT_EQ(dirty.width, 1);
    EXPECT_EQ(dirty.height, 1);
}

TEST(FloodFillTest, FourConnectivityDoesNotCrossDiagonals) {
    // Two red squares touching only at a corner; a blue diagonal separates them.
    //   R R B
    //   R R B
    //   B B R   <- seed of a separate red region at (2,2)
    Image img(3, 3, kBlue);
    img.set(0, 0, kRed);
    img.set(1, 0, kRed);
    img.set(0, 1, kRed);
    img.set(1, 1, kRed);
    img.set(2, 2, kRed);  // diagonally adjacent to the 2x2 block via (1,1)

    RectI dirty = img.fill(0, 0, kGreen, 0);

    // The 2x2 block is filled...
    EXPECT_TRUE(eq(img.get(0, 0), kGreen));
    EXPECT_TRUE(eq(img.get(1, 1), kGreen));
    // ...but the diagonally-touching red pixel is NOT reached (4-connectivity).
    EXPECT_TRUE(eq(img.get(2, 2), kRed));
    EXPECT_EQ(dirty.width, 2);
    EXPECT_EQ(dirty.height, 2);
}

TEST(FloodFillTest, SeedOutOfBoundsIsNoOp) {
    Image img(4, 4, kRed);
    std::vector<uint8_t> before = img.px;

    EXPECT_TRUE(img.fill(-1, 0, kGreen, 0).empty());
    EXPECT_TRUE(img.fill(0, -1, kGreen, 0).empty());
    EXPECT_TRUE(img.fill(4, 0, kGreen, 0).empty());
    EXPECT_TRUE(img.fill(0, 4, kGreen, 0).empty());

    EXPECT_EQ(img.px, before);
}

TEST(FloodFillTest, FillingWithSeedColorIsNoOp) {
    Image img(4, 4, kRed);
    std::vector<uint8_t> before = img.px;

    RectI dirty = img.fill(1, 1, kRed, 0);

    EXPECT_TRUE(dirty.empty());
    EXPECT_EQ(img.px, before);
}

TEST(FloodFillTest, ToleranceIncludesNearColorsAndExcludesFarOnes) {
    // Center red; a near-red neighbor (diff 10) and a far neighbor (diff 40).
    Image img(3, 1, Rgba{100, 100, 100, 255});
    img.set(0, 0, Rgba{110, 100, 100, 255});  // near: max channel diff 10
    img.set(1, 0, Rgba{100, 100, 100, 255});  // seed
    img.set(2, 0, Rgba{140, 100, 100, 255});  // far: max channel diff 40

    RectI dirty = img.fill(1, 0, kGreen, 20);

    EXPECT_TRUE(eq(img.get(0, 0), kGreen));   // within tolerance -> filled
    EXPECT_TRUE(eq(img.get(1, 0), kGreen));   // seed -> filled
    EXPECT_TRUE(eq(img.get(2, 0), Rgba{140, 100, 100, 255}));  // excluded
    EXPECT_EQ(dirty.x, 0);
    EXPECT_EQ(dirty.width, 2);
}

TEST(FloodFillTest, ToleranceBoundaryIsInclusive) {
    Image img(2, 1, Rgba{100, 100, 100, 255});
    img.set(0, 0, Rgba{100, 100, 100, 255});  // seed
    img.set(1, 0, Rgba{105, 100, 100, 255});  // diff exactly 5

    RectI dirty = img.fill(0, 0, kGreen, 5);

    EXPECT_TRUE(eq(img.get(1, 0), kGreen));  // diff == tolerance -> included
    EXPECT_EQ(dirty.width, 2);
}

TEST(FloodFillTest, DirtyRectIsTightBoundingBoxOfChangedPixels) {
    // Red canvas with a red 2x2 pocket carved out of a blue field.
    Image img(6, 6, kBlue);
    img.set(2, 3, kRed);
    img.set(3, 3, kRed);
    img.set(2, 4, kRed);
    img.set(3, 4, kRed);

    RectI dirty = img.fill(2, 3, kGreen, 0);

    EXPECT_EQ(dirty.x, 2);
    EXPECT_EQ(dirty.y, 3);
    EXPECT_EQ(dirty.width, 2);
    EXPECT_EQ(dirty.height, 2);
}

TEST(FloodFillTest, SinglePixelRegion) {
    Image img(1, 1, kRed);
    RectI dirty = img.fill(0, 0, kGreen, 0);
    EXPECT_TRUE(eq(img.get(0, 0), kGreen));
    EXPECT_EQ(dirty.width, 1);
    EXPECT_EQ(dirty.height, 1);
}

TEST(FloodFillTest, FillColorMatchingSeedWithinToleranceTerminates) {
    // Guards against infinite loops: the fill color still matches the seed test
    // (diff 3 <= tolerance 10), so a color-only stop condition would loop.
    Image img(4, 4, Rgba{100, 100, 100, 255});
    RectI dirty = img.fill(0, 0, Rgba{103, 100, 100, 255}, 10);

    EXPECT_EQ(dirty.width, 4);
    EXPECT_EQ(dirty.height, 4);
    for (int y = 0; y < 4; ++y)
        for (int x = 0; x < 4; ++x)
            EXPECT_TRUE(eq(img.get(x, y), Rgba{103, 100, 100, 255}));
}

TEST(FloodFillTest, CanvasWrapperDelegatesToPureFill) {
    Canvas c(4, 4);
    c.clear(kRed);
    RectI dirty = c.floodFill(1, 1, kBlue, 0);

    EXPECT_EQ(dirty.width, 4);
    EXPECT_EQ(dirty.height, 4);
    const uint8_t* p = c.pixels();
    EXPECT_EQ(p[0], 0);
    EXPECT_EQ(p[2], 255);
}
