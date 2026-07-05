using System.Runtime.InteropServices;

namespace Prima.App;

[StructLayout(LayoutKind.Sequential)]
public struct BrushParams
{
    internal int StructSize;
    public float Radius, Hardness, Opacity, Flow, Spacing;
    public float SizePressureMin, SizePressureGamma;
    public float FlowPressureMin, FlowPressureGamma;
    public byte R, G, B, A;

    public static BrushParams Default(Rgba color, float radius = 12f) => new()
    {
        StructSize = Marshal.SizeOf<BrushParams>(),
        Radius = radius,
        Hardness = 0.8f,
        Opacity = 1f,
        Flow = 1f,
        Spacing = 0.15f,
        SizePressureMin = 1f,
        SizePressureGamma = 1f,
        FlowPressureMin = 1f,
        FlowPressureGamma = 1f,
        R = color.R,
        G = color.G,
        B = color.B,
        A = color.A,
    };
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct InputSample(
    float X, float Y, float Pressure = 1f,
    float TiltX = 0f, float TiltY = 0f, float Rotation = 0f, double TimeMs = 0.0);

public readonly record struct DirtyRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public DirtyRect Union(DirtyRect other)
    {
        if (other.IsEmpty) return this;
        if (IsEmpty) return other;
        int x1 = Math.Min(X, other.X);
        int y1 = Math.Min(Y, other.Y);
        int x2 = Math.Max(X + Width, other.X + other.Width);
        int y2 = Math.Max(Y + Height, other.Y + other.Height);
        return new DirtyRect(x1, y1, x2 - x1, y2 - y1);
    }
}
