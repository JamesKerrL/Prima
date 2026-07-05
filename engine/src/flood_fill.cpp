#include "prima/flood_fill.h"

#include <cstddef>
#include <cstdlib>
#include <utility>
#include <vector>

namespace prima {

RectI floodFill(uint8_t* pixels, int width, int height, int stride, int seedX,
                int seedY, Rgba newColor, int tolerance) {
    RectI dirty;
    if (pixels == nullptr || width <= 0 || height <= 0) return dirty;
    if (seedX < 0 || seedY < 0 || seedX >= width || seedY >= height) return dirty;
    if (tolerance < 0) tolerance = 0;

    auto at = [&](int x, int y) -> uint8_t* {
        return pixels + static_cast<std::size_t>(y) * stride +
               static_cast<std::size_t>(x) * Canvas::kBytesPerPixel;
    };

    const uint8_t* s = at(seedX, seedY);
    const Rgba seed{s[0], s[1], s[2], s[3]};

    // Filling with the seed's own color changes nothing.
    if (seed.r == newColor.r && seed.g == newColor.g && seed.b == newColor.b &&
        seed.a == newColor.a) {
        return dirty;
    }

    auto matchesSeed = [&](int x, int y) -> bool {
        const uint8_t* p = at(x, y);
        return std::abs(int(p[0]) - int(seed.r)) <= tolerance &&
               std::abs(int(p[1]) - int(seed.g)) <= tolerance &&
               std::abs(int(p[2]) - int(seed.b)) <= tolerance &&
               std::abs(int(p[3]) - int(seed.a)) <= tolerance;
    };

    // `visited` guards against re-processing; it also makes the fill correct
    // even when `newColor` still matches the seed within tolerance (a color
    // test alone would loop forever in that case).
    std::vector<uint8_t> visited(static_cast<std::size_t>(width) * height, 0);
    auto canFill = [&](int x, int y) -> bool {
        return visited[static_cast<std::size_t>(y) * width + x] == 0 &&
               matchesSeed(x, y);
    };

    std::vector<std::pair<int, int>> stack;
    stack.reserve(64);
    stack.emplace_back(seedX, seedY);

    while (!stack.empty()) {
        const int px = stack.back().first;
        const int py = stack.back().second;
        stack.pop_back();
        if (!canFill(px, py)) continue;

        // Grow the span left and right along this row.
        int lx = px;
        while (lx - 1 >= 0 && canFill(lx - 1, py)) --lx;
        int rx = px;
        while (rx + 1 < width && canFill(rx + 1, py)) ++rx;

        // Fill the span and mark it visited.
        for (int x = lx; x <= rx; ++x) {
            uint8_t* p = at(x, py);
            p[0] = newColor.r;
            p[1] = newColor.g;
            p[2] = newColor.b;
            p[3] = newColor.a;
            visited[static_cast<std::size_t>(py) * width + x] = 1;
        }
        dirty.unionWith(RectI{lx, py, rx - lx + 1, 1});

        // Seed the rows above and below: one push per contiguous fillable run.
        const int rows[2] = {py - 1, py + 1};
        for (int ny : rows) {
            if (ny < 0 || ny >= height) continue;
            int x = lx;
            while (x <= rx) {
                if (canFill(x, ny)) {
                    stack.emplace_back(x, ny);
                    while (x <= rx && canFill(x, ny)) ++x;
                } else {
                    ++x;
                }
            }
        }
    }

    return dirty;
}

}  // namespace prima
