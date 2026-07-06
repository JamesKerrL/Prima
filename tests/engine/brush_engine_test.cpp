#include "prima/brush.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <iostream>
#include <vector>

#include <gtest/gtest.h>

#include "prima/brush_math.h"
#include "prima/canvas.h"
#include "prima/image_io.h"

using prima::accumulateCoverage;
using prima::BrushEngine;
using prima::BrushParams;
using prima::Canvas;
using prima::dabCoverage;
using prima::InputSample;
using prima::RectI;
using prima::resolveStrokeCoverage;
using prima::Rgba;
using prima::saveImagePng;
using prima::unionCoverage;

namespace {

Rgba readPixel(const Canvas& canvas, int x, int y) {
    const uint8_t* p = canvas.pixels();
    std::size_t idx = (static_cast<std::size_t>(y) * canvas.width() + x) * 4;
    return Rgba{p[idx + 0], p[idx + 1], p[idx + 2], p[idx + 3]};
}

void saveCanvasToPng(const Canvas& canvas, const char* filename) {
    if (saveImagePng(filename, canvas.pixels(), canvas.width(), canvas.height())) {
        std::cout << "Saved test image: " << filename << std::endl;
    } else {
        std::cerr << "Failed to save image: " << filename << std::endl;
    }
}

}  // namespace

// 1. Single-dab AA edges.
TEST(BrushEngineTest, SingleDabHasAntiAliasedEdgesAndFullCenterCoverage) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample s{32.f, 32.f, 1.f};
    engine.addSamples(&s, 1);
    engine.endStroke();

    // Center is fully covered.
    EXPECT_EQ(readPixel(canvas, 32, 32).a, 255);

    // Well beyond radius + 0.5 is untouched.
    EXPECT_EQ(readPixel(canvas, 32 + 20, 32).a, 0);

    // Ring pixel at distance slightly less than outer (radius+0.5) shows
    // partial AA coverage. Pixel (32+9, 32) is at d ~= 9.5 from center (32,32),
    // which sits between inner=7.6 and outer=10.5 at hardness=0.8.
    uint8_t ringAlpha = readPixel(canvas, 32 + 9, 32).a;
    EXPECT_GT(ringAlpha, 0);
    EXPECT_LT(ringAlpha, 255);
}

TEST(BrushEngineTest, SingleDabCenteredOnPixelCenterIsFourWaySymmetric) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample s{32.5f, 32.5f, 1.f};
    engine.addSamples(&s, 1);
    engine.endStroke();

    Rgba right = readPixel(canvas, 42, 32);
    Rgba left = readPixel(canvas, 22, 32);
    Rgba down = readPixel(canvas, 32, 42);
    Rgba up = readPixel(canvas, 32, 22);

    auto eq = [](const Rgba& a, const Rgba& b) {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    };

    EXPECT_TRUE(eq(right, left));
    EXPECT_TRUE(eq(right, down));
    EXPECT_TRUE(eq(right, up));
}

// 2. Opacity cap.
TEST(BrushEngineTest, OpacityCapsSingleDabCenterCoverage) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 1;
    p.opacity = 0.5f;
    p.flow = 1;
    p.color = Rgba{0, 0, 255, 255};

    engine.beginStroke(canvas, p);
    InputSample s{32.f, 32.f, 1.f};
    engine.addSamples(&s, 1);
    engine.endStroke();

    uint8_t expected = static_cast<uint8_t>(std::lround(255.f * p.opacity));
    EXPECT_EQ(readPixel(canvas, 32, 32).a, expected);
}

