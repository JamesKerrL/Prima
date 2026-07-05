#ifndef PRIMA_COLOR_WHEEL_H
#define PRIMA_COLOR_WHEEL_H

#include <cstdint>
#include <vector>

namespace prima {

// Renders the two bitmaps that back a hue-ring + SV-triangle color picker:
//
//  - a hue ring: an annulus whose angle around the center selects the hue.
//  - an SV triangle: for the currently selected hue, an equilateral triangle
//    whose interior blends the hue, black and white corners (saturation and
//    value).
//
// Both bitmaps are owned RGBA8 buffers, row-major with no row padding
// (stride == width * 4), mirroring Canvas's ownership/layout. The class is
// engine-only: STL only, no UI or interop knowledge. Areas that aren't part of
// the ring/triangle are left fully transparent so the UI can composite the two
// on top of each other.
//
// Coordinate convention (the UI's drag/hit-testing code relies on this):
//   * Ring buffer is outerSizePx x outerSizePx. Its center is the buffer
//     center ((outerSizePx-1)/2 in both axes, i.e. size/2.0 as a continuous
//     coordinate). Ring angle uses atan2(dy, dx) in *math* orientation
//     (counter-clockwise, +x = angle 0), matching HueFromAngle. Note dy is
//     measured with screen-down as positive; see the .cpp for how that is
//     reconciled so hue 0 sits where AngleFromHue(0) points.
//   * Triangle buffer is (2*innerRadius) x (2*innerRadius) pixels, where
//     innerRadius = outerRadius - ringThicknessPx (the ring's inner radius).
//     Pixel (px, py) maps to normalized Cartesian used by
//     TrianglePointFromSv/SvFromTrianglePoint via:
//         nx = (px + 0.5 - halfW) / innerRadius
//         ny = (halfH - (py + 0.5)) / innerRadius   // screen Y flipped: +ny is up
//     so the hue vertex (Cartesian (0,1)) is at the top-center of the buffer.
class ColorWheel {
public:
    static constexpr int kBytesPerPixel = 4;

    // outerSizePx is the ring bitmap's edge length; ringThicknessPx is the
    // annulus width. Both are clamped non-negative and the thickness is capped
    // at the outer radius. The ring is generated once here; the triangle is
    // generated for hue 0 and can be re-rendered with SetHue.
    ColorWheel(int outerSizePx, int ringThicknessPx);

    // Regenerates the triangle bitmap for a new hue (degrees). The ring bitmap
    // is unaffected. Reuses the existing triangle buffer (no reallocation).
    void SetHue(double hueDegrees);

    double hue() const { return hue_; }

    const uint8_t* RingPixels() const { return ring_.data(); }
    int RingWidth() const { return ringSize_; }
    int RingHeight() const { return ringSize_; }

    const uint8_t* TrianglePixels() const { return triangle_.data(); }
    int TriangleWidth() const { return triangleSize_; }
    int TriangleHeight() const { return triangleSize_; }

private:
    void GenerateRing();
    void GenerateTriangle();

    int ringSize_;        // outer bitmap edge length (px)
    int thickness_;       // ring thickness (px)
    double outerRadius_;  // ringSize_ / 2
    double innerRadius_;  // outerRadius_ - thickness_ (>= 0)
    int triangleSize_;    // triangle bitmap edge length (px)
    double hue_;          // current triangle hue (degrees)

    std::vector<uint8_t> ring_;
    std::vector<uint8_t> triangle_;
};

}  // namespace prima

#endif  // PRIMA_COLOR_WHEEL_H
