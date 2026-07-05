#include "prima_c/prima_c.h"

#include <cstring>
#include <new>

#include "prima/canvas.h"
#include "prima/color.h"
#include "prima/color_wheel.h"
#include "prima/image_io.h"
#include "prima/renderer.h"
#include "prima/viewport.h"

using prima::Canvas;
using prima::ColorWheel;
using prima::Hsv;
using prima::HsvFromRgba;
using prima::RenderTarget;
using prima::Renderer;
using prima::Rgba;
using prima::RgbaFromHsv;
using prima::SoftwareRenderer;
using prima::Viewport;

namespace {
Canvas* as_canvas(PrimaCanvas* c) { return reinterpret_cast<Canvas*>(c); }
const Canvas* as_canvas(const PrimaCanvas* c) {
    return reinterpret_cast<const Canvas*>(c);
}
Renderer* as_renderer(PrimaRenderer* r) { return reinterpret_cast<Renderer*>(r); }
ColorWheel* as_colorwheel(PrimaColorWheel* w) {
    return reinterpret_cast<ColorWheel*>(w);
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

PrimaRenderer* prima_renderer_create_software(void) {
    return reinterpret_cast<PrimaRenderer*>(new (std::nothrow) SoftwareRenderer());
}

void prima_renderer_destroy(PrimaRenderer* renderer) {
    delete as_renderer(renderer);
}

void prima_render(PrimaRenderer* renderer, const PrimaCanvas* canvas,
                  uint8_t* target, int width, int height, int stride,
                  double pan_x, double pan_y, double zoom, uint8_t bg_r,
                  uint8_t bg_g, uint8_t bg_b, uint8_t bg_a) {
    if (!renderer || !canvas || !target) return;
    RenderTarget rt{target, width, height, stride};
    Viewport vp{pan_x, pan_y, zoom};
    as_renderer(renderer)->render(*as_canvas(canvas), rt, vp,
                                  Rgba{bg_r, bg_g, bg_b, bg_a});
}

void prima_color_rgba_to_hsv(uint8_t r, uint8_t g, uint8_t b, double* h,
                             double* s, double* v) {
    Hsv hsv = HsvFromRgba(Rgba{r, g, b, 255});
    if (h) *h = hsv.h;
    if (s) *s = hsv.s;
    if (v) *v = hsv.v;
}

void prima_color_hsv_to_rgba(double h, double s, double v, uint8_t* r,
                             uint8_t* g, uint8_t* b) {
    Rgba rgba = RgbaFromHsv(Hsv{h, s, v});
    if (r) *r = rgba.r;
    if (g) *g = rgba.g;
    if (b) *b = rgba.b;
}

PrimaColorWheel* prima_colorwheel_create(int outer_size, int ring_thickness) {
    return reinterpret_cast<PrimaColorWheel*>(
        new (std::nothrow) ColorWheel(outer_size, ring_thickness));
}

void prima_colorwheel_destroy(PrimaColorWheel* wheel) {
    delete as_colorwheel(wheel);
}

void prima_colorwheel_set_hue(PrimaColorWheel* wheel, double hue) {
    if (!wheel) return;
    as_colorwheel(wheel)->SetHue(hue);
}

const uint8_t* prima_colorwheel_ring_pixels(PrimaColorWheel* wheel, int* out_w,
                                            int* out_h) {
    if (!wheel) return nullptr;
    ColorWheel* w = as_colorwheel(wheel);
    if (out_w) *out_w = w->RingWidth();
    if (out_h) *out_h = w->RingHeight();
    return w->RingPixels();
}

const uint8_t* prima_colorwheel_triangle_pixels(PrimaColorWheel* wheel,
                                                int* out_w, int* out_h) {
    if (!wheel) return nullptr;
    ColorWheel* w = as_colorwheel(wheel);
    if (out_w) *out_w = w->TriangleWidth();
    if (out_h) *out_h = w->TriangleHeight();
    return w->TrianglePixels();
}

uint8_t* prima_image_load_file(const char* path, int* out_width,
                               int* out_height) {
    if (!path) return nullptr;
    auto result = prima::loadImage(path);
    if (!result) return nullptr;
    if (out_width) *out_width = result->width;
    if (out_height) *out_height = result->height;
    auto* buf = new (std::nothrow) uint8_t[result->pixels.size()];
    if (!buf) return nullptr;
    std::memcpy(buf, result->pixels.data(), result->pixels.size());
    return buf;
}

uint8_t* prima_image_load_memory(const uint8_t* data, size_t len,
                                 int* out_width, int* out_height) {
    if (!data || len == 0) return nullptr;
    auto result = prima::loadImageFromMemory(data, len);
    if (!result) return nullptr;
    if (out_width) *out_width = result->width;
    if (out_height) *out_height = result->height;
    auto* buf = new (std::nothrow) uint8_t[result->pixels.size()];
    if (!buf) return nullptr;
    std::memcpy(buf, result->pixels.data(), result->pixels.size());
    return buf;
}

void prima_image_free(uint8_t* pixels) {
    delete[] pixels;
}

int prima_image_save_png(const char* path, const uint8_t* pixels,
                         int width, int height) {
    if (!path || !pixels || width <= 0 || height <= 0) return -1;
    return prima::saveImagePng(path, pixels, width, height) ? 0 : -1;
}

int prima_image_save_jpeg(const char* path, const uint8_t* pixels,
                          int width, int height, int quality) {
    if (!path || !pixels || width <= 0 || height <= 0) return -1;
    return prima::saveImageJpeg(path, pixels, width, height, quality) ? 0 : -1;
}

}  // extern "C"
