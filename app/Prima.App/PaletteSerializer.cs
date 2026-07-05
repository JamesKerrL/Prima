using System.Text.Json;

namespace Prima.App;

public static class PaletteSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string Serialize(SwatchPalette palette)
    {
        var colors = new List<string>(palette.Swatches.Count);
        foreach (var color in palette.Swatches)
            colors.Add(Hsv.ToHex(color, includeAlpha: true));
        return JsonSerializer.Serialize(new PaletteJson { Colors = colors }, JsonOptions);
    }

    public static SwatchPalette Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<PaletteJson>(json);
        var palette = new SwatchPalette();
        if (data?.Colors is not null)
        {
            foreach (var hex in data.Colors)
            {
                if (Hsv.TryParseHex(hex, out var color))
                    palette.Add(color);
            }
        }
        return palette;
    }

    public static void SaveToFile(SwatchPalette palette, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var json = Serialize(palette);
        File.WriteAllText(path, json);
    }

    public static SwatchPalette LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return Deserialize(json);
    }

    private sealed class PaletteJson
    {
        public List<string>? Colors { get; set; }
    }
}
