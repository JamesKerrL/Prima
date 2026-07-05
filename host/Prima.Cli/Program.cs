using Prima.App;

// Headless smoke test of the full engine stack with the new brush engine.
// Creates a canvas, draws a pressure-varying stroke through BrushEngine, and
// writes the result to a PPM image. Proves the app layer + interop + engine
// all work without any frontend.

const int width = 256;
const int height = 256;
string outPath = args.Length > 0 ? args[0] : "prima-cli-output.ppm";

using var doc = new Document(width, height);
doc.Clear(Rgba.White);

// Canned pressure-varying stroke using the new brush engine.
var bp = BrushParams.Default(new Rgba(40, 90, 200, 255), radius: 14);
bp.Hardness = 0.7f;
bp.Opacity = 0.9f;
bp.Flow = 0.8f;
bp.SizePressureMin = 0.2f;   // pressure 0 -> 20% of base radius
bp.SizePressureGamma = 1.5f;
bp.FlowPressureMin = 0.3f;   // pressure 0 -> 30% of base flow

using var engine = BrushEngine.Create();
engine.BeginStroke(doc, bp);

// A swooping stroke with varying pressure.
var samples = new InputSample[]
{
    new(30f, 50f, 0.1f),
    new(40f, 55f, 0.2f),
    new(55f, 65f, 0.3f),
    new(70f, 80f, 0.4f),
    new(90f, 95f, 0.5f),
    new(110f, 110f, 0.6f),
    new(130f, 120f, 0.7f),
    new(150f, 128f, 0.8f),
    new(170f, 130f, 0.9f),
    new(190f, 128f, 1.0f),
    new(210f, 120f, 0.7f),
    new(225f, 110f, 0.3f),
};
engine.AddSamples(samples);
engine.EndStroke();

WritePpm(outPath, doc);
Console.WriteLine($"Wrote {width}x{height} canvas to {Path.GetFullPath(outPath)}");

// Binary PPM (P6) is RGB-only, so the alpha channel is dropped on write.
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
