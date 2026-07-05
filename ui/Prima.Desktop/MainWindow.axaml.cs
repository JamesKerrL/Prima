using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Prima.App;

namespace Prima.Desktop;

public partial class MainWindow : Window
{
    private const int CanvasWidth = 640;
    private const int CanvasHeight = 480;
    private const int BrushRadius = 12;

    private readonly Document _document;
    private readonly WriteableBitmap _bitmap;

    public MainWindow()
    {
        InitializeComponent();

        _document = new Document(CanvasWidth, CanvasHeight);
        _document.Clear(Rgba.White);

        _bitmap = new WriteableBitmap(
            new PixelSize(CanvasWidth, CanvasHeight),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        CanvasImage.Source = _bitmap;
        CopyToBitmap();

        // Wired here rather than in XAML: PointerPressed and PointerMoved carry
        // different event-arg types, but delegate contravariance lets one handler
        // taking the base PointerEventArgs serve both.
        CanvasImage.PointerPressed += OnCanvasPointer;
        CanvasImage.PointerMoved += OnCanvasPointer;
    }

    private void OnCanvasPointer(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(CanvasImage).Properties.IsLeftButtonPressed)
            return;

        var pos = e.GetPosition(CanvasImage);
        _document.BrushDab((int)pos.X, (int)pos.Y, BrushRadius, new Rgba(40, 90, 220, 255));
        CopyToBitmap();
        CanvasImage.InvalidateVisual();
    }

    // Copy the engine's shared RGBA8 buffer into the WriteableBitmap, row by row
    // to respect the bitmap's own stride (which may be padded).
    private unsafe void CopyToBitmap()
    {
        using var fb = _bitmap.Lock();
        ReadOnlySpan<byte> src = _document.Pixels;
        int srcStride = _document.Stride;
        int dstStride = fb.RowBytes;
        byte* dstBase = (byte*)fb.Address;

        for (int y = 0; y < CanvasHeight; y++)
        {
            var srcRow = src.Slice(y * srcStride, srcStride);
            var dstRow = new Span<byte>(dstBase + (long)y * dstStride, srcStride);
            srcRow.CopyTo(dstRow);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _document.Dispose();
        base.OnClosed(e);
    }
}