TEST(BrushEngineTest, OverlappingSaturatedDabsWithinOneStrokeDoNotExceedOpacityCap) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 1;
    p.opacity = 0.5f;
    p.flow = 1;
    p.color = Rgba{0, 0, 255, 255};

    engine.beginStroke(canvas, p);
    // Two dab centers 5px apart (well within 2*radius) so they overlap heavily;
    // both centers individually saturate to full coverage since flow == 1.
    InputSample s1{30.f, 32.f, 1.f};
    engine.addSamples(&s1, 1);
    InputSample s2{35.f, 32.f, 1.f};
    engine.addSamples(&s2, 1);
    engine.endStroke();

    uint8_t expected = static_cast<uint8_t>(std::lround(255.f * p.opacity));

    // Each dab's own center: fully covered by its own dab (cov == 1.0).
    Rgba centerA = readPixel(canvas, 30, 32);
    Rgba centerB = readPixel(canvas, 35, 32);
    // Overlap centroid: covered by both dabs, still saturates to cov == 1.0,
    // never exceeding the opacity cap.
    Rgba overlap = readPixel(canvas, 32, 32);  // between the two centers

    EXPECT_EQ(centerA.a, expected);
    EXPECT_EQ(centerB.a, expected);
    EXPECT_EQ(overlap.a, expected);
}

// 3. Flow build-up.
TEST(BrushEngineTest, OverlappingLowFlowDabsBuildUpCoverageButNeverExceedFullAlpha) {
    BrushParams p;
    p.radius = 10;
    p.hardness = 1;
    p.opacity = 1;
    p.flow = 0.25f;
    p.spacing = 0.01f;  // forces a small step so a short stroke stamps many dabs

    // Stroke A: a very short stroke (few overlapping dabs).
    Canvas canvasA(64, 64);
    canvasA.clear(Rgba{0, 0, 0, 0});
    BrushEngine engineA;
    engineA.beginStroke(canvasA, p);
    InputSample a1{30.f, 32.f, 1.f};
    engineA.addSamples(&a1, 1);
    InputSample a2{32.f, 32.f, 1.f};
    engineA.addSamples(&a2, 1);
    engineA.endStroke();

    // Stroke B: a longer stroke over the same start (many more overlapping dabs
    // pass near the same region).
    Canvas canvasB(64, 64);
    canvasB.clear(Rgba{0, 0, 0, 0});
    BrushEngine engineB;
    engineB.beginStroke(canvasB, p);
    InputSample b1{30.f, 32.f, 1.f};
    engineB.addSamples(&b1, 1);
    InputSample b2{45.f, 32.f, 1.f};
    engineB.addSamples(&b2, 1);
    engineB.endStroke();

    uint8_t alphaA = readPixel(canvasA, 30, 32).a;
    uint8_t alphaB = readPixel(canvasB, 30, 32).a;

    EXPECT_GE(alphaB, alphaA);
    EXPECT_LE(alphaA, 255);
    EXPECT_LE(alphaB, 255);
}

// 4. Straight-alpha correctness / no fringe.
TEST(BrushEngineTest, CenterPixelRgbMatchesBrushColorExactlyWithNoFringe) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample s{32.f, 32.f, 1.f};
    engine.addSamples(&s, 1);
    engine.endStroke();

    Rgba center = readPixel(canvas, 32, 32);
    EXPECT_EQ(center.r, 255);
    EXPECT_EQ(center.g, 0);
    EXPECT_EQ(center.b, 0);
}

