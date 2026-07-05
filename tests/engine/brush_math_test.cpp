#include "prima/brush_math.h"

#include <cmath>
#include <cstdint>

#include <gtest/gtest.h>

#include "prima/canvas.h"

using prima::accumulateCoverage;
using prima::blendSourceOver;
using prima::dabCoverage;
using prima::PressureResponse;
using prima::Rgba;

namespace {
constexpr float kTol = 1e-3f;
}

// ---------------------------------------------------------------------------
// PressureResponse::apply
// ---------------------------------------------------------------------------

TEST(PressureResponseTest, ConstantWhenMinFactorIsOne) {
    PressureResponse pr{1.f, 1.f};
    EXPECT_NEAR(pr.apply(0.f), 1.f, kTol);
    EXPECT_NEAR(pr.apply(0.5f), 1.f, kTol);
    EXPECT_NEAR(pr.apply(1.f), 1.f, kTol);
}

TEST(PressureResponseTest, LinearRampWhenMinFactorZeroGammaOne) {
    PressureResponse pr{0.f, 1.f};
    EXPECT_NEAR(pr.apply(0.f), 0.f, kTol);
    EXPECT_NEAR(pr.apply(0.5f), 0.5f, kTol);
    EXPECT_NEAR(pr.apply(1.f), 1.f, kTol);
}

TEST(PressureResponseTest, ExactValuesForMinFactorPointThreeGammaTwo) {
    PressureResponse pr{0.3f, 2.f};
    // factor(p) = 0.3 + 0.7 * p^2
    EXPECT_NEAR(pr.apply(0.f), 0.3f, kTol);
    EXPECT_NEAR(pr.apply(0.5f), 0.3f + 0.7f * 0.25f, kTol);
    EXPECT_NEAR(pr.apply(1.f), 1.f, kTol);
}

TEST(PressureResponseTest, MonotonicNonDecreasingAcrossSamples) {
    struct Params {
        float minFactor, gamma;
    };
    const Params cases[] = {{0.f, 1.f}, {0.2f, 0.5f}, {0.3f, 2.f}, {0.f, 3.f}};
    const float samples[] = {0.f, 0.25f, 0.5f, 0.75f, 1.f};

    for (const auto& params : cases) {
        PressureResponse pr{params.minFactor, params.gamma};
        float prev = pr.apply(samples[0]);
        for (std::size_t i = 1; i < std::size(samples); ++i) {
            float cur = pr.apply(samples[i]);
            EXPECT_GE(cur, prev - kTol) << "minFactor=" << params.minFactor
                                         << " gamma=" << params.gamma
                                         << " at sample index " << i;
            prev = cur;
        }
    }
}

TEST(PressureResponseTest, OutputStaysWithinMinFactorToOneBounds) {
    struct Params {
        float minFactor, gamma;
    };
    const Params cases[] = {{0.f, 1.f}, {0.3f, 2.f}, {0.6f, 0.5f}, {1.f, 3.f}};
    const float pressures[] = {0.f, 0.1f, 0.33f, 0.5f, 0.75f, 0.9f, 1.f};

    for (const auto& params : cases) {
        PressureResponse pr{params.minFactor, params.gamma};
        for (float p : pressures) {
            float v = pr.apply(p);
            EXPECT_GE(v, params.minFactor - kTol);
            EXPECT_LE(v, 1.f + kTol);
        }
    }
}

// ---------------------------------------------------------------------------
// dabCoverage
// ---------------------------------------------------------------------------

TEST(DabCoverageTest, CenterIsFullCoverage) {
    EXPECT_NEAR(dabCoverage(0.f, 4.f, 1.f), 1.f, kTol);
    EXPECT_NEAR(dabCoverage(0.f, 1.f, 0.5f), 1.f, kTol);
    EXPECT_NEAR(dabCoverage(0.f, 10.f, 0.f), 1.f, kTol);
}

TEST(DabCoverageTest, ZeroBeyondOuterEdge) {
    constexpr float kHalf = 0.70710678f;
    float radius = 4.f;
    float outer = radius + kHalf;
    EXPECT_NEAR(dabCoverage(outer, radius, 1.f), 0.f, kTol);
    EXPECT_NEAR(dabCoverage(outer + 5.f, radius, 1.f), 0.f, kTol);
    EXPECT_NEAR(dabCoverage(outer, radius, 0.f), 0.f, kTol);
}

