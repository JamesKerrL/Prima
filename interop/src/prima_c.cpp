#include "prima_c/prima_c.h"

#include <new>

#include "prima/canvas.h"

using prima::Canvas;
using prima::Rgba;

namespace {
Canvas* as_canvas(PrimaCanvas* c) { return reinterpret_cast<Canvas*>(c); }
const Canvas* as_canvas(const PrimaCanvas* c) {
    return reinterpret_cast<const Canvas*>(c);
}
}  // namespace

extern "C" {

PrimaCanvas* prima_canvas_create(int width, int height) {
    return reinterpret_cast<PrimaCanvas*>(new (std::nothrow) Canvas(width, height));
}

void prima_canvas_destroy(PrimaCanvas* canvas) { delete as_canvas(canvas); }

int prima_canvas_width(const PrimaCanvas* canvas) {
    return canvas ? as_canvas(canvas)->width() : 0;
}

int prima_canvas_height(const PrimaCanvas* canvas) {
    return canvas ? as_canvas(canvas)->height() : 0;
}

int prima_canvas_stride(const PrimaCanvas* canvas) {
    return canvas ? as_canvas(canvas)->stride() : 0;
}

void prima_canvas_clear(PrimaCanvas* canvas, uint8_t r, uint8_t g, uint8_t b,
                        uint8_t a) {
    if (!canvas) return;
    as_canvas(canvas)->clear(Rgba{r, g, b, a});
}

void prima_canvas_brush_dab(PrimaCanvas* canvas, int cx, int cy, int radius,
                            uint8_t r, uint8_t g, uint8_t b, uint8_t a) {
    if (!canvas) return;
    as_canvas(canvas)->brushDab(cx, cy, radius, Rgba{r, g, b, a});
}

uint8_t* prima_canvas_pixels(PrimaCanvas* canvas, size_t* out_len,
                             int* out_stride) {
    if (!canvas) return nullptr;
    Canvas* c = as_canvas(canvas);
    if (out_len) *out_len = c->byteSize();
    if (out_stride) *out_stride = c->stride();
    return c->pixels();
}

}  // extern "C"
