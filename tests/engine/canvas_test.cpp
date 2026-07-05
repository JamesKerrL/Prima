#include "prima/canvas.h"

#include <cstddef>

#include <gtest/gtest.h>

using prima::Canvas;
using prima::Rgba;

TEST(CanvasTest, NewCanvasHasExpectedDimensions) {
    Canvas c(8, 4);
    EXPECT_EQ(c.width(), 8);
    EXPECT_EQ(c.height(), 4);
    EXPECT_EQ(c.stride(), 8 * Canvas::kBytesPerPixel);
    EXPECT_EQ(c.byteSize(), static_cast<std::size_t>(8 * 4 * 4));
}

TEST(CanvasTest, NewCanvasIsZeroed) {
    Canvas c(2, 2);
    const uint8_t* p = c.pixels();
    for (std::size_t i = 0; i < c.byteSize(); ++i) {
        EXPECT_EQ(p[i], 0) << "byte " << i;
    }
}

TEST(CanvasTest, ClearFillsEveryPixel) {
    Canvas c(3, 3);
    c.clear(Rgba{10, 20, 30, 40});
    const uint8_t* p = c.pixels();
    for (std::size_t i = 0; i < c.byteSize(); i += 4) {
        EXPECT_EQ(p[i + 0], 10);
        EXPECT_EQ(p[i + 1], 20);
        EXPECT_EQ(p[i + 2], 30);
        EXPECT_EQ(p[i + 3], 40);
    }
}

TEST(CanvasTest, BrushDabPaintsCenterAndLeavesFarCornerUntouched) {
    Canvas c(9, 9);
    c.brushDab(4, 4, 2, Rgba{255, 0, 0, 255});
    const uint8_t* p = c.pixels();

    const std::size_t center = (static_cast<std::size_t>(4) * 9 + 4) * 4;
    EXPECT_EQ(p[center + 0], 255);
    EXPECT_EQ(p[center + 3], 255);

    const std::size_t corner = 0;  // (0,0) is well outside radius 2
    EXPECT_EQ(p[corner + 3], 0);
}

TEST(CanvasTest, BrushDabClampsAtBounds) {
    Canvas c(4, 4);
    // Centered off-canvas: must not crash and must still paint the overlap.
    c.brushDab(-1, -1, 3, Rgba{1, 2, 3, 4});
    const uint8_t* p = c.pixels();
    EXPECT_EQ(p[0], 1);
    EXPECT_EQ(p[3], 4);
}

TEST(CanvasTest, NegativeRadiusIsNoOp) {
    Canvas c(4, 4);
    c.brushDab(2, 2, -5, Rgba{9, 9, 9, 9});
    const uint8_t* p = c.pixels();
    for (std::size_t i = 0; i < c.byteSize(); ++i) {
        EXPECT_EQ(p[i], 0);
    }
}
