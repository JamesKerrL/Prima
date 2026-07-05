#ifndef PRIMA_COLOR_H
#define PRIMA_COLOR_H

#include "prima/canvas.h"

namespace prima {

// HSV color: h in [0,360), s and v in [0,1].
struct Hsv {
    double h;
    double s;
    double v;
};

// Pure HSV <-> RGBA8 conversions. Alpha passes through unchanged.
Hsv HsvFromRgba(Rgba color);
Rgba RgbaFromHsv(Hsv hsv, uint8_t alpha = 255);

// Maps a ring angle (radians, atan2 convention: 0 along +x, increasing
// counter-clockwise) to a hue in [0,360), and back. 0 radians <-> hue 0.
double HueFromAngle(double radians);
double AngleFromHue(double hueDegrees);

// The SV triangle is an equilateral triangle inscribed in the unit circle,
// pointing up: the hue vertex (full color, s=1/v=1) at the top (0,1), the
// black vertex (v=0) at bottom-left, and the white vertex (s=0/v=1) at
// bottom-right. Interior colors are the barycentric blend of the three
// vertex colors.
struct TrianglePoint {
    double x;
    double y;
};

struct Sv {
    double s;
    double v;
};

// Maps normalized (s,v) to a point in the triangle's Cartesian coordinates.
TrianglePoint TrianglePointFromSv(double s, double v);

// Inverse of TrianglePointFromSv. Points outside the triangle are clamped to
// the nearest point on its boundary before conversion, so this never returns
// out-of-range (s,v).
Sv SvFromTrianglePoint(double x, double y);

}  // namespace prima

#endif  // PRIMA_COLOR_H
