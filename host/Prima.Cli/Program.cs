using Prima.App;

// Prima headless host. The C# application layer is fully usable without a
// display; this CLI is a first-class consumer of it (not just a demo). Two
// modes:
//
//   (no args) | <path> [--ppm]   Canned smoke-test stroke -> image file.
//   script [file]                Drive the app with a command script (stdin if
//                                no file). Every document operation the GUI can
//                                perform is available here, so the whole app
//                                layer -- including undo/redo -- is operable and
//                                verifiable headlessly.
//
// Script commands (one per line; '#' comments and blank lines ignored):
//   new <w> <h>                  Create a canvas (cleared to white).
//   clear <r> <g> <b> <a>        Fill the canvas (undoable).
//   brush <radius> <r> <g> <b> <a>   Set the current brush.
//   stroke <x1> <y1> <x2> <y2> ...   Paint a stroke (undoable), pressure 1.
//   undo                         Undo the last edit.
//   redo                         Redo the last undone edit.
//   pixel <x> <y>                Print the RGBA at a pixel.
//   status                       Print modified / can-undo / can-redo state.
//   save <path>                  Save a PNG (marks the document saved).

if (args.Length > 0 && args[0] == "script")
{
    TextReader reader = args.Length > 1 ? new StreamReader(args[1]) : Console.In;
    using (reader == Console.In ? null : reader)
        return RunScript(reader);
}

RunDemo(args);
return 0;

static void RunDemo(string[] args)
{
    const int width = 256;
    const int height = 256;
    string outPath = args.Length > 0 ? args[0] : "prima-cli-output.png";
    bool usePpm = args.Length > 1 && args[1] == "--ppm";

    using var doc = new Document(width, height);
    doc.Clear(Rgba.White);

    var bp = BrushParams.Default(new Rgba(40, 90, 200, 255), radius: 14);
    bp.Hardness = 0.7f;
    bp.Opacity = 0.9f;
    bp.Flow = 0.8f;
    bp.SizePressureMin = 0.2f;
    bp.SizePressureGamma = 1.5f;
    bp.FlowPressureMin = 0.3f;

    using var engine = BrushEngine.Create();
    engine.BeginStroke(doc, bp);

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

    if (usePpm)
        WritePpm(outPath, doc);
    else
        doc.SaveAsPng(outPath);

    Console.WriteLine($"Wrote {width}x{height} canvas to {Path.GetFullPath(outPath)}");
}

// Interprets a command script against a live Document + BrushEngine session.
// Returns 0 on success, non-zero if a command failed.
static int RunScript(TextReader reader)
{
    Document? doc = null;
    BrushEngine? engine = null;
    var brush = BrushParams.Default(new Rgba(40, 90, 220, 255), radius: 12);

    try
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            string[] t = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string cmd = t[0].ToLowerInvariant();

            switch (cmd)
            {
                case "new":
                    doc?.Dispose();
                    engine?.Dispose();
                    doc = new Document(int.Parse(t[1]), int.Parse(t[2]));
                    doc.Clear(Rgba.White);
                    doc.History.Clear(); // the initial fill is setup, not an undoable edit
                    engine = BrushEngine.Create();
                    Console.WriteLine($"new {doc.Width}x{doc.Height}");
                    break;

                case "clear":
                    Require(doc);
                    doc!.Clear(new Rgba(B(t, 1), B(t, 2), B(t, 3), B(t, 4)));
                    Console.WriteLine("clear");
                    break;

                case "brush":
                    brush = BrushParams.Default(
                        new Rgba(B(t, 2), B(t, 3), B(t, 4), B(t, 5)),
                        radius: float.Parse(t[1]));
                    brush.Hardness = 1f;
                    Console.WriteLine("brush set");
                    break;

                case "stroke":
                {
                    Require(doc);
                    Require(engine);
                    int n = (t.Length - 1) / 2;
                    var samples = new InputSample[n];
                    for (int i = 0; i < n; i++)
                        samples[i] = new InputSample(
                            float.Parse(t[1 + i * 2]), float.Parse(t[2 + i * 2]), 1f);

                    engine!.BeginStroke(doc!, brush);
                    DirtyRect dirty = engine.AddSamples(samples);
                    dirty = dirty.Union(engine.EndStroke());
                    doc!.PushStrokeEdit(engine, dirty);
                    Console.WriteLine($"stroke dirty=({dirty.X},{dirty.Y},{dirty.Width},{dirty.Height})");
                    break;
                }

                case "undo":
                {
                    Require(doc);
                    DirtyRect r = doc!.Undo();
                    Console.WriteLine($"undo dirty=({r.X},{r.Y},{r.Width},{r.Height})");
                    break;
                }

                case "redo":
                {
                    Require(doc);
                    DirtyRect r = doc!.Redo();
                    Console.WriteLine($"redo dirty=({r.X},{r.Y},{r.Width},{r.Height})");
                    break;
                }

                case "pixel":
                {
                    Require(doc);
                    int x = int.Parse(t[1]), y = int.Parse(t[2]);
                    int idx = y * doc!.Stride + x * 4;
                    ReadOnlySpan<byte> px = doc.Pixels;
                    Console.WriteLine($"pixel {x} {y} = {px[idx]} {px[idx + 1]} {px[idx + 2]} {px[idx + 3]}");
                    break;
                }

                case "status":
                    Require(doc);
                    Console.WriteLine(
                        $"status modified={doc!.IsModified} canUndo={doc.History.CanUndo} canRedo={doc.History.CanRedo}");
                    break;

                case "save":
                    Require(doc);
                    doc!.SaveAsPng(t[1]);
                    Console.WriteLine($"saved {Path.GetFullPath(t[1])}");
                    break;

                default:
                    Console.Error.WriteLine($"unknown command: {cmd}");
                    return 1;
            }
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
    finally
    {
        engine?.Dispose();
        doc?.Dispose();
    }

    static byte B(string[] t, int i) => byte.Parse(t[i]);
    static void Require(object? o)
    {
        if (o is null) throw new InvalidOperationException("no document; run 'new <w> <h>' first");
    }
}

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
