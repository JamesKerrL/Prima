using System.Collections.ObjectModel;

namespace Prima.App;

/// <summary>
/// User-curated color swatches, in the order the user added them (unlike
/// <see cref="ColorHistory"/>, this is not MRU-ordered and has no cap - the
/// user manages it explicitly).
/// </summary>
public sealed class SwatchPalette
{
    private readonly List<Rgba> _swatches = [];

    public ReadOnlyCollection<Rgba> Swatches => _swatches.AsReadOnly();

    /// <summary>Appends a swatch. No-op if the exact color is already saved
    /// (a palette is a set of distinct colors, not a log).</summary>
    public void Add(Rgba color)
    {
        if (_swatches.Contains(color)) return;
        _swatches.Add(color);
    }

    /// <summary>Removes a swatch by value. No-op if not present.</summary>
    public void Remove(Rgba color) => _swatches.Remove(color);

    public void AddRange(IEnumerable<Rgba> colors)
    {
        foreach (var color in colors)
            Add(color);
    }

    public void Clear() => _swatches.Clear();
}
