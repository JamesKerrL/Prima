#include "prima/canvas.h"

#include <algorithm>

#include "prima/flood_fill.h"

namespace prima {

namespace {
int clampNonNegative(int v) { return v < 0 ? 0 : v; }
}  // namespace

Canvas::Canvas(int width, int height)
    : width_(clampNonNegative(width)),
      height_(clampNonNegative(height)),
      pixels_(static_cast<std::size_t>(clampNonNegative(width)) *
                  static_cast<std::size_t>(clampNonNegative(height)) *
                  kBytesPerPixel,
              0) {}

void Canvas::clear(Rgba color) {
    for (std::size_t i = 0; i < pixels_.size(); i += kBytesPerPixel) {
        pixels_[i + 0] = color.r;
        pixels_[i + 1] = color.g;
        pixels_[i + 2] = color.b;
        pixels_[i + 3] = color.a;
    }
}

void Canvas::brushDab(int cx, int cy, int radius, Rgba color) {
    if (radius < 0) return;
    const int r2 = radius * radius;
    const int minY = std::max(0, cy - radius);
    const int maxY = std::min(height_ - 1, cy + radius);
    const int minX = std::max(0, cx - radius);
    const int maxX = std::min(width_ - 1, cx + radius);
    for (int y = minY; y <= maxY; ++y) {
        const int dy = y - cy;
        for (int x = minX; x <= maxX; ++x) {
            const int dx = x - cx;
            if (dx * dx + dy * dy > r2) continue;
            const std::size_t i =
                (static_cast<std::size_t>(y) * width_ + x) * kBytesPerPixel;
            pixels_[i + 0] = color.r;
            pixels_[i + 1] = color.g;
            pixels_[i + 2] = color.b;
            pixels_[i + 3] = color.a;
        }
    }
}

RectI Canvas::floodFill(int seedX, int seedY, Rgba newColor, int tolerance) {
    return prima::floodFill(pixels_.data(), width_, height_, stride(), seedX,
                            seedY, newColor, tolerance);
}

}  // namespace prima
