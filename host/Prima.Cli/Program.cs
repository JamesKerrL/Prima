using Prima.App;

// Headless smoke test of the full engine stack: no UI, no display required.
// Creates a canvas, paints a few brush dabs, and writes the result as a PNG
// image. Proves the app layer + interop + engine all work without any frontend.

const int width = 256;
const int height = 256;
string outPath = args.Length > 0 ? args[0] : "prima-cli-output.png";
bool usePpm = args.Length > 1 && args[1] == "--ppm";

using var doc = new Document(width, height);
doc.Clear(Rgba.White);
doc.BrushDab(80, 128, 40, new Rgba(220, 40, 40, 255));    // red
doc.BrushDab(176, 128, 40, new Rgba(40, 80, 220, 255));   // blue
doc.BrushDab(128, 128, 30, new Rgba(40, 180, 40, 255));   // green overlap

if (usePpm)
    WritePpm(outPath, doc);
else
    doc.SaveAsPng(outPath);

Console.WriteLine($"Wrote {width}x{height} canvas to {Path.GetFullPath(outPath)}");

// Binary PPM (P6) — kept as a debug output option (--ppm).
static void WritePpm(string path, Document doc)
{
    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
    using var w = new BinaryWriter(fs);
    w.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{doc.Width} {doc.Height}\n255\n"));

    ReadOnlySpan<byte> px = doc.Pixels;
    Span<byte> rgb = stackalloc byte[3];
    for (int i = 0; i < px.Length; i += 4)
    {
        rgb[0] = px[i + 0];
        rgb[1] = px[i + 1];
        rgb[2] = px[i + 2];
        w.Write(rgb);
    }
}
