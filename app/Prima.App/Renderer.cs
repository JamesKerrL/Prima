namespace Prima.App;

/// <summary>
/// A render backend that composites a <see cref="Document"/> into a caller-owned
/// RGBA8 target buffer through a <see cref="Viewport"/>. Currently the software
/// (CPU) backend; GPU backends will implement the same surface later.
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
