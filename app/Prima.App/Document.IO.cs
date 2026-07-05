namespace Prima.App;

public sealed unsafe partial class Document
{
    /// <summary>Load an image file into a new Document. Returns null on failure.</summary>
    public static Document? LoadFromFile(string path)
    {
        byte* nativePixels = NativeMethods.prima_image_load_file(
            path, out int w, out int h);
        if (nativePixels == null) return null;

        try
        {
            var doc = new Document(w, h);
            new Span<byte>(nativePixels, w * h * 4).CopyTo(doc.Pixels);
            return doc;
        }
        finally
        {
            NativeMethods.prima_image_free(nativePixels);
        }
    }

    /// <summary>Save the canvas as a PNG file.</summary>
    public void SaveAsPng(string path)
    {
        ThrowIfDisposed();
        fixed (byte* p = Pixels)
        {
            int result = NativeMethods.prima_image_save_png(
                path, p, Width, Height);
            if (result != 0)
                throw new InvalidOperationException(
                    $"Failed to save PNG: {path}");
        }
        MarkSaved();
    }

    /// <summary>
    /// Save the canvas as a JPEG file with the given quality (1-100).
    /// Since JPEG has no alpha, the canvas is composited against the specified
    /// background color (defaults to white).
    /// </summary>
    public void SaveAsJpeg(string path, int quality = 90, Rgba? background = null)
    {
        ThrowIfDisposed();
        Rgba bg = background ?? Rgba.White;
        int pixelCount = Width * Height;
        byte[] composite = new byte[pixelCount * 3]; // RGB only for JPEG

        Span<byte> src = Pixels;
        for (int i = 0; i < pixelCount; i++)
        {
            int si = i * 4;
            int di = i * 3;
            byte r = src[si];
            byte g = src[si + 1];
            byte b = src[si + 2];
            byte a = src[si + 3];

            if (a == 255)
            {
                composite[di] = r;
                composite[di + 1] = g;
                composite[di + 2] = b;
            }
            else
            {
                float fa = a / 255f;
                float inv = 1f - fa;
                composite[di] = (byte)(r * fa + bg.R * inv);
                composite[di + 1] = (byte)(g * fa + bg.G * inv);
                composite[di + 2] = (byte)(b * fa + bg.B * inv);
            }
        }

        fixed (byte* p = composite)
        {
            int result = NativeMethods.prima_image_save_jpeg(
                path, p, Width, Height, quality);
            if (result != 0)
                throw new InvalidOperationException(
                    $"Failed to save JPEG: {path}");
        }
        MarkSaved();
    }
}
