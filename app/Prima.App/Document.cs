namespace Prima.App;

/// <summary>
/// A drawable document backed by the native Prima engine. Owns the native canvas
/// handle and exposes safe drawing operations plus zero-copy access to the shared
/// pixel buffer. Has no UI dependencies and is fully usable headlessly — both the
/// desktop UI and the CLI host consume this same type.
/// </summary>
public sealed unsafe partial class Document : IDisposable
{
    private nint _handle;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Row stride in bytes (Width * 4 for RGBA8, no padding).</summary>
    public int Stride { get; }

    /// <summary>The native canvas handle, for use by other in-assembly types
    /// (e.g. <see cref="Renderer"/>). Throws if disposed.</summary>
    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public Document(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _handle = NativeMethods.prima_canvas_create(width, height);
        if (_handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native canvas.");

        Width = NativeMethods.prima_canvas_width(_handle);
        Height = NativeMethods.prima_canvas_height(_handle);
        Stride = NativeMethods.prima_canvas_stride(_handle);
    }

    /// <summary>Fill the whole canvas with a single color.</summary>
    public void Clear(Rgba color)
    {
        ThrowIfDisposed();
        NativeMethods.prima_canvas_clear(_handle, color.R, color.G, color.B, color.A);
    }

    /// <summary>Stamp one filled, bounds-clamped circular brush dab.</summary>
    public void BrushDab(int cx, int cy, int radius, Rgba color)
    {
        ThrowIfDisposed();
        NativeMethods.prima_canvas_brush_dab(
            _handle, cx, cy, radius, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// A view over the engine's own RGBA8 pixel buffer (row-major, no copy).
    /// Valid until this document is disposed.
    /// </summary>
    public Span<byte> Pixels
    {
        get
        {
            ThrowIfDisposed();
            byte* p = NativeMethods.prima_canvas_pixels(_handle, out nuint len, out _);
            return new Span<byte>(p, checked((int)len));
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
            throw new ObjectDisposedException(nameof(Document));
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            NativeMethods.prima_canvas_destroy(_handle);
            _handle = nint.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~Document() => Dispose();
}
