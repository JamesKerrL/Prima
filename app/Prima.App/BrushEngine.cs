namespace Prima.App;

public sealed unsafe class BrushEngine : IDisposable
{
    private nint _handle;
    private bool _strokeActive;

    private BrushEngine(nint handle) => _handle = handle;

    public static BrushEngine Create()
    {
        nint h = NativeMethods.prima_brush_engine_create();
        if (h == nint.Zero)
            throw new InvalidOperationException("Failed to create brush engine.");
        return new BrushEngine(h);
    }

    public bool StrokeActive => _handle != nint.Zero && _strokeActive;

    public void BeginStroke(Document document, in BrushParams parameters)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(document);

        fixed (BrushParams* p = &parameters)
        {
            NativeMethods.prima_stroke_begin(_handle, document.Handle, p);
        }
        _strokeActive = true;
    }

    public DirtyRect AddSamples(ReadOnlySpan<InputSample> samples)
    {
        ThrowIfDisposed();
        if (samples.IsEmpty)
        {
            var empty = default(NativeMethods.PrimaRect);
            return new DirtyRect(empty.X, empty.Y, empty.Width, empty.Height);
        }

        fixed (InputSample* p = samples)
        {
            NativeMethods.prima_stroke_add(_handle, p, samples.Length, out var dirty);
            return new DirtyRect(dirty.X, dirty.Y, dirty.Width, dirty.Height);
        }
    }

    public DirtyRect EndStroke()
    {
        ThrowIfDisposed();
        NativeMethods.prima_stroke_end(_handle, out var dirty);
        _strokeActive = false;
        return new DirtyRect(dirty.X, dirty.Y, dirty.Width, dirty.Height);
    }

    /// <summary>
    /// Read a packed RGBA8 region out of the current/most recent stroke's
    /// frozen "before" snapshot — the pre-stroke pixels, for undo history.
    /// </summary>
    public byte[] ReadBaselineRegion(DirtyRect region)
    {
        ThrowIfDisposed();
        if (region.IsEmpty) return Array.Empty<byte>();

        var buffer = new byte[checked(region.Width * region.Height * 4)];
        fixed (byte* p = buffer)
        {
            int result = NativeMethods.prima_brush_engine_read_baseline_region(
                _handle, region.X, region.Y, region.Width, region.Height, p);
            if (result != 0)
                throw new InvalidOperationException(
                    "Failed to read stroke baseline region (no active baseline or rect out of bounds).");
        }
        return buffer;
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
            throw new ObjectDisposedException(nameof(BrushEngine));
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            NativeMethods.prima_brush_engine_destroy(_handle);
            _handle = nint.Zero;
        }
        _strokeActive = false;
        GC.SuppressFinalize(this);
    }

    ~BrushEngine() => Dispose();
}
