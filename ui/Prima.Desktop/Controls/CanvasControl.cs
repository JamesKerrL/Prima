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
/// the cursor), and left-drag painting via the brush engine. Rendering targets a
/// WriteableBitmap sized to physical pixels for crispness on HiDPI; the exposed
/// <see cref="Viewport"/> is in DIP space so pointer input maps directly.
///
/// Stroke rendering uses dirty-rect partial invalidation: only the canvas region
/// modified by brush strokes is re-rendered, not the full viewport. Pan/zoom/resize
/// still trigger a full re-render.
/// </summary>
public sealed class CanvasControl : Control, IDisposable
{
    private readonly Renderer _renderer = Renderer.CreateSoftware();
    private readonly BrushEngine _brushEngine = BrushEngine.Create();
    private Document? _document;
    private WriteableBitmap? _bitmap;
    private PixelSize _bitmapSize;
    private Viewport _viewport = Viewport.Identity;

    private const double MinZoom = 0.05;
    private const double MaxZoom = 64.0;

    private bool _spaceHeld;
    private bool _panning;
    private Point _lastPanPoint;

    private bool _autoFit = true;
    private Viewport _lastRenderViewport = Viewport.Identity;
    private bool _fullRenderPending = true;

    // Brush state
    private bool _stroking;
    private BrushParams _brushParams = BrushParams.Default(new Rgba(40, 90, 220, 255), 12);
    private InputSample[] _sampleBuffer = new InputSample[64];
    private DirtyRect _pendingDirty;

    /// <summary>Fill color for target area outside the canvas.</summary>
    public Rgba Background { get; set; } = new(30, 30, 35, 255);

    /// <summary>Brush radius in canvas pixels (float, subpixel).</summary>
    public float BrushSize
    {
        get => _brushParams.Radius;
        set => _brushParams.Radius = Math.Max(0.3f, value);
    }

