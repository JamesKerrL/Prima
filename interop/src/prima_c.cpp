#include "prima_c/prima_c.h"

#include <new>

#include "prima/brush.h"
#include "prima/canvas.h"
#include "prima/renderer.h"
#include "prima/viewport.h"

using prima::BrushEngine;
using prima::BrushParams;
using prima::Canvas;
using prima::InputSample;
using prima::RectI;
using prima::RenderTarget;
using prima::Renderer;
using prima::Rgba;
using prima::SoftwareRenderer;
using prima::Viewport;

// PrimaInputSample and prima::InputSample share an identical field layout
// (float x, y, pressure, tilt*, rotation; double time*), so the sample batch is
// reinterpret_cast across the boundary — no per-event copy or heap allocation.
static_assert(sizeof(PrimaInputSample) == sizeof(InputSample),
              "PrimaInputSample must be layout-compatible with prima::InputSample");

namespace {
Canvas* as_canvas(PrimaCanvas* c) { return reinterpret_cast<Canvas*>(c); }
const Canvas* as_canvas(const PrimaCanvas* c) {
    return reinterpret_cast<const Canvas*>(c);
}
Renderer* as_renderer(PrimaRenderer* r) { return reinterpret_cast<Renderer*>(r); }
BrushEngine* as_brush_engine(PrimaBrushEngine* e) {
    return reinterpret_cast<BrushEngine*>(e);
}

// Copy a native RectI into the caller's optional out-rect.
void write_rect(PrimaRect* out, const RectI& r) {
    if (!out) return;
    out->x = r.x;
    out->y = r.y;
    out->width = r.width;
    out->height = r.height;
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

PrimaBrushEngine* prima_brush_engine_create(void) {
    return reinterpret_cast<PrimaBrushEngine*>(new (std::nothrow) BrushEngine());
}

void prima_brush_engine_destroy(PrimaBrushEngine* engine) {
    delete as_brush_engine(engine);
}

void prima_stroke_begin(PrimaBrushEngine* engine, PrimaCanvas* canvas,
                        const PrimaBrushParams* params) {
    if (!engine || !canvas || !params) return;
    BrushParams bp;
    bp.radius = params->radius;
    bp.hardness = params->hardness;
    bp.opacity = params->opacity;
    bp.flow = params->flow;
    bp.spacing = params->spacing;
    bp.sizeResponse.minFactor = params->size_pressure_min;
    bp.sizeResponse.gamma = params->size_pressure_gamma;
    bp.flowResponse.minFactor = params->flow_pressure_min;
    bp.flowResponse.gamma = params->flow_pressure_gamma;
    bp.color = Rgba{params->r, params->g, params->b, params->a};
    as_brush_engine(engine)->beginStroke(*as_canvas(canvas), bp);
}

void prima_stroke_add(PrimaBrushEngine* engine, const PrimaInputSample* samples,
                      int count, PrimaRect* out_dirty) {
    if (!engine) return;
    if (!samples || count <= 0) {
        write_rect(out_dirty, RectI{});
        return;
    }
    RectI dirty = as_brush_engine(engine)->addSamples(
        reinterpret_cast<const InputSample*>(samples), count);
    write_rect(out_dirty, dirty);
}

void prima_stroke_end(PrimaBrushEngine* engine, PrimaRect* out_dirty) {
    if (!engine) return;
    write_rect(out_dirty, as_brush_engine(engine)->endStroke());
}

}  // extern "C"
