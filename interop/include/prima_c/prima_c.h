#ifndef PRIMA_C_H
#define PRIMA_C_H

/* C ABI for the Prima engine. This is the single, narrow boundary between the
 * native C++ core and the managed C# application layer. Keep it coarse-grained:
 * no per-pixel calls across this line — pixel data is shared via
 * prima_canvas_pixels(), not marshaled. */

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#  if defined(PRIMA_C_BUILD)
#    define PRIMA_C_API __declspec(dllexport)
#  else
#    define PRIMA_C_API __declspec(dllimport)
#  endif
#else
#  define PRIMA_C_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque handle to a native canvas. */
typedef struct PrimaCanvas PrimaCanvas;

/* Create a zero-initialized RGBA8 canvas. Returns NULL on failure. */
PRIMA_C_API PrimaCanvas* prima_canvas_create(int width, int height);

/* Destroy a canvas. Safe to pass NULL. */
PRIMA_C_API void prima_canvas_destroy(PrimaCanvas* canvas);

PRIMA_C_API int prima_canvas_width(const PrimaCanvas* canvas);
PRIMA_C_API int prima_canvas_height(const PrimaCanvas* canvas);
PRIMA_C_API int prima_canvas_stride(const PrimaCanvas* canvas);

/* Fill the whole canvas with one color. */
PRIMA_C_API void prima_canvas_clear(PrimaCanvas* canvas,
                                    uint8_t r, uint8_t g, uint8_t b, uint8_t a);

/* Stamp one filled, bounds-clamped circular brush dab. */
PRIMA_C_API void prima_canvas_brush_dab(PrimaCanvas* canvas,
                                        int cx, int cy, int radius,
                                        uint8_t r, uint8_t g, uint8_t b, uint8_t a);

/* Return a pointer into the canvas's own pixel buffer (shared, not copied).
 * out_len receives the buffer length in bytes and out_stride the row stride in
 * bytes; either may be NULL. The pointer is valid until the canvas is destroyed. */
PRIMA_C_API uint8_t* prima_canvas_pixels(PrimaCanvas* canvas,
                                         size_t* out_len, int* out_stride);

/* --- Rendering ---------------------------------------------------------------
 * A renderer composites a canvas into a caller-owned RGBA8 target buffer through
 * a viewport (pan + zoom). The target may be UI-owned memory (e.g. a mapped
 * bitmap), so no pixels are copied across the boundary. */

/* Opaque handle to a render backend. */
typedef struct PrimaRenderer PrimaRenderer;

/* Create the software (CPU) render backend. Returns NULL on failure. */
PRIMA_C_API PrimaRenderer* prima_renderer_create_software(void);

/* Destroy a renderer. Safe to pass NULL. */
PRIMA_C_API void prima_renderer_destroy(PrimaRenderer* renderer);

/* Composite `canvas` into `target` (w x h, row stride in bytes) through the
 * viewport given by pan_x/pan_y (canvas-space point at the target origin) and
 * zoom (target pixels per canvas pixel). Target pixels mapping outside the
 * canvas are filled with the bg_* color. */
PRIMA_C_API void prima_render(PrimaRenderer* renderer, const PrimaCanvas* canvas,
                              uint8_t* target, int width, int height, int stride,
                              double pan_x, double pan_y, double zoom,
                              uint8_t bg_r, uint8_t bg_g, uint8_t bg_b,
                              uint8_t bg_a);

/* --- Brush / strokes ---------------------------------------------------------
 * A brush engine turns batched pointer input into dabs composited onto a canvas.
 * The add-samples path is the only per-input-event call: one call per UI pointer
 * event, with the event's coalesced samples passed in a single batch. */

/* All fields mirror prima::BrushParams / InputSample. */
typedef struct PrimaBrushParams {
    int32_t struct_size;          /* = sizeof(PrimaBrushParams); enables additive growth */
    float radius, hardness, opacity, flow, spacing;
    float size_pressure_min, size_pressure_gamma;
    float flow_pressure_min, flow_pressure_gamma;
    uint8_t r, g, b, a;
} PrimaBrushParams;

typedef struct PrimaInputSample {
    float x, y;                   /* canvas coords, subpixel */
    float pressure;               /* 0..1; pass 1.0 if the device has none */
    float tilt_x, tilt_y, rotation;   /* reserved, pass 0 */
    double time_ms;               /* reserved, pass 0 */
} PrimaInputSample;

typedef struct PrimaRect { int32_t x, y, width, height; } PrimaRect;  /* width<=0 => empty */

/* Opaque handle to a brush/stroke engine. One per document is typical. */
typedef struct PrimaBrushEngine PrimaBrushEngine;

PRIMA_C_API PrimaBrushEngine* prima_brush_engine_create(void);
PRIMA_C_API void prima_brush_engine_destroy(PrimaBrushEngine* engine);

/* Begin a stroke on `canvas` with `params` (copied). If a stroke is already
 * active it is ended first. `canvas` must outlive the stroke. */
PRIMA_C_API void prima_stroke_begin(PrimaBrushEngine* engine, PrimaCanvas* canvas,
                                    const PrimaBrushParams* params);

/* Feed `count` samples in one batched call — the ONLY per-input-event call;
 * one call per UI pointer event, samples coalesced inside. out_dirty
 * (optional) receives the canvas rect modified by this call. */
PRIMA_C_API void prima_stroke_add(PrimaBrushEngine* engine,
                                  const PrimaInputSample* samples, int count,
                                  PrimaRect* out_dirty);

PRIMA_C_API void prima_stroke_end(PrimaBrushEngine* engine, PrimaRect* out_dirty);

#ifdef __cplusplus
}  /* extern "C" */
#endif

#endif  /* PRIMA_C_H */