    /// <summary>Brush hardness 0..1 (1 = crisp AA edge).</summary>
    public float BrushHardness
    {
        get => _brushParams.Hardness;
        set => _brushParams.Hardness = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Stroke opacity cap 0..1.</summary>
    public float BrushOpacity
    {
        get => _brushParams.Opacity;
        set => _brushParams.Opacity = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Per-dab flow 0..1 (build-up rate).</summary>
    public float BrushFlow
    {
        get => _brushParams.Flow;
        set => _brushParams.Flow = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Brush color.</summary>
    public Rgba BrushColor
    {
        get => new(_brushParams.R, _brushParams.G, _brushParams.B, _brushParams.A);
        set
        {
            _brushParams.R = value.R;
            _brushParams.G = value.G;
            _brushParams.B = value.B;
            _brushParams.A = value.A;
        }
    }

    /// <summary>Currently selected tool.</summary>
    public ToolType CurrentTool { get; set; } = ToolType.Brush;

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
        set { _viewport = value; _fullRenderPending = true; InvalidateVisual(); }
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
        RenderIntoBitmap(scaling, pw, ph);

        context.DrawImage(_bitmap!, new Rect(_bitmap!.Size), new Rect(Bounds.Size));
    }

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
        _fullRenderPending = true;
    }

    private unsafe void RenderIntoBitmap(double scaling, int pw, int ph)
    {
        using var fb = _bitmap!.Lock();
        double zs = _viewport.Zoom * scaling;

        bool vpChanged = _viewport.PanX != _lastRenderViewport.PanX ||
                         _viewport.PanY != _lastRenderViewport.PanY ||
                         _viewport.Zoom != _lastRenderViewport.Zoom;

        // Decide full vs. partial render.
        if (_fullRenderPending || _autoFit || vpChanged || _pendingDirty.IsEmpty)
        {
            // Full render.
            var renderVp = new Viewport(_viewport.PanX, _viewport.PanY, zs);
            int len = fb.RowBytes * fb.Size.Height;
            var span = new Span<byte>((void*)fb.Address, len);
            _renderer.Render(_document!, span, fb.Size.Width, fb.Size.Height,
                             fb.RowBytes, renderVp, Background);
            _fullRenderPending = false;
            _pendingDirty = default;
        }
        else
        {
            // Partial (dirty-rect) render: only the region affected by the last
            // stroke call(s).
            DirtyRect dirty = _pendingDirty;
            _pendingDirty = default;

            // Map canvas dirty rect to physical target rect with 1px pad for AA.
            int tx = (int)Math.Floor((dirty.X - _viewport.PanX) * zs) - 1;
            int ty = (int)Math.Floor((dirty.Y - _viewport.PanY) * zs) - 1;
            int tx2 = (int)Math.Ceiling(
                (dirty.X + dirty.Width - _viewport.PanX) * zs) + 1;
            int ty2 = (int)Math.Ceiling(
                (dirty.Y + dirty.Height - _viewport.PanY) * zs) + 1;

            tx = Math.Clamp(tx, 0, pw);
            ty = Math.Clamp(ty, 0, ph);
            tx2 = Math.Clamp(tx2, 0, pw);
            ty2 = Math.Clamp(ty2, 0, ph);

            int subW = tx2 - tx;
            int subH = ty2 - ty;
            if (subW > 0 && subH > 0)
            {
                byte* subAddr = (byte*)fb.Address + ty * fb.RowBytes + tx * 4;
                var subVp = new Viewport(
                    _viewport.PanX + tx / zs,
                    _viewport.PanY + ty / zs,
                    zs);
                var span = new Span<byte>(subAddr, subH * fb.RowBytes);
                _renderer.Render(_document!, span, subW, subH,
                                 fb.RowBytes, subVp, Background);
            }
        }

        _lastRenderViewport = _viewport;
    }

    private void InvalidateStroke()
    {
        InvalidateVisual();
    }

    private float GetPressure(PointerEventArgs e)
    {
        // Mouse/touch report constant 0.5; only pen gives real pressure.
        return e.Pointer.Type == PointerType.Pen
            ? (float)e.GetCurrentPoint(this).Properties.Pressure
            : 1.0f;
    }

    private InputSample SampleFromPoint(Point posDip, float pressure)
    {
        return new InputSample(
            (float)_viewport.TargetToCanvasX(posDip.X),
            (float)_viewport.TargetToCanvasY(posDip.Y),
            pressure);
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
        else if (props.IsLeftButtonPressed && _document is not null &&
                 CurrentTool == ToolType.Brush)
        {
            e.Pointer.Capture(this);
            _stroking = true;
            _autoFit = false;

            float pressure = GetPressure(e);
            var sample = SampleFromPoint(pos, pressure);

            _brushParams.SizePressureMin = 1f;
            _brushParams.SizePressureGamma = 1f;
            _brushParams.FlowPressureMin = 1f;
            _brushParams.FlowPressureGamma = 1f;

            _brushEngine.BeginStroke(_document, _brushParams);

            _pendingDirty = _pendingDirty.Union(
                _brushEngine.AddSamples(new[] { sample }));

            InvalidateStroke();
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
            _viewport.PanX -= d.X / _viewport.Zoom;
            _viewport.PanY -= d.Y / _viewport.Zoom;
            InvalidateVisual();
        }
        else if (_stroking)
        {
            var points = e.GetIntermediatePoints(this);
            int count = points.Count;

            if (_sampleBuffer.Length < count)
            {
                int newSize = _sampleBuffer.Length;
                while (newSize < count) newSize *= 2;
                _sampleBuffer = new InputSample[newSize];
            }

            float pressure = GetPressure(e);
            for (int i = 0; i < count; i++)
            {
                var pt = points[i];
                _sampleBuffer[i] = SampleFromPoint(pt.Position, pressure);
            }

            _pendingDirty = _pendingDirty.Union(
                _brushEngine.AddSamples(new ReadOnlySpan<InputSample>(
                    _sampleBuffer, 0, count)));

            InvalidateStroke();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning)
        {
            _panning = false;
        }
        else if (_stroking)
        {
            _stroking = false;
            e.Pointer.Capture(null);

            _pendingDirty = _pendingDirty.Union(_brushEngine.EndStroke());
            InvalidateStroke();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _autoFit = false;
        var pos = e.GetPosition(this);
        double factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        double newZoom = Math.Clamp(_viewport.Zoom * factor, MinZoom, MaxZoom);

        double cx = _viewport.TargetToCanvasX(pos.X);
        double cy = _viewport.TargetToCanvasY(pos.Y);
        _viewport.Zoom = newZoom;
        _viewport.PanX = cx - pos.X / newZoom;
        _viewport.PanY = cy - pos.Y / newZoom;

        _fullRenderPending |= Math.Abs(factor - 1.0) > 0.001;
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
        _brushEngine.Dispose();
    }
}
