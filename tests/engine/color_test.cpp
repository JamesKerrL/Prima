#include "prima/color.h"

#include <cmath>

#include <gtest/gtest.h>

using prima::AngleFromHue;
using prima::HsvFromRgba;
using prima::Hsv;
using prima::HueFromAngle;
using prima::Rgba;
using prima::RgbaFromHsv;
using prima::Sv;
using prima::SvFromTrianglePoint;
using prima::TrianglePointFromSv;

namespace {
constexpr double kPi = 3.14159265358979323846;
constexpr double kEps = 1e-6;
}  // namespace

TEST(ColorTest, PureRedIsHue0FullSaturationFullValue) {
    Hsv hsv = HsvFromRgba(Rgba{255, 0, 0, 255});
    EXPECT_NEAR(hsv.h, 0.0, kEps);
    EXPECT_NEAR(hsv.s, 1.0, kEps);
    EXPECT_NEAR(hsv.v, 1.0, kEps);
}

TEST(ColorTest, GrayHasZeroSaturation) {
    Hsv hsv = HsvFromRgba(Rgba{128, 128, 128, 255});
    EXPECT_NEAR(hsv.s, 0.0, kEps);
    EXPECT_NEAR(hsv.v, 128.0 / 255.0, kEps);
}

TEST(ColorTest, BlackIsZeroValue) {
    Hsv hsv = HsvFromRgba(Rgba{0, 0, 0, 255});
    EXPECT_NEAR(hsv.v, 0.0, kEps);
    EXPECT_NEAR(hsv.s, 0.0, kEps);
}

TEST(ColorTest, WhiteIsZeroSaturationFullValue) {
    Hsv hsv = HsvFromRgba(Rgba{255, 255, 255, 255});
    EXPECT_NEAR(hsv.s, 0.0, kEps);
    EXPECT_NEAR(hsv.v, 1.0, kEps);
}

TEST(ColorTest, KnownHueValues) {
    // Green, blue, yellow, cyan, magenta at full saturation/value.
    EXPECT_NEAR(HsvFromRgba(Rgba{0, 255, 0, 255}).h, 120.0, kEps);
    EXPECT_NEAR(HsvFromRgba(Rgba{0, 0, 255, 255}).h, 240.0, kEps);
    EXPECT_NEAR(HsvFromRgba(Rgba{255, 255, 0, 255}).h, 60.0, kEps);
    EXPECT_NEAR(HsvFromRgba(Rgba{0, 255, 255, 255}).h, 180.0, kEps);
    EXPECT_NEAR(HsvFromRgba(Rgba{255, 0, 255, 255}).h, 300.0, kEps);
}

TEST(ColorTest, HsvToRgbaKnownValues) {
    Rgba red = RgbaFromHsv(Hsv{0.0, 1.0, 1.0});
    EXPECT_EQ(red.r, 255);
    EXPECT_EQ(red.g, 0);
    EXPECT_EQ(red.b, 0);

    Rgba green = RgbaFromHsv(Hsv{120.0, 1.0, 1.0});
    EXPECT_EQ(green.r, 0);
    EXPECT_EQ(green.g, 255);
    EXPECT_EQ(green.b, 0);

    Rgba white = RgbaFromHsv(Hsv{0.0, 0.0, 1.0});
    EXPECT_EQ(white.r, 255);
    EXPECT_EQ(white.g, 255);
    EXPECT_EQ(white.b, 255);

    Rgba black = RgbaFromHsv(Hsv{0.0, 0.0, 0.0});
    EXPECT_EQ(black.r, 0);
    EXPECT_EQ(black.g, 0);
    EXPECT_EQ(black.b, 0);
}

TEST(ColorTest, AlphaPassesThroughUnchanged) {
    Rgba c = RgbaFromHsv(Hsv{0.0, 1.0, 1.0}, 128);
    EXPECT_EQ(c.a, 128);
}

