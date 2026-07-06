#include "prima/dab_emitter.h"

#include <algorithm>
#include <cmath>
#include <vector>

#include <gtest/gtest.h>

using prima::BrushParams;
using prima::DabEmitter;
using prima::DabPoint;
using prima::InputSample;

namespace {

constexpr float kEps = 1e-4f;

InputSample makeSample(float x, float y, float pressure = 1.f) {
    InputSample s;
    s.x = x;
    s.y = y;
    s.pressure = pressure;
    return s;
}

// step = max(kMinSpacingStep, spacing * 2 * radius) for default BrushParams
// (radius=12, spacing=0.15) => max(0.15, 0.15*2*12) = 3.6
constexpr float kDefaultStep = 3.6f;

}  // namespace

TEST(DabEmitterTest, BatchingIsDeterministic) {
    std::vector<InputSample> samples;
    for (int i = 0; i < 8; ++i) {
        samples.push_back(makeSample(i * (20.f / 7.f), 0.f, 1.f));
    }

    BrushParams params;

    std::vector<DabPoint> batched;
    {
        DabEmitter e;
        e.begin(params);
        e.addSamples(samples.data(), static_cast<int>(samples.size()),
                     [&](const DabPoint& d) { batched.push_back(d); });
        e.end([&](const DabPoint& d) { batched.push_back(d); });
    }

    std::vector<DabPoint> perSample;
    {
        DabEmitter e;
        e.begin(params);
        for (const auto& s : samples) {
            e.addSamples(&s, 1,
                         [&](const DabPoint& d) { perSample.push_back(d); });
        }
        e.end([&](const DabPoint& d) { perSample.push_back(d); });
    }

    ASSERT_EQ(batched.size(), perSample.size());
    for (std::size_t i = 0; i < batched.size(); ++i) {
        EXPECT_NEAR(batched[i].x, perSample[i].x, kEps) << "at index " << i;
        EXPECT_NEAR(batched[i].y, perSample[i].y, kEps) << "at index " << i;
        EXPECT_NEAR(batched[i].radius, perSample[i].radius, kEps) << "at index " << i;
        EXPECT_NEAR(batched[i].flow, perSample[i].flow, kEps) << "at index " << i;
    }
}

TEST(DabEmitterTest, SpacingProducesExpectedPositions) {
    BrushParams params;
    DabEmitter e;
    e.begin(params);

    std::vector<DabPoint> dabs;
    InputSample samples[2] = {makeSample(0.f, 0.f), makeSample(18.f, 0.f)};
    e.addSamples(samples, 2, [&](const DabPoint& d) { dabs.push_back(d); });
    e.end([&](const DabPoint& d) { dabs.push_back(d); });

    // Pen-down at 0, then every kDefaultStep: 3.6, 7.2, 10.8, 14.4, 18.0(<=18)
    std::vector<float> expected = {0.f, 3.6f, 7.2f, 10.8f, 14.4f, 18.0f};
    ASSERT_EQ(dabs.size(), expected.size());
    for (std::size_t i = 0; i < expected.size(); ++i) {
        EXPECT_NEAR(dabs[i].x, expected[i], 1e-3f) << "at index " << i;
        EXPECT_NEAR(dabs[i].y, 0.f, kEps) << "at index " << i;
    }
}