// 5. Dirty-rect exactness.
TEST(BrushEngineTest, DirtyRectExactlyMatchesChangedPixelBoundingBox) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    std::vector<uint8_t> before(canvas.pixels(), canvas.pixels() + canvas.byteSize());

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    engine.beginStroke(canvas, p);

    RectI accumulated{};
    InputSample s1{20.f, 20.f, 1.f};
    accumulated.unionWith(engine.addSamples(&s1, 1));
    InputSample s2{45.f, 40.f, 1.f};
    accumulated.unionWith(engine.addSamples(&s2, 1));
    accumulated.unionWith(engine.endStroke());

    // Compute actual bounding box of changed pixels by diffing before/after.
    const uint8_t* after = canvas.pixels();
    int minX = canvas.width(), minY = canvas.height();
    int maxX = -1, maxY = -1;
    for (int y = 0; y < canvas.height(); ++y) {
        for (int x = 0; x < canvas.width(); ++x) {
            std::size_t idx = (static_cast<std::size_t>(y) * canvas.width() + x) * 4;
            if (std::memcmp(before.data() + idx, after + idx, 4) != 0) {
                minX = std::min(minX, x);
                minY = std::min(minY, y);
                maxX = std::max(maxX, x);
                maxY = std::max(maxY, y);
            }
        }
    }

    ASSERT_LE(minX, maxX) << "expected some pixels to change";
    RectI actual{minX, minY, maxX - minX + 1, maxY - minY + 1};

    EXPECT_EQ(accumulated.x, actual.x);
    EXPECT_EQ(accumulated.y, actual.y);
    EXPECT_EQ(accumulated.width, actual.width);
    EXPECT_EQ(accumulated.height, actual.height);
}

TEST(BrushEngineTest, TinySubThresholdSampleReturnsEmptyDirtyRect) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample penDown{32.f, 32.f, 1.f};
    RectI first = engine.addSamples(&penDown, 1);
    EXPECT_FALSE(first.empty());

    InputSample tinyMove{32.01f, 32.f, 1.f};
    RectI second = engine.addSamples(&tinyMove, 1);
    EXPECT_TRUE(second.empty());

    engine.endStroke();
}

// 6. Stroke isolation / batching determinism on pixels.
TEST(BrushEngineTest, IdenticalStrokesOnFreshCanvasesProduceIdenticalPixels) {
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    auto runStroke = [&](Canvas& canvas, BrushEngine& engine) {
        canvas.clear(Rgba{0, 0, 0, 0});
        engine.beginStroke(canvas, p);
        InputSample s1{20.f, 20.f, 1.f};
        engine.addSamples(&s1, 1);
        InputSample s2{45.f, 40.f, 1.f};
        engine.addSamples(&s2, 1);
        engine.endStroke();
    };

    Canvas canvasA(64, 64);
    BrushEngine engineA;
    runStroke(canvasA, engineA);

    Canvas canvasB(64, 64);
    BrushEngine engineB;
    runStroke(canvasB, engineB);

    EXPECT_EQ(std::memcmp(canvasA.pixels(), canvasB.pixels(), canvasA.byteSize()), 0);
}

// 7. Baseline region readback (undo support).
TEST(BrushEngineTest, ReadBaselineRegionReturnsPreStrokePixels) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{5, 6, 7, 8});

    BrushEngine engine;
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample s{32.f, 32.f, 1.f};
    RectI dirty = engine.addSamples(&s, 1);
    engine.endStroke();

    ASSERT_FALSE(dirty.empty());
    // The stroke changed the (fully-covered) dab center on the live canvas...
    EXPECT_EQ(readPixel(canvas, 32, 32).r, 255);

    // ...but the baseline region still holds the original pre-stroke color.
    std::vector<uint8_t> region(static_cast<std::size_t>(dirty.width) * dirty.height * 4);
    ASSERT_TRUE(engine.readBaselineRegion(dirty.x, dirty.y, dirty.width,
                                          dirty.height, region.data()));
    for (std::size_t i = 0; i < region.size(); i += 4) {
        EXPECT_EQ(region[i + 0], 5);
        EXPECT_EQ(region[i + 1], 6);
        EXPECT_EQ(region[i + 2], 7);
        EXPECT_EQ(region[i + 3], 8);
    }
}

TEST(BrushEngineTest, ReadBaselineRegionRejectsOutOfBoundsRect) {
    Canvas canvas(16, 16);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 5;
    p.hardness = 1;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{1, 2, 3, 4};

    engine.beginStroke(canvas, p);
    InputSample s{8.f, 8.f, 1.f};
    engine.addSamples(&s, 1);
    engine.endStroke();

    std::vector<uint8_t> region(4);
    EXPECT_FALSE(engine.readBaselineRegion(-1, 0, 1, 1, region.data()));
    EXPECT_FALSE(engine.readBaselineRegion(0, 0, 100, 1, region.data()));
    EXPECT_FALSE(engine.readBaselineRegion(0, 0, 1, 100, region.data()));
}

