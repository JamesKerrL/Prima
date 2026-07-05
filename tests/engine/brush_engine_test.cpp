#include "prima/brush.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <iostream>
#include <vector>

#include <gtest/gtest.h>

#include "prima/canvas.h"
#include "prima/image_io.h"

using prima::BrushEngine;
using prima::BrushParams;
using prima::Canvas;
using prima::InputSample;
using prima::RectI;
using prima::Rgba;
using prima::saveImagePng;

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
    // Diagonal stroke from top-left to bottom-right
    for (int i = 0; i <= 20; ++i) {
        float t = i / 20.0f;
        InputSample s{20.0f + t * 160.0f, 20.0f + t * 160.0f, 1.0f};
        engine.addSamples(&s, 1);
    }
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
    // Horizontal stroke from left to right
    for (int i = 0; i <= 20; ++i) {
        float t = i / 20.0f;
        InputSample s{20.0f + t * 160.0f, 100.0f, 1.0f};
        engine.addSamples(&s, 1);
    }
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
    // Sine curve stroke
    for (int i = 0; i <= 40; ++i) {
        float t = i / 40.0f;
        float x = 30.0f + t * 140.0f;
        float y = 100.0f + 30.0f * std::sin(t * 3.14159f * 2.0f);
        InputSample s{x, y, 1.0f};
        engine.addSamples(&s, 1);
    }
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_curved_stroke.png");
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
    // Horizontal stroke
    for (int i = 0; i <= 20; ++i) {
        float t = i / 20.0f;
        InputSample s{20.0f + t * 160.0f, 100.0f, 1.0f};
        engine.addSamples(&s, 1);
    }
    engine.endStroke();

    saveCanvasToPng(canvas, "brush_hard_edge.png");
    SUCCEED();
}
