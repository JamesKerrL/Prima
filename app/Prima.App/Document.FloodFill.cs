namespace Prima.App;

public sealed unsafe partial class Document
{
    /// <summary>
    /// Flood fill the contiguous region at (<paramref name="x"/>, <paramref name="y"/>)
    /// with <paramref name="color"/>, matching pixels within
    /// <paramref name="tolerance"/> per channel of the seed color (0 = exact).
    /// Recorded as a single undoable <see cref="RegionEdit"/>. Returns the rect
    /// to re-render — empty (and a no-op with no history entry) when nothing
    /// changed: an out-of-bounds seed, or filling with the seed's own color.
    /// </summary>
    public DirtyRect FloodFill(int x, int y, Rgba color, int tolerance = 0)
    {
        ThrowIfDisposed();

        // The fill's dirty rect isn't known until the native call returns, and
        // it mutates in place, so the "before" pixels can't be read afterwards.
        // Snapshot the whole canvas up front; only the dirty sub-rect is
        // retained in history (the full copy is transient — fill is a discrete
        // click, not a hot path).
        var full = new DirtyRect(0, 0, Width, Height);
        byte[] fullBefore = ReadRegion(full);

        NativeMethods.prima_canvas_flood_fill(
            _handle, x, y, color.R, color.G, color.B, color.A, tolerance,
            out var pr);
        var dirty = new DirtyRect(pr.X, pr.Y, pr.Width, pr.Height);
        if (dirty.IsEmpty) return dirty;

        byte[] before = ExtractRegion(fullBefore, full, dirty);
        byte[] after = ReadRegion(dirty);
        History.Push(new RegionEdit(dirty, before, after, "Flood fill"));
        return dirty;
    }

    /// <summary>
    /// Copy the packed RGBA8 sub-region <paramref name="sub"/> (which must lie
    /// within <paramref name="full"/>) out of a snapshot of the
    /// <paramref name="full"/> region.
    /// </summary>
    private static byte[] ExtractRegion(byte[] fullPixels, DirtyRect full, DirtyRect sub)
    {
        var dst = new byte[sub.Width * sub.Height * 4];
        int fullRowBytes = full.Width * 4;
        int subRowBytes = sub.Width * 4;
        for (int row = 0; row < sub.Height; row++)
        {
            int srcOffset = (sub.Y - full.Y + row) * fullRowBytes + (sub.X - full.X) * 4;
            Array.Copy(fullPixels, srcOffset, dst, row * subRowBytes, subRowBytes);
        }
        return dst;
    }
}