TEST(BrushEngineTest, ReadBaselineRegionFailsBeforeAnyStroke) {
    BrushEngine engine;
    std::vector<uint8_t> region(4);
    EXPECT_FALSE(engine.readBaselineRegion(0, 0, 1, 1, region.data()));
}

TEST(BrushEngineTest, BatchingSamplesIntoMultipleAddSamplesCallsMatchesSingleCall) {
    BrushParams p;
    p.radius = 10;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{255, 0, 0, 255};

    InputSample samples[] = {
        InputSample{20.f, 20.f, 1.f},
        InputSample{30.f, 25.f, 1.f},
        InputSample{45.f, 40.f, 1.f},
    };

    // Canvas A: all samples in one addSamples call.
    Canvas canvasA(64, 64);
    canvasA.clear(Rgba{0, 0, 0, 0});
    BrushEngine engineA;
    engineA.beginStroke(canvasA, p);
    engineA.addSamples(samples, 3);
    engineA.endStroke();

    // Canvas B: samples fed one at a time across separate addSamples calls.
    Canvas canvasB(64, 64);
    canvasB.clear(Rgba{0, 0, 0, 0});
    BrushEngine engineB;
    engineB.beginStroke(canvasB, p);
    for (const auto& s : samples) {
        InputSample single = s;
        engineB.addSamples(&single, 1);
    }
    engineB.endStroke();

    EXPECT_EQ(std::memcmp(canvasA.pixels(), canvasB.pixels(), canvasA.byteSize()), 0);
}

// 8. Dual-accumulation (shape ∪ + flow build-up, resolved as min) semantics.

// A single dab must be bit-identical to the pure-kernel pipeline: for one
// stamp, shape >= buildup, so the min-resolve leaves single-dab output exactly
// as it was before the shape cap existed.
TEST(BrushEngineTest, SingleDabOutputUnchangedByShapeCap) {
    for (float flow : {1.0f, 0.3f}) {
        Canvas canvas(64, 64);
        canvas.clear(Rgba{0, 0, 0, 0});

        BrushEngine engine;
        BrushParams p;
        p.radius = 10;
        p.hardness = 0.8f;
        p.opacity = 1;
        p.flow = flow;
        p.color = Rgba{255, 0, 0, 255};

        engine.beginStroke(canvas, p);
        InputSample s{32.f, 32.f, 1.f};
        engine.addSamples(&s, 1);
        engine.endStroke();

        // Edge pixel (41,32): center (41.5,32.5), d = sqrt(9.5^2 + 0.5^2)
        // from the dab center — inside the AA band.
        float d = std::sqrt(9.5f * 9.5f + 0.5f * 0.5f);
        float cov = dabCoverage(d, p.radius, p.hardness);
        ASSERT_GT(cov, 0.f);
        ASSERT_LT(cov, 1.f);

        uint16_t shape = unionCoverage(0, cov);
        uint16_t buildup = accumulateCoverage(0, flow, cov);
        float sa = resolveStrokeCoverage(shape, buildup) / 65535.f;
        uint8_t expected = static_cast<uint8_t>(std::lround(sa * 255.f));

        EXPECT_EQ(readPixel(canvas, 41, 32).a, expected) << "flow=" << flow;
    }
}

