#include "prima/color_wheel.h"

#include <algorithm>
#include <cmath>

#include "prima/color.h"

namespace prima {

namespace {

int clampNonNegative(int v) { return v < 0 ? 0 : v; }

// Writes a color into an RGBA8 row-major buffer at pixel (x, y).
void putPixel(std::vector<uint8_t>& buf, int width, int x, int y, Rgba color) {
    const std::size_t i =
        (static_cast<std::size_t>(y) * width + x) * ColorWheel::kBytesPerPixel;
    buf[i + 0] = color.r;
    buf[i + 1] = color.g;
    buf[i + 2] = color.b;
    buf[i + 3] = color.a;
}

// Signed distance from point (px, py) to the (finite) edge between a and b,
// positive on the interior side (the side containing a reference point). We use
// it only to derive an anti-aliasing coverage; sign handling is done by the
// caller which knows the interior side.
double distanceToSegment(double px, double py, double ax, double ay,
                         double bx, double by) {
    const double dx = bx - ax;
    const double dy = by - ay;
    const double len2 = dx * dx + dy * dy;
    double t = len2 > 0.0 ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0.0;
    t = std::clamp(t, 0.0, 1.0);
    const double cx = ax + t * dx;
    const double cy = ay + t * dy;
    const double ex = px - cx;
    const double ey = py - cy;
    return std::sqrt(ex * ex + ey * ey);
}

}  // namespace

ColorWheel::ColorWheel(int outerSizePx, int ringThicknessPx)
    : ringSize_(clampNonNegative(outerSizePx)),
      thickness_(clampNonNegative(ringThicknessPx)),
      outerRadius_(clampNonNegative(outerSizePx) / 2.0),
      innerRadius_(0.0),
      triangleSize_(0),
      hue_(0.0) {
    // Cap thickness so the inner radius never goes negative.
    if (thickness_ > outerRadius_) thickness_ = static_cast<int>(outerRadius_);
    innerRadius_ = outerRadius_ - thickness_;

    // Triangle bounding box: the square that bounds the inner circle.
    triangleSize_ = static_cast<int>(std::lround(2.0 * innerRadius_));
    if (triangleSize_ < 0) triangleSize_ = 0;

    ring_.assign(static_cast<std::size_t>(ringSize_) * ringSize_ *
                     kBytesPerPixel,
                 0);
    triangle_.assign(static_cast<std::size_t>(triangleSize_) * triangleSize_ *
                         kBytesPerPixel,
                     0);

    GenerateRing();
    GenerateTriangle();
}

void ColorWheel::SetHue(double hueDegrees) {
    hue_ = hueDegrees;
    GenerateTriangle();
}

void ColorWheel::GenerateRing() {
    if (ringSize_ <= 0) return;

    // Continuous center of the buffer. Pixel centers are sampled at +0.5.
    const double cx = ringSize_ / 2.0;
    const double cy = ringSize_ / 2.0;
    const double outer = outerRadius_;
    const double inner = innerRadius_;
    // ~1px soft edge (coverage ramps over one pixel around each boundary).
    const double aa = 1.0;

    for (int y = 0; y < ringSize_; ++y) {
        const double sy = y + 0.5 - cy;
        for (int x = 0; x < ringSize_; ++x) {
            const double sx = x + 0.5 - cx;
            const double dist = std::sqrt(sx * sx + sy * sy);

            // Coverage: fully inside the annulus -> 1, outside -> 0, with a
            // linear ramp of width `aa` straddling each boundary. Using
            // (boundary - dist) / aa + 0.5 centers the ramp on the boundary.
            const double outerCov =
                std::clamp((outer - dist) / aa + 0.5, 0.0, 1.0);
            const double innerCov =
                std::clamp((dist - inner) / aa + 0.5, 0.0, 1.0);
            const double coverage = outerCov * innerCov;
            if (coverage <= 0.0) continue;  // buffer already transparent

            // Math orientation: screen Y grows downward, so negate to make the
            // angle increase counter-clockwise, matching AngleFromHue/HueFromAngle.
            const double hue = HueFromAngle(std::atan2(-sy, sx));
            const Rgba full = RgbaFromHsv(Hsv{hue, 1.0, 1.0});
            const uint8_t alpha =
                static_cast<uint8_t>(std::lround(coverage * 255.0));
            putPixel(ring_, ringSize_, x, y, Rgba{full.r, full.g, full.b, alpha});
        }
    }
}

void ColorWheel::GenerateTriangle() {
    if (triangleSize_ <= 0) return;

    // Clear to transparent first (SetHue reuses the buffer).
    std::fill(triangle_.begin(), triangle_.end(), static_cast<uint8_t>(0));

    const double scale = innerRadius_;  // unit circle -> pixels
    if (scale <= 0.0) return;

    const double halfW = triangleSize_ / 2.0;
    const double halfH = triangleSize_ / 2.0;

    // Triangle vertices in normalized Cartesian (must match color.cpp):
    //   hue (top) = (0, 1), black (bl) = (-sqrt(3)/2, -0.5),
    //   white (br) = (sqrt(3)/2, -0.5).
    const double s3o2 = 0.8660254037844386;
    const double vx[3] = {0.0, -s3o2, s3o2};
    const double vy[3] = {1.0, -0.5, -0.5};

    // AA width expressed in normalized units (one pixel wide).
    const double aa = 1.0 / scale;

    for (int y = 0; y < triangleSize_; ++y) {
        // Screen Y flipped so +ny points up toward the hue vertex.
        const double ny = (halfH - (y + 0.5)) / scale;
        for (int x = 0; x < triangleSize_; ++x) {
            const double nx = (x + 0.5 - halfW) / scale;

            // Coverage from the min distance to the three edges. If the point
            // is inside the triangle, coverage ramps from 1 down to 0.5 exactly
            // at each edge; outside, it drops below 0.5 toward 0.
            double minDist = 1e30;
            minDist = std::min(minDist,
                               distanceToSegment(nx, ny, vx[0], vy[0], vx[1], vy[1]));
            minDist = std::min(minDist,
                               distanceToSegment(nx, ny, vx[1], vy[1], vx[2], vy[2]));
            minDist = std::min(minDist,
                               distanceToSegment(nx, ny, vx[2], vy[2], vx[0], vy[0]));

            // Inside test via sign of the point-in-triangle predicate.
            const bool inside = [&]() {
                auto cross = [](double ax, double ay, double bx, double by,
                                double px, double py) {
                    return (bx - ax) * (py - ay) - (by - ay) * (px - ax);
                };
                const double d0 = cross(vx[0], vy[0], vx[1], vy[1], nx, ny);
                const double d1 = cross(vx[1], vy[1], vx[2], vy[2], nx, ny);
                const double d2 = cross(vx[2], vy[2], vx[0], vy[0], nx, ny);
                const bool hasNeg = (d0 < 0) || (d1 < 0) || (d2 < 0);
                const bool hasPos = (d0 > 0) || (d1 > 0) || (d2 > 0);
                return !(hasNeg && hasPos);
            }();

            // Signed distance: positive inside, negative outside. Coverage
            // ramps over `aa` centered on the edge (0.5 at the edge).
            const double signedDist = inside ? minDist : -minDist;
            const double coverage =
                std::clamp(signedDist / aa + 0.5, 0.0, 1.0);
            if (coverage <= 0.0) continue;

            const Sv sv = SvFromTrianglePoint(nx, ny);
            const Rgba color = RgbaFromHsv(Hsv{hue_, sv.s, sv.v});
            const uint8_t alpha =
                static_cast<uint8_t>(std::lround(coverage * 255.0));
            putPixel(triangle_, triangleSize_, x, y,
                     Rgba{color.r, color.g, color.b, alpha});
        }
    }
}

}  // namespace prima
