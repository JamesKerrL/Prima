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

    /// <summary>Fill the whole canvas with a single color. Undoable.</summary>
    public void Clear(Rgba color) => Clear(color, recordHistory: true);

    /// <summary>
    /// Fill the whole canvas with a single color. When <paramref name="recordHistory"/>
    /// is false, no <see cref="FillEdit"/> is pushed — used by <see cref="FillEdit.Redo"/>
    /// itself to avoid re-recording the same edit it is replaying.
    /// </summary>
    internal void Clear(Rgba color, bool recordHistory)
    {
        ThrowIfDisposed();

        if (recordHistory)
        {
            byte[] before = ReadRegion(new DirtyRect(0, 0, Width, Height));
            NativeMethods.prima_canvas_clear(_handle, color.R, color.G, color.B, color.A);
            History.Push(new FillEdit(before, color, "Clear"));
        }
        else
        {
            NativeMethods.prima_canvas_clear(_handle, color.R, color.G, color.B, color.A);
        }
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

    /// <summary>
    /// Copy a packed RGBA8 region (row-major, no padding) out of the canvas.
    /// The rect is clamped to canvas bounds; an empty/out-of-bounds rect
    /// yields an empty array.
    /// </summary>
    public byte[] ReadRegion(DirtyRect region)
    {
        ThrowIfDisposed();
        region = ClampToBounds(region);
        if (region.IsEmpty) return Array.Empty<byte>();

        var dst = new byte[region.Width * region.Height * 4];
        Span<byte> src = Pixels;
        int rowBytes = region.Width * 4;
        for (int row = 0; row < region.Height; row++)
        {
            int srcOffset = (region.Y + row) * Stride + region.X * 4;
            src.Slice(srcOffset, rowBytes).CopyTo(dst.AsSpan(row * rowBytes, rowBytes));
        }
        return dst;
    }

    /// <summary>
    /// Write a packed RGBA8 region (as produced by <see cref="ReadRegion"/>)
    /// back into the canvas at the given rect.
    /// </summary>
    public void WriteRegion(DirtyRect region, ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        region = ClampToBounds(region);
        if (region.IsEmpty) return;

        Span<byte> dst = Pixels;
        int rowBytes = region.Width * 4;
        for (int row = 0; row < region.Height; row++)
        {
            int dstOffset = (region.Y + row) * Stride + region.X * 4;
            data.Slice(row * rowBytes, rowBytes).CopyTo(dst.Slice(dstOffset, rowBytes));
        }
    }

    private DirtyRect ClampToBounds(DirtyRect r)
    {
        if (r.IsEmpty) return default;
        int x1 = Math.Max(r.X, 0);
        int y1 = Math.Max(r.Y, 0);
        int x2 = Math.Min(r.X + r.Width, Width);
        int y2 = Math.Min(r.Y + r.Height, Height);
        if (x2 <= x1 || y2 <= y1) return default;
        return new DirtyRect(x1, y1, x2 - x1, y2 - y1);
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
