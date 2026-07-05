#ifndef PRIMA_CANVAS_H
#define PRIMA_CANVAS_H

#include <cstddef>
#include <cstdint>
#include <vector>

namespace prima {

// A packed RGBA8 color, one byte per channel.
struct Rgba {
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;
};

// A canvas owns a contiguous RGBA8 pixel buffer, stored row-major with no row
// padding (stride == width * kBytesPerPixel). This is the core document surface
// the whole app draws into; it has no knowledge of the UI or of C#.
class Canvas {
public:
    static constexpr int kBytesPerPixel = 4;

    Canvas(int width, int height);

    int width() const { return width_; }
    int height() const { return height_; }
    int stride() const { return width_ * kBytesPerPixel; }

    uint8_t* pixels() { return pixels_.data(); }
    const uint8_t* pixels() const { return pixels_.data(); }
    std::size_t byteSize() const { return pixels_.size(); }

    // Fill the entire canvas with a single color.
    void clear(Rgba color);

    // Stamp one filled, bounds-clamped circular brush dab centered at (cx, cy).
    void brushDab(int cx, int cy, int radius, Rgba color);

private:
    int width_;
    int height_;
    std::vector<uint8_t> pixels_;
};

}  // namespace prima

#endif  // PRIMA_CANVAS_H