// The anti-saturation regression test: a dense stroke at full flow used to
// pile overlapping dabs' partial coverage up to ~1.0 across the whole AA
// band, turning the edge into a hard aliased cut. The shape cap keeps each
// edge pixel at its single-dab geometric coverage.
TEST(BrushEngineTest, OverlappingDabsPreserveAntiAliasedEdge) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 2;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 1;
    p.spacing = 0.15f;  // dab every 0.6px: heavy overlap
    p.color = Rgba{0, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample a{10.f, 32.f, 1.f};
    InputSample b{50.f, 32.f, 1.f};
    engine.addSamples(&a, 1);
    engine.addSamples(&b, 1);
    engine.endStroke();

    // Vertical cross-section at x=30 (mid-stroke). Path runs along y=32.0, so
    // pixel row y's center sits |y + 0.5 - 32| from the path.
    uint8_t interior = readPixel(canvas, 30, 32).a;   // d = 0.5 -> full
    uint8_t nearEdge = readPixel(canvas, 30, 30).a;   // d = 1.5 -> ~0.81 cov
    uint8_t farEdge = readPixel(canvas, 30, 29).a;    // d = 2.5 -> ~0.04 cov
    uint8_t outside = readPixel(canvas, 30, 28).a;    // d = 3.5 -> beyond AA

    EXPECT_EQ(interior, 255);
    // Before the fix this pixel saturated to ~253; the geometric coverage of
    // a radius-2 hardness-0.8 dab at d~1.5 is ~0.81 -> ~207.
    EXPECT_GE(nearEdge, 170);
    EXPECT_LE(nearEdge, 235);
    EXPECT_GT(farEdge, 0);
    EXPECT_LE(farEdge, 40);
    EXPECT_EQ(outside, 0);

    // The cross-section must keep multiple intermediate AA levels per side.
    int intermediate = 0;
    for (int y = 27; y <= 37; ++y) {
        uint8_t alpha = readPixel(canvas, 30, y).a;
        if (alpha > 0 && alpha < 255) ++intermediate;
    }
    EXPECT_GE(intermediate, 4);
}

// Edge quality must not depend on how densely dabs happen to land: tighter
// spacing means more overlapping stamps, which previously darkened the AA
// fringe further. With the shape union it converges on the same edge alpha.
TEST(BrushEngineTest, EdgeAlphaIndependentOfSpacing) {
    auto strokeEdgeAlpha = [](float spacing) -> uint8_t {
        Canvas canvas(64, 64);
        canvas.clear(Rgba{0, 0, 0, 0});

        BrushEngine engine;
        BrushParams p;
        p.radius = 2;
        p.hardness = 0.8f;
        p.opacity = 1;
        p.flow = 1;
        p.spacing = spacing;
        p.color = Rgba{0, 0, 0, 255};

        engine.beginStroke(canvas, p);
        InputSample a{10.f, 32.f, 1.f};
        InputSample b{50.f, 32.f, 1.f};
        engine.addSamples(&a, 1);
        engine.addSamples(&b, 1);
        engine.endStroke();

        return readPixel(canvas, 30, 30).a;  // AA-band pixel, d ~= 1.5
    };

    uint8_t sparse = strokeEdgeAlpha(0.15f);
    uint8_t dense = strokeEdgeAlpha(0.05f);
    EXPECT_NEAR(sparse, dense, 10);
}

// Flow build-up (airbrush) must still work where it should: stroke interiors
// accumulate toward full alpha, while the same stroke's edge pixels stay
// capped at their geometric coverage no matter how many dabs pass.
TEST(BrushEngineTest, LowFlowBuildupStillWorksInInterior) {
    Canvas canvas(64, 64);
    canvas.clear(Rgba{0, 0, 0, 0});

    BrushEngine engine;
    BrushParams p;
    p.radius = 4;
    p.hardness = 0.8f;
    p.opacity = 1;
    p.flow = 0.25f;
    p.spacing = 0.05f;  // dab every 0.4px: many overlapping low-flow dabs
    p.color = Rgba{0, 0, 0, 255};

    engine.beginStroke(canvas, p);
    InputSample a{10.f, 32.f, 1.f};
    InputSample b{54.f, 32.f, 1.f};
    engine.addSamples(&a, 1);
    engine.addSamples(&b, 1);
    engine.endStroke();

    // Interior (d = 0.5): shape is full there, so build-up is what shows —
    // ~23 dabs at flow 0.25 accumulate to near-saturation.
    uint8_t interior = readPixel(canvas, 30, 32).a;
    EXPECT_GE(interior, 230);

    // AA-band pixel (d = 3.5, cov ~0.62): enough dabs passed to build far
    // beyond 0.62 (~0.88 before the fix -> ~224), but the shape cap holds it
    // at its geometric coverage (~159).
    uint8_t edge = readPixel(canvas, 30, 28).a;
    EXPECT_GE(edge, 130);
    EXPECT_LE(edge, 175);
    EXPECT_LT(edge, interior);
}

