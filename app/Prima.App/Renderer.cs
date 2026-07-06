using System.Runtime.InteropServices;

namespace Prima.App;

/// <summary>
/// A render backend that composites a <see cref="Document"/> into a caller-owned
/// RGBA8 target buffer through a <see cref="Viewport"/>. Backends: software
/// (CPU, always available) and Direct3D 11 (Windows, hardware → WARP fallback).
/// </summary>
public sealed unsafe class Renderer : IDisposable
{
    private nint _handle;

    private Renderer(nint handle) => _handle = handle;

    /// <summary>Create the software (CPU) render backend.</summary>
    public static Renderer CreateSoftware()
    {
        nint h = NativeMethods.prima_renderer_create_software();
        if (h == nint.Zero)
            throw new InvalidOperationException("Failed to create software renderer.");
        return new Renderer(h);
    }

    /// <summary>
    /// Create the Direct3D 11 render backend (hardware device, falling back to
    /// WARP), or null when D3D11 is unavailable — an expected condition on
    /// non-Windows builds, not an error.
    /// </summary>
    public static Renderer? CreateD3D11()
    {
        nint h = NativeMethods.prima_renderer_create_d3d11();
        return h == nint.Zero ? null : new Renderer(h);
    }

    /// <summary>Best available backend: D3D11 if it initializes, else software.</summary>
    public static Renderer CreateBest() => CreateD3D11() ?? CreateSoftware();

    /// <summary>Backend name reported by the engine ("software", "d3d11").</summary>
    public string Name
    {
        get
        {
            ThrowIfDisposed();
            return Marshal.PtrToStringUTF8(NativeMethods.prima_renderer_name(_handle)) ?? "";
        }
    }

    /// <summary>
    /// Composite <paramref name="document"/> into <paramref name="target"/>
    /// (<paramref name="width"/> x <paramref name="height"/>, row
    /// <paramref name="stride"/> in bytes) through <paramref name="viewport"/>.
    /// Target pixels mapping outside the canvas are filled with
    /// <paramref name="background"/>.
    /// </summary>
    public void Render(Document document, Span<byte> target, int width, int height,
                       int stride, Viewport viewport, Rgba background)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(document);
        if (stride < width * 4)
            throw new ArgumentOutOfRangeException(nameof(stride), "Stride too small for width.");
        if (target.Length < (long)stride * height)
            throw new ArgumentException("Target buffer smaller than height * stride.", nameof(target));

        fixed (byte* p = target)
        {
            NativeMethods.prima_render(
                _handle, document.Handle, p, width, height, stride,
                viewport.PanX, viewport.PanY, viewport.Zoom,
                background.R, background.G, background.B, background.A);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
            throw new ObjectDisposedException(nameof(Renderer));
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            NativeMethods.prima_renderer_destroy(_handle);
            _handle = nint.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~Renderer() => Dispose();
}
