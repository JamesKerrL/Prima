using System.Collections.ObjectModel;

namespace Prima.App;

/// <summary>
/// Most-recently-used history of picked colors. Most recent is first;
/// re-picking a color already in the history moves it to the front instead of
/// duplicating it; the list is capped at <see cref="Capacity"/>, evicting the
/// oldest entry once full.
/// </summary>
public sealed class ColorHistory
{
    public const int DefaultCapacity = 20;

    private readonly LinkedList<Rgba> _colors = new();

    public int Capacity { get; }

    public ColorHistory(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public ReadOnlyCollection<Rgba> Colors => new(_colors.ToList());

    /// <summary>Records a pick: moves the color to the front if already
    /// present, otherwise inserts it at the front and evicts the oldest entry
    /// if over capacity.</summary>
    public void Record(Rgba color)
    {
        _colors.Remove(color);
        _colors.AddFirst(color);
        while (_colors.Count > Capacity)
            _colors.RemoveLast();
    }

    public void Clear() => _colors.Clear();
}
