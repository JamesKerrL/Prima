namespace Prima.App;

/// <summary>
/// Owns a native hue-ring + SV-triangle bitmap pair for the color picker.
/// Mirrors <see cref="Document"/>'s native-handle ownership: the ring bitmap
/// is generated once at construction; the triangle is regenerated on
/// <see cref="SetHue"/>. Both pixel spans are zero-copy views over the
/// engine's own buffers, valid until this wheel is disposed.
/// </summary>
public sealed unsafe class ColorWheel : IDisposable
{
    private nint _handle;

    public int RingWidth { get; }
    public int RingHeight { get; }
    public int TriangleWidth { get; }
    public int TriangleHeight { get; }

    public ColorWheel(int outerSize, int ringThickness)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outerSize);
        ArgumentOutOfRangeException.ThrowIfNegative(ringThickness);

        _handle = NativeMethods.prima_colorwheel_create(outerSize, ringThickness);
        if (_handle == nint.Zero)
            throw new InvalidOperationException("Failed to create native color wheel.");

        NativeMethods.prima_colorwheel_ring_pixels(_handle, out int ringW, out int ringH);
        NativeMethods.prima_colorwheel_triangle_pixels(_handle, out int triW, out int triH);
        RingWidth = ringW;
        RingHeight = ringH;
        TriangleWidth = triW;
        TriangleHeight = triH;
    }

    /// <summary>Regenerates the triangle bitmap for a new hue (degrees). The
    /// ring bitmap is unaffected.</summary>
    public void SetHue(double hueDegrees)
    {
        ThrowIfDisposed();
        NativeMethods.prima_colorwheel_set_hue(_handle, hueDegrees);
    }

    /// <summary>A view over the engine's own ring RGBA8 pixel buffer
    /// (row-major, no copy). Valid until this wheel is disposed.</summary>
    public ReadOnlySpan<byte> RingPixels
    {
        get
        {
            ThrowIfDisposed();
            byte* p = NativeMethods.prima_colorwheel_ring_pixels(_handle, out int w, out int h);
            return new ReadOnlySpan<byte>(p, checked(w * h * 4));
        }
    }

    /// <summary>A view over the engine's own triangle RGBA8 pixel buffer
    /// (row-major, no copy). Valid until this wheel is disposed.</summary>
    public ReadOnlySpan<byte> TrianglePixels
    {
        get
        {
            ThrowIfDisposed();
            byte* p = NativeMethods.prima_colorwheel_triangle_pixels(_handle, out int w, out int h);
            return new ReadOnlySpan<byte>(p, checked(w * h * 4));
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
            throw new ObjectDisposedException(nameof(ColorWheel));
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            NativeMethods.prima_colorwheel_destroy(_handle);
            _handle = nint.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~ColorWheel() => Dispose();
}
