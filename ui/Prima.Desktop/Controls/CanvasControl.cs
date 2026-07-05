using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Prima.App;

namespace Prima.Desktop.Controls;

/// <summary>
/// A reusable control that displays a <see cref="Document"/> through the engine's
/// software renderer, with pan (middle-drag or space+drag), zoom (wheel, around
/// the cursor), and left-drag painting. Rendering targets a WriteableBitmap sized
/// to physical pixels for crispness on HiDPI; the exposed <see cref="Viewport"/>
/// is in DIP space so pointer input maps directly.
/// </summary>
public sealed class CanvasControl : Control, IDisposable
{
    private readonly Renderer _renderer = Renderer.CreateSoftware();
    private Document? _document;
    private WriteableBitmap? _bitmap;
    private PixelSize _bitmapSize;
    private Viewport _viewport = Viewport.Identity;

    private const double MinZoom = 0.05;
    private const double MaxZoom = 64.0;

    private bool _spaceHeld;
    private bool _panning;
    private Point _lastPanPoint;

    /// <summary>
    /// While true, the viewport is recomputed each render to fit the whole
    /// document into the control's bounds. Cleared the moment the user pans
    /// or zooms manually, so their choice isn't clobbered by a later resize.
    /// </summary>
    private bool _autoFit = true;

    /// <summary>Fill color for target area outside the canvas.</summary>
    public Rgba Background { get; set; } = new(30, 30, 35, 255);

    /// <summary>Brush radius in canvas pixels.</summary>
    public int BrushRadius { get; set; } = 12;

    /// <summary>Brush color.</summary>
    public Rgba BrushColor { get; set; } = new(40, 90, 220, 255);

    public CanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public Document? Document
    {
        get => _document;
        set
        {
            _document?.Dispose();
            _document = value;
            _autoFit = true;
            InvalidateVisual();
        }
    }

    public Viewport Viewport
    {
        get => _viewport;
        set { _viewport = value; InvalidateVisual(); }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_document is null || Bounds.Width < 1 || Bounds.Height < 1)
            return;

        if (_autoFit)
            FitToWindow();

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int pw = Math.Max(1, (int)Math.Ceiling(Bounds.Width * scaling));
        int ph = Math.Max(1, (int)Math.Ceiling(Bounds.Height * scaling));

        EnsureBitmap(pw, ph);
        RenderIntoBitmap(scaling);

        context.DrawImage(_bitmap!, new Rect(_bitmap!.Size), new Rect(Bounds.Size));
    }

    /// <summary>Scales and centers the viewport so the whole document fits the control's bounds.</summary>
    private void FitToWindow()
    {
        double zoom = Math.Clamp(
            Math.Min(Bounds.Width / _document!.Width, Bounds.Height / _document.Height),
            MinZoom, MaxZoom);

        _viewport = new Viewport(
            _document.Width / 2.0 - Bounds.Width / (2.0 * zoom),
            _document.Height / 2.0 - Bounds.Height / (2.0 * zoom),
            zoom);
    }

    private void EnsureBitmap(int pw, int ph)
    {
        if (_bitmap is not null && _bitmapSize.Width == pw && _bitmapSize.Height == ph)
            return;
        _bitmap?.Dispose();
        _bitmapSize = new PixelSize(pw, ph);
        _bitmap = new WriteableBitmap(
            _bitmapSize, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
    }

    private unsafe void RenderIntoBitmap(double scaling)
    {
        using var fb = _bitmap!.Lock();
        // The bitmap is physical pixels; the viewport is DIP-space, so the render
        // zoom is scaled up by the DPI factor. Pan is in canvas space, unchanged.
        var renderVp = new Viewport(_viewport.PanX, _viewport.PanY, _viewport.Zoom * scaling);
        int len = fb.RowBytes * fb.Size.Height;
        var span = new Span<byte>((void*)fb.Address, len);
        _renderer.Render(_document!, span, fb.Size.Width, fb.Size.Height, fb.RowBytes,
                         renderVp, Background);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        var pos = e.GetPosition(this);

        if (props.IsMiddleButtonPressed || (_spaceHeld && props.IsLeftButtonPressed))
        {
            _autoFit = false;
            _panning = true;
            _lastPanPoint = pos;
        }
        else if (props.IsLeftButtonPressed)
        {
            Paint(pos);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_panning)
        {
            var d = pos - _lastPanPoint;
            _lastPanPoint = pos;
            // Drag content with the cursor: moving right shows content to the left.
            _viewport.PanX -= d.X / _viewport.Zoom;
            _viewport.PanY -= d.Y / _viewport.Zoom;
            InvalidateVisual();
        }
        else if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Paint(pos);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _panning = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _autoFit = false;
        var pos = e.GetPosition(this);
        double factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        double newZoom = Math.Clamp(_viewport.Zoom * factor, MinZoom, MaxZoom);

        // Keep the canvas point under the cursor fixed while zooming.
        double cx = _viewport.TargetToCanvasX(pos.X);
        double cy = _viewport.TargetToCanvasY(pos.Y);
        _viewport.Zoom = newZoom;
        _viewport.PanX = cx - pos.X / newZoom;
        _viewport.PanY = cy - pos.Y / newZoom;

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space) _spaceHeld = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space) _spaceHeld = false;
    }

    private void Paint(Point posDip)
    {
        if (_document is null) return;
        int cx = (int)Math.Floor(_viewport.TargetToCanvasX(posDip.X));
        int cy = (int)Math.Floor(_viewport.TargetToCanvasY(posDip.Y));
        _document.BrushDab(cx, cy, BrushRadius, BrushColor);
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _renderer.Dispose();
    }
}
