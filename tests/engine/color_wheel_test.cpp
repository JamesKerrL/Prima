#include "prima/color_wheel.h"

#include <cmath>

#include <gtest/gtest.h>

#include "prima/color.h"

using prima::ColorWheel;
using prima::Hsv;
using prima::HsvFromRgba;
using prima::Rgba;

namespace {

Rgba pixelAt(const uint8_t* buf, int width, int x, int y) {
    const std::size_t i =
        (static_cast<std::size_t>(y) * width + x) * ColorWheel::kBytesPerPixel;
    return Rgba{buf[i + 0], buf[i + 1], buf[i + 2], buf[i + 3]};
}

// Shortest circular distance between two hue angles (degrees), so a hue that
// wraps to just under 360 still reads as "close to 0".
double hueDistance(double a, double b) {
    double d = std::fmod(std::fabs(a - b), 360.0);
    return d > 180.0 ? 360.0 - d : d;
}

}  // namespace

TEST(ColorWheelTest, RingAndTriangleHaveExpectedDimensions) {
    ColorWheel wheel(64, 8);
    EXPECT_EQ(wheel.RingWidth(), 64);
    EXPECT_EQ(wheel.RingHeight(), 64);
    // Triangle is bounded by the inner circle: diameter = outer - 2*thickness.
    EXPECT_EQ(wheel.TriangleWidth(), 64 - 2 * 8);
    EXPECT_EQ(wheel.TriangleHeight(), 64 - 2 * 8);
}

TEST(ColorWheelTest, RingCenterIsTransparent) {
    ColorWheel wheel(64, 8);
    Rgba center = pixelAt(wheel.RingPixels(), wheel.RingWidth(),
                          wheel.RingWidth() / 2, wheel.RingHeight() / 2);
    EXPECT_EQ(center.a, 0);
}

TEST(ColorWheelTest, RingCornerIsTransparent) {
    ColorWheel wheel(64, 8);
    Rgba corner = pixelAt(wheel.RingPixels(), wheel.RingWidth(), 0, 0);
    EXPECT_EQ(corner.a, 0);
}

TEST(ColorWheelTest, RingSampledAngleMatchesHue) {
    ColorWheel wheel(200, 20);
    const int w = wheel.RingWidth();
    const double cx = w / 2.0;
    const double cy = w / 2.0;
    const double radius = (w / 2.0) - 10.0;  // mid-annulus

    // Sample along +x (hue 0 -> red) and at 90 degrees CCW (math orientation:
    // +x=0deg, screen-up = +y in math coords => pixel row above center).
    int xRight = static_cast<int>(cx + radius);
    int yRight = static_cast<int>(cy);
    Rgba red = pixelAt(wheel.RingPixels(), w, xRight, yRight);
    Hsv hsvRed = HsvFromRgba(Rgba{red.r, red.g, red.b, 255});
    EXPECT_LE(hueDistance(hsvRed.h, 0.0), 5.0) << "h=" << hsvRed.h;

    int xUp = static_cast<int>(cx);
    int yUp = static_cast<int>(cy - radius);
    Rgba up = pixelAt(wheel.RingPixels(), w, xUp, yUp);
    Hsv hsvUp = HsvFromRgba(Rgba{up.r, up.g, up.b, 255});
    EXPECT_LE(hueDistance(hsvUp.h, 90.0), 5.0) << "h=" << hsvUp.h;
}

TEST(ColorWheelTest, TriangleTopCenterIsFullHue) {
    ColorWheel wheel(200, 20);
    wheel.SetHue(0.0);
    const int w = wheel.TriangleWidth();

    Rgba top = pixelAt(wheel.TrianglePixels(), w, w / 2, 1);
    // Near the hue vertex: high alpha, and close to pure red.
    EXPECT_GT(top.a, 200);
    EXPECT_GT(top.r, 200);
    EXPECT_LT(top.g, 60);
    EXPECT_LT(top.b, 60);
}

TEST(ColorWheelTest, TriangleBottomCenterBlendsBlackAndWhite) {
    ColorWheel wheel(200, 20);
    wheel.SetHue(0.0);
    const int w = wheel.TriangleWidth();

    // The base edge (black-white, ny=-0.5) sits at y = 0.75*w - 0.5 in pixel
    // space (see the header's pixel<->Cartesian mapping); sample just above
    // it, at the base's center column -> midpoint of black and white = gray.
    const int y = static_cast<int>(0.75 * w) - 2;
    Rgba bottom = pixelAt(wheel.TrianglePixels(), w, w / 2, y);
    EXPECT_GT(bottom.a, 200);
    EXPECT_NEAR(bottom.r, bottom.g, 10);
    EXPECT_NEAR(bottom.g, bottom.b, 10);
}

TEST(ColorWheelTest, TriangleOutsideCornerIsTransparent) {
    ColorWheel wheel(200, 20);
    Rgba corner = pixelAt(wheel.TrianglePixels(), wheel.TriangleWidth(), 0, 0);
    EXPECT_EQ(corner.a, 0);
}

TEST(ColorWheelTest, SetHueChangesTriangleTopColor) {
    ColorWheel wheel(200, 20);
    const int w = wheel.TriangleWidth();

    wheel.SetHue(0.0);
    Rgba red = pixelAt(wheel.TrianglePixels(), w, w / 2, 1);

    wheel.SetHue(120.0);
    Rgba green = pixelAt(wheel.TrianglePixels(), w, w / 2, 1);

    EXPECT_NE(red.r, green.r);
    EXPECT_GT(green.g, green.r);
}

TEST(ColorWheelTest, ZeroSizeDoesNotCrash) {
    ColorWheel wheel(0, 0);
    EXPECT_EQ(wheel.RingWidth(), 0);
    EXPECT_EQ(wheel.TriangleWidth(), 0);
}

TEST(ColorWheelTest, ThicknessLargerThanOuterRadiusIsClamped) {
    // Must not crash and must produce a non-negative-sized triangle.
    ColorWheel wheel(50, 1000);
    EXPECT_GE(wheel.TriangleWidth(), 0);
}