// Visual output tests for inspecting brush edge quality
TEST(BrushEngineTest, DISABLED_VisualSimpleDiagonalStroke) {
    Canvas canvas(200, 200);
    canvas.clear(Rgba{255, 255, 255, 255});

    BrushEngine engine;
    BrushParams p;
    p.radius = 8;
    p.hardness = 0.7f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{0, 0, 0, 255};
    p.spacing = 0.1f;

    engine.beginStroke(canvas, p);
    // Diagonal stroke from top-left to bottom-right. Only two raw samples —
    // pen-down and pen-up — to mimic a fast mouse swipe where the OS/UI
    // delivers few, widely-spaced pointer events. All intermediate dabs come
    // from DabEmitter's own spacing-walk interpolation over the long segment,
    // which is exactly what happens on a quick real stroke.
    InputSample start{20.0f, 20.0f, 1.0f};
    InputSample end{180.0f, 180.0f, 1.0f};
    engine.addSamples(&start, 1);
    engine.addSamples(&end, 1);
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_diagonal_stroke.png");
    SUCCEED();  // Visual test; always passes so you can inspect the image
}

TEST(BrushEngineTest, DISABLED_VisualHorizontalStroke) {
    Canvas canvas(200, 200);
    canvas.clear(Rgba{255, 255, 255, 255});

    BrushEngine engine;
    BrushParams p;
    p.radius = 8;
    p.hardness = 0.7f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{0, 0, 0, 255};
    p.spacing = 0.1f;

    engine.beginStroke(canvas, p);
    // Horizontal stroke from left to right. Only two raw samples (fast swipe);
    // see comment in VisualSimpleDiagonalStroke above.
    InputSample start{20.0f, 100.0f, 1.0f};
    InputSample end{180.0f, 100.0f, 1.0f};
    engine.addSamples(&start, 1);
    engine.addSamples(&end, 1);
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_horizontal_stroke.png");
    SUCCEED();
}

TEST(BrushEngineTest, DISABLED_VisualSmoothCurvedStroke) {
    Canvas canvas(200, 200);
    canvas.clear(Rgba{255, 255, 255, 255});

    BrushEngine engine;
    BrushParams p;
    p.radius = 8;
    p.hardness = 0.7f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{0, 0, 0, 255};
    p.spacing = 0.1f;

    engine.beginStroke(canvas, p);
    // Sine curve stroke, sampled sparsely (5 raw points instead of 40) to
    // mimic a fast mouse swipe along a curve. DabEmitter Catmull-Rom-fits a
    // curve through each raw sample using its neighbors for tangents, so
    // direction changes round off smoothly instead of chording into straight
    // segments between the few points the OS actually delivered.
    for (int i = 0; i <= 4; ++i) {
        float t = i / 4.0f;
        float x = 30.0f + t * 140.0f;
        float y = 100.0f + 30.0f * std::sin(t * 3.14159f * 2.0f);
        InputSample s{x, y, 1.0f};
        engine.addSamples(&s, 1);
    }
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_curved_stroke.png");
    SUCCEED();
}

