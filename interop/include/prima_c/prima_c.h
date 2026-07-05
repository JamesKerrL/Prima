#ifndef PRIMA_C_H
#define PRIMA_C_H

/* C ABI for the Prima engine. This is the single, narrow boundary between the
 * native C++ core and the managed C# application layer. Keep it coarse-grained:
 * no per-pixel calls across this line - pixel data is shared via
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

/* --- Color -------------------------------------------------------------------
 * HSV<->RGBA conversions and the hue-ring/SV-triangle bitmap renderer used by
 * the color picker. Pixel buffers are shared (pointer into engine-owned
 * memory), never copied across the boundary, same pattern as
 * prima_canvas_pixels(). */

PRIMA_C_API void prima_color_rgba_to_hsv(uint8_t r, uint8_t g, uint8_t b,
                                         double* h, double* s, double* v);
PRIMA_C_API void prima_color_hsv_to_rgba(double h, double s, double v,
                                         uint8_t* r, uint8_t* g, uint8_t* b);

/* Opaque handle to a native color wheel (ring + SV-triangle bitmaps). */
typedef struct PrimaColorWheel PrimaColorWheel;

/* Create a color wheel. Returns NULL on failure. */
PRIMA_C_API PrimaColorWheel* prima_colorwheel_create(int outer_size,
                                                     int ring_thickness);

/* Destroy a color wheel. Safe to pass NULL. */
PRIMA_C_API void prima_colorwheel_destroy(PrimaColorWheel* wheel);

/* Regenerate the triangle bitmap for a new hue (degrees). */
PRIMA_C_API void prima_colorwheel_set_hue(PrimaColorWheel* wheel, double hue);

/* Pointers into the wheel's own RGBA8 pixel buffers (shared, not copied).
 * out_w/out_h receive the buffer dimensions; either may be NULL. Valid until
 * the wheel is destroyed. */
PRIMA_C_API const uint8_t* prima_colorwheel_ring_pixels(PrimaColorWheel* wheel,
                                                        int* out_w, int* out_h);
PRIMA_C_API const uint8_t* prima_colorwheel_triangle_pixels(PrimaColorWheel* wheel,
                                                            int* out_w, int* out_h);

#ifdef __cplusplus
}  /* extern "C" */
#endif

#endif  /* PRIMA_C_H */