TEST(DabCoverageTest, MidpointOfAABandIsHalfCoverage) {
    constexpr float kHalf = 0.70710678f;
    float radius = 4.f;
    float hardness = 0.5f;
    float inner = hardness * std::max(radius - kHalf, 0.f);
    float outer = radius + kHalf;
    float mid = (inner + outer) / 2.f;
    EXPECT_NEAR(dabCoverage(mid, radius, hardness), 0.5f, kTol);
}

TEST(DabCoverageTest, HardnessGrowsInnerRadius) {
    float radius = 6.f;
    float d = 3.f;  // fixed mid distance

    float covSoft = dabCoverage(d, radius, 0.f);
    float covMed = dabCoverage(d, radius, 0.5f);
    float covHard = dabCoverage(d, radius, 1.f);

    // As hardness increases, inner grows, so coverage at a fixed distance
    // should be non-decreasing (harder brush = crisper, fuller coverage
    // closer to the edge).
    EXPECT_GE(covMed, covSoft - kTol);
    EXPECT_GE(covHard, covMed - kTol);

    // Confirm inner actually grows with hardness for this radius.
    constexpr float kHalf = 0.70710678f;
    float innerSoft = 0.f * std::max(radius - kHalf, 0.f);
    float innerMed = 0.5f * std::max(radius - kHalf, 0.f);
    float innerHard = 1.f * std::max(radius - kHalf, 0.f);
    EXPECT_LT(innerSoft, innerMed);
    EXPECT_LT(innerMed, innerHard);
}

TEST(DabCoverageTest, MonotonicNonIncreasingInDistance) {
    float radius = 5.f;
    float hardness = 0.4f;
    float outer = radius + 0.5f;

    float prev = dabCoverage(0.f, radius, hardness);
    const int steps = 20;
    for (int i = 1; i <= steps; ++i) {
        float d = outer * (static_cast<float>(i) / steps);
        float cur = dabCoverage(d, radius, hardness);
        EXPECT_LE(cur, prev + kTol) << "d=" << d;
        prev = cur;
    }
}

TEST(DabCoverageTest, AlwaysWithinZeroOneRange) {
    float radii[] = {0.f, 1.f, 5.f, 20.f};
    float hardnesses[] = {0.f, 0.3f, 0.7f, 1.f};
    for (float radius : radii) {
        for (float hardness : hardnesses) {
            for (float d = -1.f; d <= radius + 2.f; d += 0.5f) {
                float cov = dabCoverage(d, radius, hardness);
                EXPECT_GE(cov, 0.f);
                EXPECT_LE(cov, 1.f);
            }
        }
    }
}

// ---------------------------------------------------------------------------
// accumulateCoverage
// ---------------------------------------------------------------------------

TEST(AccumulateCoverageTest, FullFlowSaturatesInstantly) {
    uint16_t result = accumulateCoverage(0, 1.0f, 1.0f);
    EXPECT_EQ(result, 65535);
}

TEST(AccumulateCoverageTest, PartialFlowBuildsAsymptotically) {
    uint16_t c = 0;
    uint16_t prev = 0;
    for (int i = 0; i < 100; ++i) {
        uint16_t next = accumulateCoverage(c, 0.25f, 1.0f);
        EXPECT_GE(next, prev);
        if (prev < 65535) {
            // Should be strictly increasing until it (nearly) saturates.
            EXPECT_GT(next, prev) << "iteration " << i;
        }
        EXPECT_LE(next, 65535);
        prev = next;
        c = next;
    }
    // After many iterations it should have approached saturation closely.
    EXPECT_GE(c, 65000);
    EXPECT_LE(c, 65535);
}

TEST(AccumulateCoverageTest, NeverExceedsMaxNearSaturation) {
    const uint16_t highCs[] = {65000, 65500, 65534, 65535};
    const float flows[] = {0.1f, 0.5f, 0.9f, 1.0f};
    const float covs[] = {0.1f, 0.5f, 0.9f, 1.0f};

    for (uint16_t c : highCs) {
        for (float flow : flows) {
            for (float cov : covs) {
                uint16_t result = accumulateCoverage(c, flow, cov);
                EXPECT_LE(result, 65535);
            }
        }
    }
}

