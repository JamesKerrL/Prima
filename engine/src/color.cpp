#include "prima/color.h"

#include <algorithm>
#include <cmath>

namespace prima {

namespace {

constexpr double kPi = 3.14159265358979323846;
constexpr double kSqrt3Over2 = 0.8660254037844386;

// Triangle vertices: hue (top), black (bottom-left), white (bottom-right).
constexpr double kAx = 0.0, kAy = 1.0;
constexpr double kBx = -kSqrt3Over2, kBy = -0.5;
constexpr double kCx = kSqrt3Over2, kCy = -0.5;

double clamp01(double v) { return std::clamp(v, 0.0, 1.0); }

uint8_t toByte(double channel01) {
    return static_cast<uint8_t>(std::lround(clamp01(channel01) * 255.0));
}

}  // namespace

Hsv HsvFromRgba(Rgba color) {
    const double r = color.r / 255.0;
    const double g = color.g / 255.0;
    const double b = color.b / 255.0;

    const double maxC = std::max({r, g, b});
    const double minC = std::min({r, g, b});
    const double delta = maxC - minC;

    double h = 0.0;
    if (delta > 0.0) {
        if (maxC == r) {
            h = 60.0 * std::fmod((g - b) / delta, 6.0);
        } else if (maxC == g) {
            h = 60.0 * (((b - r) / delta) + 2.0);
        } else {
            h = 60.0 * (((r - g) / delta) + 4.0);
        }
        if (h < 0.0) h += 360.0;
    }

    const double s = maxC == 0.0 ? 0.0 : delta / maxC;
    const double v = maxC;
    return Hsv{h, s, v};
}

Rgba RgbaFromHsv(Hsv hsv, uint8_t alpha) {
    const double h = std::fmod(std::fmod(hsv.h, 360.0) + 360.0, 360.0);
    const double s = clamp01(hsv.s);
    const double v = clamp01(hsv.v);

    const double c = v * s;
    const double x = c * (1.0 - std::fabs(std::fmod(h / 60.0, 2.0) - 1.0));
    const double m = v - c;

    double r1, g1, b1;
    if (h < 60.0) {
        r1 = c; g1 = x; b1 = 0.0;
    } else if (h < 120.0) {
        r1 = x; g1 = c; b1 = 0.0;
    } else if (h < 180.0) {
        r1 = 0.0; g1 = c; b1 = x;
    } else if (h < 240.0) {
        r1 = 0.0; g1 = x; b1 = c;
    } else if (h < 300.0) {
        r1 = x; g1 = 0.0; b1 = c;
    } else {
        r1 = c; g1 = 0.0; b1 = x;
    }

    return Rgba{toByte(r1 + m), toByte(g1 + m), toByte(b1 + m), alpha};
}

double HueFromAngle(double radians) {
    double degrees = radians * (180.0 / kPi);
    degrees = std::fmod(degrees, 360.0);
    if (degrees < 0.0) degrees += 360.0;
    return degrees;
}

double AngleFromHue(double hueDegrees) {
    double degrees = std::fmod(hueDegrees, 360.0);
    if (degrees < 0.0) degrees += 360.0;
    double radians = degrees * (kPi / 180.0);
    if (radians > kPi) radians -= 2.0 * kPi;
    return radians;
}

TrianglePoint TrianglePointFromSv(double s, double v) {
    s = clamp01(s);
    v = clamp01(v);

    const double a = s * v;
    const double b = 1.0 - v;
    const double c = (1.0 - s) * v;

    return TrianglePoint{a * kAx + b * kBx + c * kCx,
                          a * kAy + b * kBy + c * kCy};
}

Sv SvFromTrianglePoint(double x, double y) {
    // Barycentric coordinates of (x,y) w.r.t. (A, B, C).
    const double denom =
        (kBy - kCy) * (kAx - kCx) + (kCx - kBx) * (kAy - kCy);
    double a = ((kBy - kCy) * (x - kCx) + (kCx - kBx) * (y - kCy)) / denom;
    double b = ((kCy - kAy) * (x - kCx) + (kAx - kCx) * (y - kCy)) / denom;
    double c = 1.0 - a - b;

    // Clamp to the triangle interior (points outside get their negative
    // barycentric weight zeroed, then the remaining weights renormalized),
    // so the result is always a valid (s, v) inside the triangle.
    a = std::max(0.0, a);
    b = std::max(0.0, b);
    c = std::max(0.0, c);
    const double sum = a + b + c;
    if (sum > 0.0) {
        a /= sum;
        b /= sum;
        c /= sum;
    } else {
        a = 0.0; b = 1.0; c = 0.0;
    }

    const double v = a + c;
    const double s = v > 0.0 ? a / v : 0.0;
    return Sv{s, v};
}

}  // namespace prima
