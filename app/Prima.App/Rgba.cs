namespace Prima.App;

/// <summary>A packed RGBA8 color, one byte per channel.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);
    public static readonly Rgba White = new(255, 255, 255, 255);
    public static readonly Rgba Black = new(0, 0, 0, 255);
}