TEST(AccumulateCoverageTest, NeverDecreasesBelowInputC) {
    const uint16_t cs[] = {0, 100, 32768, 65000, 65535};
    for (uint16_t c : cs) {
        EXPECT_GE(accumulateCoverage(c, 0.f, 1.f), c);
        EXPECT_GE(accumulateCoverage(c, 1.f, 0.f), c);
        EXPECT_GE(accumulateCoverage(c, 0.f, 0.f), c);
        EXPECT_GE(accumulateCoverage(c, 0.5f, 0.5f), c);
    }
}

// ---------------------------------------------------------------------------
// blendSourceOver
// ---------------------------------------------------------------------------

TEST(BlendSourceOverTest, OverTransparentDestinationReproducesSourceColor) {
    Rgba dst{0, 0, 0, 0};
    Rgba src{200, 100, 50, 255};
    float sa = 0.6f;

    Rgba out = blendSourceOver(dst, src, sa);

    EXPECT_NEAR(out.r, src.r, 1);
    EXPECT_NEAR(out.g, src.g, 1);
    EXPECT_NEAR(out.b, src.b, 1);
    EXPECT_NEAR(out.a, static_cast<int>(std::round(sa * 255.f)), 1);
}

TEST(BlendSourceOverTest, OverOpaqueDestinationFullAlphaReplacesExactly) {
    Rgba dst{10, 20, 30, 255};
    Rgba src{200, 100, 50, 255};

    Rgba out = blendSourceOver(dst, src, 1.f);

    EXPECT_EQ(out.r, src.r);
    EXPECT_EQ(out.g, src.g);
    EXPECT_EQ(out.b, src.b);
    EXPECT_EQ(out.a, 255);
}

TEST(BlendSourceOverTest, PartialAlphaMatchesHandComputedFormula) {
    Rgba dst{40, 80, 120, 128};   // dA = 128/255
    Rgba src{220, 60, 10, 0};     // src.a ignored for alpha math
    float sa = 0.3f;

    float dA = 128.f / 255.f;
    float outA = sa + dA * (1.f - sa);
    float expR = (src.r * sa + dst.r * dA * (1.f - sa)) / outA;
    float expG = (src.g * sa + dst.g * dA * (1.f - sa)) / outA;
    float expB = (src.b * sa + dst.b * dA * (1.f - sa)) / outA;

    Rgba out = blendSourceOver(dst, src, sa);

    EXPECT_NEAR(out.r, expR, 1.0);
    EXPECT_NEAR(out.g, expG, 1.0);
    EXPECT_NEAR(out.b, expB, 1.0);
    EXPECT_NEAR(out.a, std::round(outA * 255.f), 1.0);
}

TEST(BlendSourceOverTest, NoColorFringeOverTransparentDestination) {
    Rgba dst{0, 0, 0, 0};
    Rgba src{123, 45, 6, 0};

    for (float sa : {0.01f, 0.05f, 0.1f, 0.3f}) {
        Rgba out = blendSourceOver(dst, src, sa);
        // Even at very low alpha, the visible color (once un-premultiplied)
        // must remain the source color, not shifted toward the transparent
        // destination's (black) rgb. This is the classic straight-alpha bug.
        EXPECT_NEAR(out.r, src.r, 1) << "sa=" << sa;
        EXPECT_NEAR(out.g, src.g, 1) << "sa=" << sa;
        EXPECT_NEAR(out.b, src.b, 1) << "sa=" << sa;
    }
}

TEST(BlendSourceOverTest, ZeroSourceAlphaLeavesDestinationUnchanged) {
    Rgba dsts[] = {
        {0, 0, 0, 0},
        {10, 20, 30, 40},
        {255, 255, 255, 255},
        {5, 250, 128, 1},
    };
    Rgba src{77, 88, 99, 255};

    for (const Rgba& dst : dsts) {
        Rgba out = blendSourceOver(dst, src, 0.f);
        EXPECT_EQ(out.r, dst.r);
        EXPECT_EQ(out.g, dst.g);
        EXPECT_EQ(out.b, dst.b);
        EXPECT_EQ(out.a, dst.a);
    }
}