TEST(DabEmitterTest, LeftoverCarriesAcrossSegmentsAndCalls) {
    BrushParams params;

    // Irregular path: 0 -> 5 -> 5 (dup) -> 20, fed as separate addSamples calls
    // to also exercise leftover-carry across call boundaries.
    DabEmitter e;
    e.begin(params);
    std::vector<DabPoint> dabs;

    InputSample s0 = makeSample(0.f, 0.f);
    InputSample s1 = makeSample(5.f, 0.f);
    InputSample s2 = makeSample(5.f, 0.f);
    InputSample s3 = makeSample(20.f, 0.f);

    e.addSamples(&s0, 1, [&](const DabPoint& d) { dabs.push_back(d); });
    e.addSamples(&s1, 1, [&](const DabPoint& d) { dabs.push_back(d); });
    e.addSamples(&s2, 1, [&](const DabPoint& d) { dabs.push_back(d); });
    e.addSamples(&s3, 1, [&](const DabPoint& d) { dabs.push_back(d); });
    e.end([&](const DabPoint& d) { dabs.push_back(d); });

    // Equivalent straight path 0 -> 20 in one shot must match exactly.
    DabEmitter eRef;
    eRef.begin(params);
    std::vector<DabPoint> ref;
    InputSample sref[2] = {makeSample(0.f, 0.f), makeSample(20.f, 0.f)};
    eRef.addSamples(sref, 2, [&](const DabPoint& d) { ref.push_back(d); });
    eRef.end([&](const DabPoint& d) { ref.push_back(d); });

    ASSERT_EQ(dabs.size(), ref.size());
    for (std::size_t i = 0; i < dabs.size(); ++i) {
        EXPECT_NEAR(dabs[i].x, ref[i].x, kEps) << "at index " << i;
        EXPECT_NEAR(dabs[i].y, ref[i].y, kEps) << "at index " << i;
    }

    // Sanity: expected positions at multiples of step, pen-down at 0.
    // 0, 3.6, 7.2, 10.8, 14.4, 18.0
    std::vector<float> expected = {0.f, 3.6f, 7.2f, 10.8f, 14.4f, 18.0f};
    ASSERT_EQ(dabs.size(), expected.size());
    for (std::size_t i = 0; i < expected.size(); ++i) {
        EXPECT_NEAR(dabs[i].x, expected[i], 1e-3f) << "at index " << i;
    }
}

TEST(DabEmitterTest, PressureRampProducesNonDecreasingRadius) {
    BrushParams params;
    params.sizeResponse = {0.1f, 1.0f};  // minFactor=0.1, gamma=1

    const int kCount = 20;
    std::vector<InputSample> samples;
    for (int i = 0; i < kCount; ++i) {
        float t = static_cast<float>(i) / (kCount - 1);
        samples.push_back(makeSample(t * 60.f, 0.f, t));
    }

    DabEmitter e;
    e.begin(params);
    std::vector<DabPoint> dabs;
    e.addSamples(samples.data(), static_cast<int>(samples.size()),
                 [&](const DabPoint& d) { dabs.push_back(d); });
    e.end([&](const DabPoint& d) { dabs.push_back(d); });

    ASSERT_GT(dabs.size(), 1u);
    for (std::size_t i = 1; i < dabs.size(); ++i) {
        EXPECT_GE(dabs[i].radius, dabs[i - 1].radius - kEps) << "at index " << i;
    }
}

TEST(DabEmitterTest, TapEmitsExactlyOneDab) {
    BrushParams params;
    DabEmitter e;
    e.begin(params);

    std::vector<DabPoint> dabs;
    InputSample s = makeSample(7.f, 3.f);
    e.addSamples(&s, 1, [&](const DabPoint& d) { dabs.push_back(d); });
    e.end([&](const DabPoint& d) { dabs.push_back(d); });

    ASSERT_EQ(dabs.size(), 1u);
    EXPECT_NEAR(dabs[0].x, 7.f, kEps);
    EXPECT_NEAR(dabs[0].y, 3.f, kEps);
}

