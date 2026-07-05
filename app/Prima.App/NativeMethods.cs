using System.Runtime.InteropServices;

namespace Prima.App;

/// <summary>
/// Raw P/Invoke bindings to the native <c>prima_c</c> shared library. Mirrors
/// interop/include/prima_c/prima_c.h one-to-one. Internal on purpose — callers
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
    internal static partial byte* prima_canvas_pixels(
        nint canvas, out nuint outLen, out int outStride);

    [LibraryImport(Lib)]
    internal static partial nint prima_renderer_create_software();

    [LibraryImport(Lib)]
    internal static partial void prima_renderer_destroy(nint renderer);

    [LibraryImport(Lib)]
    internal static partial void prima_render(
        nint renderer, nint canvas, byte* target, int width, int height, int stride,
        double panX, double panY, double zoom,
        byte bgR, byte bgG, byte bgB, byte bgA);
}
