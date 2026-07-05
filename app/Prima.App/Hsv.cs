using System.Globalization;

namespace Prima.App;

/// <summary>
/// An HSV color: <see cref="H"/> in [0,360), <see cref="S"/>/<see cref="V"/> in
/// [0,1]. Conversions to/from <see cref="Rgba"/> call into the native color-
/// science core; HEX parsing/formatting is trivial string work and stays here.
/// </summary>
public readonly record struct Hsv(double H, double S, double V)
{
    /// <summary>Converts to RGBA via the native HSV->RGBA conversion. Alpha
    /// passes through unchanged.</summary>
    public Rgba ToRgba(byte alpha = 255)
    {
        NativeMethods.prima_color_hsv_to_rgba(H, S, V, out byte r, out byte g, out byte b);
        return new Rgba(r, g, b, alpha);
    }

    /// <summary>Converts from RGBA via the native RGBA->HSV conversion.</summary>
    public static Hsv FromRgba(Rgba color)
    {
        NativeMethods.prima_color_rgba_to_hsv(color.R, color.G, color.B, out double h, out double s, out double v);
        return new Hsv(h, s, v);
    }

    /// <summary>Formats as an uppercase "#RRGGBB" hex string (optionally with alpha).</summary>
    public static string ToHex(Rgba color, bool includeAlpha = false) =>
        includeAlpha
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// Parses a "#RRGGBB", "#RGB", "RRGGBB", or "RGB" hex string into an RGBA
    /// color with full opacity. Returns false if the string isn't a valid hex
    /// color.
    /// </summary>
    public static bool TryParseHex(string? text, out Rgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string s = text.Trim();
        if (s.StartsWith('#')) s = s[1..];

        if (s.Length == 3)
        {
            s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        }
        if (s.Length != 6 && s.Length != 8) return false;

        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r))
            return false;
        if (!byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g))
            return false;
        if (!byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            return false;

        byte a = 255;
        if (s.Length == 8)
        {
            if (!byte.TryParse(s.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a))
                return false;
        }

        color = new Rgba(r, g, b, a);
        return true;
    }
}