TEST(DabEmitterTest, ZeroLengthSegmentDoesNotDuplicateOrMissDabs) {
    BrushParams params;

    DabEmitter eDup;
    eDup.begin(params);
    std::vector<DabPoint> dupDabs;
    InputSample dupSamples[4] = {
        makeSample(0.f, 0.f), makeSample(5.f, 0.f), makeSample(5.f, 0.f),
        makeSample(10.f, 0.f)};
    eDup.addSamples(dupSamples, 4, [&](const DabPoint& d) { dupDabs.push_back(d); });
    eDup.end([&](const DabPoint& d) { dupDabs.push_back(d); });

    DabEmitter eNoDup;
    eNoDup.begin(params);
    std::vector<DabPoint> noDupDabs;
    InputSample noDupSamples[3] = {
        makeSample(0.f, 0.f), makeSample(5.f, 0.f), makeSample(10.f, 0.f)};
    eNoDup.addSamples(noDupSamples, 3,
                      [&](const DabPoint& d) { noDupDabs.push_back(d); });
    eNoDup.end([&](const DabPoint& d) { noDupDabs.push_back(d); });

    ASSERT_EQ(dupDabs.size(), noDupDabs.size());
    for (std::size_t i = 0; i < dupDabs.size(); ++i) {
        EXPECT_NEAR(dupDabs[i].x, noDupDabs[i].x, kEps) << "at index " << i;
        EXPECT_NEAR(dupDabs[i].y, noDupDabs[i].y, kEps) << "at index " << i;
        EXPECT_NEAR(dupDabs[i].radius, noDupDabs[i].radius, kEps) << "at index " << i;
        EXPECT_NEAR(dupDabs[i].flow, noDupDabs[i].flow, kEps) << "at index " << i;
    }
}

TEST(DabEmitterTest, BeginEndWithNoSamplesEmitsNothing) {
    BrushParams params;
    DabEmitter e;
    e.begin(params);

    std::vector<DabPoint> dabs;
    e.end([&](const DabPoint& d) { dabs.push_back(d); });

    EXPECT_TRUE(dabs.empty());
}

// 7. Catmull-Rom curve smoothing at sharp direction changes.
//
// Fast mouse strokes deliver few, widely-spaced raw samples. Without curve
// fitting, a direction change between samples chords into two straight
// segments meeting at a sharp corner: the dab-to-dab turn is concentrated
// almost entirely in one step, right at the corner sample. Catmull-Rom
// fitting (using each sample's neighbors as tangent guides) should spread
// that turn out gradually across several dabs instead.
TEST(DabEmitterTest, SharpDirectionChangeCurvesGraduallyInsteadOfChording) {
    BrushParams params;
    params.spacing = 0.1f;  // tight spacing for a fine-grained direction trace

    DabEmitter e;
    e.begin(params);
    std::vector<DabPoint> dabs;
    auto sink = [&](const DabPoint& d) { dabs.push_back(d); };

    // A 90-degree V: (0,10) -> (10,0) -> (20,10), apex at the middle sample.
    InputSample s1 = makeSample(0.f, 10.f);
    InputSample s2 = makeSample(10.f, 0.f);
    InputSample s3 = makeSample(20.f, 10.f);
    e.addSamples(&s1, 1, sink);
    e.addSamples(&s2, 1, sink);
    e.addSamples(&s3, 1, sink);
    e.end(sink);

    ASSERT_GT(dabs.size(), 4u);

    float maxTurnDeg = 0.f;
    for (std::size_t i = 1; i + 1 < dabs.size(); ++i) {
        float ax = dabs[i].x - dabs[i - 1].x, ay = dabs[i].y - dabs[i - 1].y;
        float bx = dabs[i + 1].x - dabs[i].x, by = dabs[i + 1].y - dabs[i].y;
        float aLen = std::sqrt(ax * ax + ay * ay);
        float bLen = std::sqrt(bx * bx + by * by);
        if (aLen < 1e-4f || bLen < 1e-4f) continue;
        float cosAngle = std::clamp((ax * bx + ay * by) / (aLen * bLen), -1.f, 1.f);
        maxTurnDeg = std::max(maxTurnDeg, std::acos(cosAngle) * 180.f / 3.14159265f);
    }

    // The raw path's own turn at the vertex is 90 degrees. Unsmoothed
    // chording would show that whole turn in a single dab-to-dab step;
    // smoothing spreads it out, so no single step should come close to it.
    EXPECT_LT(maxTurnDeg, 60.f);
}