TEST(BrushEngineTest, DISABLED_VisualFastZigzagStroke) {
    Canvas canvas(200, 200);
    canvas.clear(Rgba{255, 255, 255, 255});

    BrushEngine engine;
    BrushParams p;
    p.radius = 4;  // thin brush so path smoothing is visible, not swallowed
                   // by the dab's own circular footprint
    p.hardness = 0.7f;
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{0, 0, 0, 255};
    p.spacing = 0.1f;

    engine.beginStroke(canvas, p);
    // A sharp V-shaped zigzag from just 3 raw samples — the most extreme case
    // of a fast stroke through an abrupt direction change. With Catmull-Rom
    // smoothing, the middle vertex should round off into a curve instead of
    // a sharp mitered corner.
    InputSample p1{30.0f, 150.0f, 1.0f};
    InputSample p2{100.0f, 50.0f, 1.0f};
    InputSample p3{170.0f, 150.0f, 1.0f};
    engine.addSamples(&p1, 1);
    engine.addSamples(&p2, 1);
    engine.addSamples(&p3, 1);
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_zigzag_stroke.png");
    SUCCEED();
}

// Small-radius stroke quality: thin brushes are nearly all AA band, so edge
// handling dominates how they look. Sparse 2-sample fast swipes and dense
// slow paths exercise different emitter code paths (see testing policy).
TEST(BrushEngineTest, DISABLED_VisualSmallRadiusStrokes) {
    auto runStroke = [](float radius, float flow,
                        const std::vector<InputSample>& samples,
                        const char* filename) {
        Canvas canvas(200, 200);
        canvas.clear(Rgba{255, 255, 255, 255});

        BrushEngine engine;
        BrushParams p;
        p.radius = radius;
        p.hardness = 0.8f;
        p.opacity = 1;
        p.flow = flow;
        p.spacing = 0.15f;
        p.color = Rgba{0, 0, 0, 255};

        engine.beginStroke(canvas, p);
        for (const InputSample& s : samples) {
            InputSample single = s;
            engine.addSamples(&single, 1);
        }
        engine.endStroke();
        saveCanvasToPng(canvas, filename);
    };

    // Fast swipes: only pen-down and pen-up samples.
    std::vector<InputSample> sparseDiagonal = {
        InputSample{20.f, 20.f, 1.f},
        InputSample{180.f, 180.f, 1.f},
    };
    runStroke(1.f, 1.f, sparseDiagonal, "brush_small_r1_sparse.png");
    runStroke(2.f, 1.f, sparseDiagonal, "brush_small_r2_sparse.png");

    // Slow dense sine path: many closely-spaced raw samples.
    std::vector<InputSample> denseSine;
    for (int i = 0; i <= 40; ++i) {
        float t = i / 40.f;
        float x = 20.f + t * 160.f;
        float y = 100.f + 40.f * std::sin(t * 3.14159f * 2.f);
        denseSine.push_back(InputSample{x, y, 1.f});
    }
    runStroke(2.f, 1.f, denseSine, "brush_small_r2_dense_sine.png");

    // Low-flow airbrush variant: edges must stay AA'd while the interior
    // builds up.
    runStroke(2.f, 0.3f, denseSine, "brush_small_r2_lowflow.png");

    SUCCEED();
}

TEST(BrushEngineTest, DISABLED_VisualHardBrush) {
    Canvas canvas(200, 200);
    canvas.clear(Rgba{255, 255, 255, 255});

    BrushEngine engine;
    BrushParams p;
    p.radius = 8;
    p.hardness = 1.0f;  // Very hard edge
    p.opacity = 1;
    p.flow = 1;
    p.color = Rgba{0, 0, 0, 255};
    p.spacing = 0.1f;

    engine.beginStroke(canvas, p);
    // Horizontal stroke. Only two raw samples (fast swipe); see comment in
    // VisualSimpleDiagonalStroke above.
    InputSample start{20.0f, 100.0f, 1.0f};
    InputSample end{180.0f, 100.0f, 1.0f};
    engine.addSamples(&start, 1);
    engine.addSamples(&end, 1);
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_hard_edge.png");
    SUCCEED();
}
