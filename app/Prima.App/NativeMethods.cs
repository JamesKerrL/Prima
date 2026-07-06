using System.Runtime.InteropServices;

namespace Prima.App;

/// <summary>
/// Raw P/Invoke bindings to the native <c>prima_c</c> shared library. Mirrors
/// interop/include/prima_c/prima_c.h one-to-one. Internal on purpose - callers
/// go through <see cref="Document"/>, never these directly.
/// </summary>
internal static unsafe partial class NativeMethods
{
    private const string Lib = "prima_c";

    [LibraryImport(Lib)]
    internal static partial nint prima_canvas_create(int width, int height);

    [LibraryImport(Lib)]
    internal static partial void prima_canvas_destroy(nint canvas);

    [LibraryImport(Lib)]
    internal static partial int prima_canvas_width(nint canvas);

    [LibraryImport(Lib)]
    internal static partial int prima_canvas_height(nint canvas);

    [LibraryImport(Lib)]
    internal static partial int prima_canvas_stride(nint canvas);

    [LibraryImport(Lib)]
    internal static partial void prima_canvas_clear(
        nint canvas, byte r, byte g, byte b, byte a);

    [LibraryImport(Lib)]
    internal static partial void prima_canvas_brush_dab(
        nint canvas, int cx, int cy, int radius, byte r, byte g, byte b, byte a);

    [LibraryImport(Lib)]
    internal static partial void prima_canvas_flood_fill(
        nint canvas, int seedX, int seedY, byte r, byte g, byte b, byte a,
        int tolerance, out PrimaRect outDirty);

    [LibraryImport(Lib)]
    internal static partial byte* prima_canvas_pixels(
        nint canvas, out nuint outLen, out int outStride);

    [LibraryImport(Lib)]
    internal static partial nint prima_renderer_create_software();

    [LibraryImport(Lib)]
    internal static partial nint prima_renderer_create_d3d11();

    [LibraryImport(Lib)]
    internal static partial nint prima_renderer_name(nint renderer);

    [LibraryImport(Lib)]
    internal static partial void prima_renderer_destroy(nint renderer);

    [LibraryImport(Lib)]
    internal static partial void prima_render(
        nint renderer, nint canvas, byte* target, int width, int height, int stride,
        double panX, double panY, double zoom,
        byte bgR, byte bgG, byte bgB, byte bgA);

    // --- Color -----------------------------------------------------------------

    [LibraryImport(Lib)]
    internal static partial void prima_color_rgba_to_hsv(
        byte r, byte g, byte b, out double h, out double s, out double v);

    [LibraryImport(Lib)]
    internal static partial void prima_color_hsv_to_rgba(
        double h, double s, double v, out byte r, out byte g, out byte b);

    [LibraryImport(Lib)]
    internal static partial nint prima_colorwheel_create(int outerSize, int ringThickness);

    [LibraryImport(Lib)]
    internal static partial void prima_colorwheel_destroy(nint wheel);

    [LibraryImport(Lib)]
    internal static partial void prima_colorwheel_set_hue(nint wheel, double hue);

    [LibraryImport(Lib)]
    internal static partial byte* prima_colorwheel_ring_pixels(
        nint wheel, out int outW, out int outH);

    [LibraryImport(Lib)]
    internal static partial byte* prima_colorwheel_triangle_pixels(
        nint wheel, out int outW, out int outH);

    // --- Image I/O -------------------------------------------------------------

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial byte* prima_image_load_file(
        string path, out int outWidth, out int outHeight);

    [LibraryImport(Lib)]
    internal static partial byte* prima_image_load_memory(
        byte* data, int len, out int outWidth, out int outHeight);

    [LibraryImport(Lib)]
    internal static partial void prima_image_free(byte* pixels);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int prima_image_save_png(
        string path, byte* pixels, int width, int height);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int prima_image_save_jpeg(
        string path, byte* pixels, int width, int height, int quality);

    // --- Brush / stroke engine ---

    [StructLayout(LayoutKind.Sequential)]
    internal struct PrimaRect
    {
        public int X, Y, Width, Height;
    }

    [LibraryImport(Lib)]
    internal static partial nint prima_brush_engine_create();

    [LibraryImport(Lib)]
    internal static partial void prima_brush_engine_destroy(nint engine);

    [LibraryImport(Lib)]
    internal static partial void prima_stroke_begin(
        nint engine, nint canvas, BrushParams* parameters);

    [LibraryImport(Lib)]
    internal static partial void prima_stroke_add(
        nint engine, InputSample* samples, int count, out PrimaRect outDirty);

    [LibraryImport(Lib)]
    internal static partial void prima_stroke_end(
        nint engine, out PrimaRect outDirty);

    [LibraryImport(Lib)]
    internal static partial int prima_brush_engine_read_baseline_region(
        nint engine, int x, int y, int w, int h, byte* dst);
}
