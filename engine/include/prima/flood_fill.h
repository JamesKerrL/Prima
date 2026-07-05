#ifndef PRIMA_FLOOD_FILL_H
#define PRIMA_FLOOD_FILL_H

#include <cstdint>

#include "prima/canvas.h"  // for Rgba
#include "prima/rect.h"

namespace prima {

// Contiguous, 4-connected flood fill over a packed RGBA8 buffer (row-major,
// `stride` bytes per row). Starting at the seed pixel, every pixel reachable
// through 4-connectivity whose per-channel difference from the seed pixel's
// ORIGINAL color is <= `tolerance` (0 = exact match) is overwritten with
// `newColor`. Returns the bounding rect of the pixels that changed.
//
// This is a pure algorithm unit: it owns no state and knows nothing about
// Canvas, so it can be unit-tested against a plain buffer. Returns an empty
// RectI (and touches nothing) when the seed is out of bounds, the buffer is
// empty, or `newColor` already equals the seed color.
RectI floodFill(uint8_t* pixels, int width, int height, int stride,
                int seedX, int seedY, Rgba newColor, int tolerance);

}  // namespace prima

#endif  // PRIMA_FLOOD_FILL_H