TEST(ColorTest, RoundTripRgbaHsvRgba) {
    const Rgba samples[] = {
        {255, 0, 0, 255}, {0, 255, 0, 255},   {0, 0, 255, 255},
        {12, 200, 77, 255}, {200, 12, 233, 255}, {128, 128, 128, 255},
        {0, 0, 0, 255}, {255, 255, 255, 255},
    };
    for (const Rgba& original : samples) {
        Hsv hsv = HsvFromRgba(original);
        Rgba roundTripped = RgbaFromHsv(hsv, original.a);
        EXPECT_NEAR(roundTripped.r, original.r, 1) << "r for hue " << hsv.h;
        EXPECT_NEAR(roundTripped.g, original.g, 1) << "g for hue " << hsv.h;
        EXPECT_NEAR(roundTripped.b, original.b, 1) << "b for hue " << hsv.h;
    }
}

TEST(ColorTest, HueFromAngleKnownValues) {
    EXPECT_NEAR(HueFromAngle(0.0), 0.0, kEps);
    EXPECT_NEAR(HueFromAngle(kPi), 180.0, kEps);
    EXPECT_NEAR(HueFromAngle(-kPi / 2.0), 270.0, kEps);
    EXPECT_NEAR(HueFromAngle(2.0 * kPi), 0.0, kEps);
}

TEST(ColorTest, HueAngleRoundTrip) {
    for (double hue = 0.0; hue < 360.0; hue += 15.0) {
        double angle = AngleFromHue(hue);
        double roundTripped = HueFromAngle(angle);
        EXPECT_NEAR(roundTripped, hue, kEps) << "hue=" << hue;
    }
}

TEST(ColorTest, TriangleHueVertexIsFullColor) {
    // (s=1, v=1) maps to the top vertex (0, 1).
    auto p = TrianglePointFromSv(1.0, 1.0);
    EXPECT_NEAR(p.x, 0.0, kEps);
    EXPECT_NEAR(p.y, 1.0, kEps);
}

TEST(ColorTest, TriangleBlackVertexIsZeroValue) {
    // (v=0) maps to the bottom-left vertex regardless of s.
    auto p = TrianglePointFromSv(0.5, 0.0);
    EXPECT_NEAR(p.x, -0.8660254037844386, kEps);
    EXPECT_NEAR(p.y, -0.5, kEps);
}

TEST(ColorTest, TriangleWhiteVertexIsZeroSaturation) {
    // (s=0, v=1) maps to the bottom-right vertex.
    auto p = TrianglePointFromSv(0.0, 1.0);
    EXPECT_NEAR(p.x, 0.8660254037844386, kEps);
    EXPECT_NEAR(p.y, -0.5, kEps);
}

TEST(ColorTest, TriangleSvRoundTrip) {
    // Note: s is only meaningful when v > 0 (at v=0 every s maps to the same
    // black vertex), so only v>0 samples are used for the round trip.
    const double svPairs[][2] = {
        {1.0, 1.0}, {0.0, 1.0}, {0.5, 0.5},
        {0.25, 0.75}, {1.0, 0.5}, {0.0, 0.0},
    };
    for (const auto& sv : svPairs) {
        auto p = TrianglePointFromSv(sv[0], sv[1]);
        Sv back = SvFromTrianglePoint(p.x, p.y);
        EXPECT_NEAR(back.s, sv[0], kEps) << "s for s=" << sv[0] << " v=" << sv[1];
        EXPECT_NEAR(back.v, sv[1], kEps) << "v for s=" << sv[0] << " v=" << sv[1];
    }
}

TEST(ColorTest, TriangleOutsidePointClampsToValidSv) {
    // Far outside the triangle: must still return an (s,v) in [0,1].
    Sv sv = SvFromTrianglePoint(10.0, 10.0);
    EXPECT_GE(sv.s, 0.0);
    EXPECT_LE(sv.s, 1.0);
    EXPECT_GE(sv.v, 0.0);
    EXPECT_LE(sv.v, 1.0);
}
