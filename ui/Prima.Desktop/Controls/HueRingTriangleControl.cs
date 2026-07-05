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
/// The interactive heart of the color picker: a hue ring wrapping a
/// saturation/value triangle. Owns a native <see cref="ColorWheel"/> and blits
/// its two RGBA8 bitmaps (zero-copy, exactly like <see cref="CanvasControl"/>
/// does with the canvas), draws drag handles for the current hue and (s,v), and
/// converts pointer drags into color changes.
///
/// <para>The coordinate math here is a bit-for-bit mirror of the engine so the
/// handles land exactly on the colors the engine rendered (see the engine's
/// <c>color_wheel.h</c>/<c>color.cpp</c> doc comments):</para>
/// <list type="bullet">
///   <item>Ring: hue = atan2(-dy, dx) in radians, converted to degrees and
///     normalized to [0,360). dy is buffer-down-positive, so the Y is negated
///     - matching the engine's <c>HueFromAngle(atan2(-sy, sx))</c>.</item>
///   <item>Triangle: pixel-space maps to normalized Cartesian via
///     nx=(px+0.5-halfW)/r, ny=(halfH-(py+0.5))/r (r = TriangleWidth/2), then
///     (s,v) is recovered by the same barycentric-clamp as the engine's
///     <c>SvFromTrianglePoint</c>. Vertices: hue=(0,1),
///     black=(-sqrt(3)/2,-0.5), white=(sqrt(3)/2,-0.5).</item>
/// </list>
///
/// <para>Public surface for integration (consumed by <c>ColorPickerControl</c>):
/// set <see cref="Color"/> from outside (e.g. a typed hex value) to move the
/// handles and repaint without re-raising <see cref="ColorChanged"/>; the event
/// fires only for genuine pointer interaction.</para>
/// </summary>
public sealed class HueRingTriangleControl : Control, IDisposable
{
    // Ring thickness as a fraction of the outer size. 13% sits in the middle of
    // the 12-15% design band: thick enough to grab comfortably, thin enough to
    // leave a usable triangle inside. Constant so the geometry is stable across
    // resizes (only the absolute pixel size changes).
    private const double RingThicknessFraction = 0.13;

    // Extra grab tolerance (DIP) around the ring annulus so the user doesn't
    // have to hit it pixel-perfectly. Applied on both edges of the annulus.
    private const double RingHitTolerance = 10.0;

    // Handle geometry (DIP). The ring handle is a ring outline sitting on the
    // annulus midline; the triangle handle is a small ring over the (s,v) point.
    private const double HandleRadius = 6.0;
    private const double HandleStrokeWidth = 2.0;

    // Triangle vertices in normalized Cartesian (must match color.cpp exactly).
    private const double Sqrt3Over2 = 0.8660254037844386;
    private static readonly (double X, double Y) VHue = (0.0, 1.0);
    private static readonly (double X, double Y) VBlack = (-Sqrt3Over2, -0.5);
    private static readonly (double X, double Y) VWhite = (Sqrt3Over2, -0.5);

    private ColorWheel? _wheel;
    private int _wheelOuterPx;          // native ring bitmap edge length (px)

    private WriteableBitmap? _ringBitmap;
    private WriteableBitmap? _triangleBitmap;
    private double _ringBitmapHue = double.NaN; // hue the triangle bitmap holds

    // Current color in HSV. Hue drives the ring; (s,v) drive the triangle
    // handle. Stored as HSV (not RGBA) because the picker is an HSV picker:
    // round-tripping through RGBA would lose hue/saturation at the greys/edges
    // (e.g. any black collapses to H=0,S=0), which would make the handles jump.
    private Hsv _color = new(0.0, 1.0, 1.0);

    // Which target the current drag grabbed. A drag stays locked to whatever it
    // grabbed on press until release, so the pointer wandering into the other
    // region mid-drag never flips control (avoids ring/triangle flicker).
    private enum DragTarget { None, Ring, Triangle }
    private DragTarget _drag = DragTarget.None;

    /// <summary>
    /// Raised only when a pointer interaction changes the color. Programmatic
    /// sets of <see cref="Color"/> do not raise it (avoids feedback loops when
    /// the parent pushes a value in, e.g. from a hex field).
    /// </summary>
    public event EventHandler<Rgba>? ColorChanged;

    /// <summary>Raised when a pointer drag begins (before the first color
    /// update), so a parent can snapshot the pre-edit color for a before/after
    /// preview.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised when a pointer drag ends, so a parent can commit the
    /// final color (e.g. to a recent-color history) once per gesture rather
    /// than on every intermediate move.</summary>
    public event EventHandler? DragEnded;

    /// <summary>
    /// The current color as HSV. Setting this from outside moves the handles and
    /// repaints (regenerating the triangle bitmap only if the hue changed)
    /// without raising <see cref="ColorChanged"/>. HSV is used rather than RGBA
    /// so hue/saturation survive at greys and pure black/white.
    /// </summary>
    public Hsv Color
    {
        get => _color;
        set
        {
            if (_color.Equals(value)) return;
            _color = value;
            InvalidateVisual();
        }
    }

    /// <summary>The current color as RGBA (derived from <see cref="Color"/>).</summary>
    public Rgba SelectedColor => _color.ToRgba();

    public HueRingTriangleControl()
    {
        ClipToBounds = true;
    }

    // ---- Layout / geometry ---------------------------------------------------

    // Everything the render and hit-testing paths need about where the wheel
    // currently sits in DIP space. Recomputed from Bounds each time so it always
    // reflects the live layout.
    private readonly struct Geometry
    {
        public readonly double CenterX, CenterY; // control-space center (DIP)
        public readonly double OuterRadius;       // ring outer radius (DIP)
        public readonly double InnerRadius;       // ring inner radius = triangle circumradius (DIP)

        public Geometry(double cx, double cy, double outer, double inner)
        {
            CenterX = cx; CenterY = cy; OuterRadius = outer; InnerRadius = inner;
        }

        public double RingThickness => OuterRadius - InnerRadius;
        public double MidlineRadius => (OuterRadius + InnerRadius) / 2.0;
    }

    private Geometry GetGeometry()
    {
        double side = Math.Min(Bounds.Width, Bounds.Height);
        double outer = side / 2.0;
        double thickness = outer * RingThicknessFraction;
        double inner = outer - thickness;
        return new Geometry(Bounds.Width / 2.0, Bounds.Height / 2.0, outer, inner);
    }

    // ---- Rendering -----------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 2 || Bounds.Height < 2) return;

        var g = GetGeometry();
        if (g.OuterRadius < 1) return;

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int outerPx = Math.Max(2, (int)Math.Round(2.0 * g.OuterRadius * scaling));

        EnsureWheel(outerPx);
        if (_wheel is null) return;

        EnsureTriangleBitmapHue();
        BlitRing();
        BlitTriangle();

        // Ring bitmap fills the outer circle's bounding square, centered.
        var ringRect = new Rect(
            g.CenterX - g.OuterRadius, g.CenterY - g.OuterRadius,
            2.0 * g.OuterRadius, 2.0 * g.OuterRadius);
        if (_ringBitmap is not null)
            context.DrawImage(_ringBitmap, new Rect(_ringBitmap.Size), ringRect);

        // Triangle bitmap is (2*innerRadius)px square, centered concentrically
        // inside the ring's inner circle. Its half-size in DIP equals the ring's
        // inner radius, so it lines up with the annulus exactly.
        if (_triangleBitmap is not null)
        {
            var triRect = new Rect(
                g.CenterX - g.InnerRadius, g.CenterY - g.InnerRadius,
                2.0 * g.InnerRadius, 2.0 * g.InnerRadius);
            context.DrawImage(_triangleBitmap, new Rect(_triangleBitmap.Size), triRect);
        }

        DrawRingHandle(context, g);
        DrawTriangleHandle(context, g);
    }

    // Recreate the native wheel only when the pixel size actually changes
    // (mirrors CanvasControl.EnsureBitmap). The ColorWheel is a native resource;
    // regenerating it every frame would thrash allocations and the ring fill.
    private void EnsureWheel(int outerPx)
    {
        if (_wheel is not null && _wheelOuterPx == outerPx) return;

        _wheel?.Dispose();
        _ringBitmap?.Dispose(); _ringBitmap = null;
        _triangleBitmap?.Dispose(); _triangleBitmap = null;
        _ringBitmapHue = double.NaN;

        int thickness = Math.Max(1, (int)Math.Round(outerPx * RingThicknessFraction));
        _wheel = new ColorWheel(outerPx, thickness);
        _wheelOuterPx = outerPx;
    }

    // Regenerate the native triangle bitmap only when the hue it holds differs
    // from the current hue. (s,v)-only changes don't touch the triangle colors,
    // so a triangle-only drag never triggers native regeneration.
    private void EnsureTriangleBitmapHue()
    {
        if (_wheel is null) return;
        if (!double.IsNaN(_ringBitmapHue) && _ringBitmapHue == _color.H) return;
        _wheel.SetHue(_color.H);
        _ringBitmapHue = _color.H;
    }

    private unsafe void BlitRing()
    {
        if (_wheel is null) return;
        int w = _wheel.RingWidth, h = _wheel.RingHeight;
        if (w <= 0 || h <= 0) return;

        if (_ringBitmap is null ||
            _ringBitmap.PixelSize.Width != w || _ringBitmap.PixelSize.Height != h)
        {
            _ringBitmap?.Dispose();
            _ringBitmap = new WriteableBitmap(
                new PixelSize(w, h), new Vector(96, 96),
                PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        }
        CopyInto(_ringBitmap, _wheel.RingPixels, w, h);
    }

    private unsafe void BlitTriangle()
    {
        if (_wheel is null) return;
        int w = _wheel.TriangleWidth, h = _wheel.TriangleHeight;
        if (w <= 0 || h <= 0) return;

        if (_triangleBitmap is null ||
            _triangleBitmap.PixelSize.Width != w || _triangleBitmap.PixelSize.Height != h)
        {
            _triangleBitmap?.Dispose();
            _triangleBitmap = new WriteableBitmap(
                new PixelSize(w, h), new Vector(96, 96),
                PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        }
        CopyInto(_triangleBitmap, _wheel.TrianglePixels, w, h);
    }

    // Copies a tightly-packed RGBA8 source span into the bitmap, honoring the
    // bitmap's row stride (the engine buffers have no row padding; the bitmap
    // might). Same lock+Span pattern as CanvasControl.
    private static unsafe void CopyInto(WriteableBitmap bitmap, ReadOnlySpan<byte> src, int w, int h)
    {
        using var fb = bitmap.Lock();
        int srcStride = w * 4;
        var dst = new Span<byte>((void*)fb.Address, fb.RowBytes * fb.Size.Height);
        if (fb.RowBytes == srcStride)
        {
            src.Slice(0, srcStride * h).CopyTo(dst);
        }
        else
        {
            for (int y = 0; y < h; y++)
                src.Slice(y * srcStride, srcStride).CopyTo(dst.Slice(y * fb.RowBytes, srcStride));
        }
    }

    private void DrawRingHandle(DrawingContext context, Geometry g)
    {
        // Place the handle at the current hue's angle on the annulus midline.
        // Inverse of the pointer->hue map: screen angle = -hueRadians (because
        // hue increases counter-clockwise but screen Y grows down). So:
        //   dx =  cos(hueRad) * r,  dy = -sin(hueRad) * r.
        double hueRad = _color.H * (Math.PI / 180.0);
        double r = g.MidlineRadius;
        double x = g.CenterX + Math.Cos(hueRad) * r;
        double y = g.CenterY - Math.Sin(hueRad) * r;
        DrawHandle(context, x, y);
    }

    private void DrawTriangleHandle(DrawingContext context, Geometry g)
    {
        // (s,v) -> normalized Cartesian (same barycentric blend as the engine's
        // TrianglePointFromSv), then to control-space DIP. +ny is up, so screen
        // Y is negated. Scale is the ring inner radius (triangle circumradius).
        var (nx, ny) = TrianglePointFromSv(_color.S, _color.V);
        double x = g.CenterX + nx * g.InnerRadius;
        double y = g.CenterY - ny * g.InnerRadius;
        DrawHandle(context, x, y);
    }

    // A white ring with a dark halo underneath, so it stays visible over any
    // hue/value it sits on. Stroke color comes from the theme token.
    private void DrawHandle(DrawingContext context, double x, double y)
    {
        var stroke = ResolveHandleStroke();
        var center = new Point(x, y);
        var halo = new Pen(new SolidColorBrush(Colors.Black, 0.55), HandleStrokeWidth + 2.0);
        var ring = new Pen(stroke, HandleStrokeWidth);
        context.DrawEllipse(null, halo, center, HandleRadius, HandleRadius);
        context.DrawEllipse(null, ring, center, HandleRadius, HandleRadius);
    }

    private IBrush ResolveHandleStroke()
    {
        if (this.TryFindResource("PrimaHandleStrokeBrush", out object? res) && res is IBrush b)
            return b;
        return Brushes.White; // fallback if theme isn't loaded
    }

    // ---- Pointer interaction -------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(this);
        var g = GetGeometry();
        if (g.OuterRadius < 1) return;

        double dx = pos.X - g.CenterX;
        double dy = pos.Y - g.CenterY;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // Decide once, on press, which target this drag owns and stick with it
        // until release. Ring wins ties within its (tolerance-padded) annulus;
        // anything inside the inner circle is a triangle drag.
        bool inRing = dist >= g.InnerRadius - RingHitTolerance &&
                      dist <= g.OuterRadius + RingHitTolerance;
        if (inRing)
        {
            _drag = DragTarget.Ring;
            DragStarted?.Invoke(this, EventArgs.Empty);
            UpdateHueFromPointer(dx, dy);
        }
        else if (dist < g.InnerRadius)
        {
            _drag = DragTarget.Triangle;
            DragStarted?.Invoke(this, EventArgs.Empty);
            UpdateSvFromPointer(pos, g);
        }
        else
        {
            _drag = DragTarget.None;
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == DragTarget.None) return;

        var pos = e.GetPosition(this);
        var g = GetGeometry();
        if (g.OuterRadius < 1) return;

        if (_drag == DragTarget.Ring)
            UpdateHueFromPointer(pos.X - g.CenterX, pos.Y - g.CenterY);
        else
            UpdateSvFromPointer(pos, g); // clamps back into the triangle if outside
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag != DragTarget.None)
        {
            _drag = DragTarget.None;
            e.Pointer.Capture(null);
            DragEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    // dx/dy are pointer offsets from the wheel center in DIP, dy positive-down -
    // the same orientation as the engine's buffer coordinates, so the formula is
    // identical: hue = normalize(degrees(atan2(-dy, dx))).
    private void UpdateHueFromPointer(double dx, double dy)
    {
        double radians = Math.Atan2(-dy, dx);
        double hue = radians * (180.0 / Math.PI);
        hue %= 360.0;
        if (hue < 0.0) hue += 360.0;

        if (hue == _color.H) return;
        _color = _color with { H = hue };
        InvalidateVisual();
        RaiseColorChanged();
    }

    private void UpdateSvFromPointer(Point pos, Geometry g)
    {
        // Control-space DIP -> normalized Cartesian, using the exact triangle
        // mapping from the engine. The scale is the triangle's circumradius
        // (= ring inner radius = TriangleWidth/2 in the native buffer). Center
        // offset uses the wheel center directly, which is where the concentric
        // triangle bitmap's center lands.
        double r = g.InnerRadius;
        if (r <= 0) return;
        double nx = (pos.X - g.CenterX) / r;
        double ny = (g.CenterY - pos.Y) / r; // +ny is up

        var (s, v) = SvFromTrianglePoint(nx, ny); // clamps outside points in

        if (s == _color.S && v == _color.V) return;
        _color = _color with { S = s, V = v };
        InvalidateVisual();
        RaiseColorChanged();
    }

    private void RaiseColorChanged() => ColorChanged?.Invoke(this, _color.ToRgba());

    // ---- Pure geometry (mirrors engine/src/color.cpp exactly) ----------------

    // TrianglePointFromSv: (s,v) -> normalized Cartesian. Weights:
    //   a = s*v (hue vertex), b = 1-v (black vertex), c = (1-s)*v (white vertex).
    private static (double X, double Y) TrianglePointFromSv(double s, double v)
    {
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);
        double a = s * v;
        double b = 1.0 - v;
        double c = (1.0 - s) * v;
        return (a * VHue.X + b * VBlack.X + c * VWhite.X,
                a * VHue.Y + b * VBlack.Y + c * VWhite.Y);
    }

    // SvFromTrianglePoint: normalized Cartesian -> (s,v). Barycentric w.r.t.
    // (Hue, Black, White), negative weights zeroed then renormalized (clamps
    // outside points into the triangle), v = a+c, s = a/v. Line-for-line the
    // same as the engine so a drag out-of-bounds clamps identically.
    private static (double S, double V) SvFromTrianglePoint(double x, double y)
    {
        double ax = VHue.X, ay = VHue.Y;
        double bx = VBlack.X, by = VBlack.Y;
        double cx = VWhite.X, cy = VWhite.Y;

        double denom = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
        double a = ((by - cy) * (x - cx) + (cx - bx) * (y - cy)) / denom;
        double b = ((cy - ay) * (x - cx) + (ax - cx) * (y - cy)) / denom;
        double c = 1.0 - a - b;

        a = Math.Max(0.0, a);
        b = Math.Max(0.0, b);
        c = Math.Max(0.0, c);
        double sum = a + b + c;
        if (sum > 0.0)
        {
            a /= sum; b /= sum; c /= sum;
        }
        else
        {
            a = 0.0; b = 1.0; c = 0.0;
        }

        double v = a + c;
        double s = v > 0.0 ? a / v : 0.0;
        return (s, v);
    }

    // ---- Lifetime ------------------------------------------------------------

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }

    public void Dispose()
    {
        _ringBitmap?.Dispose(); _ringBitmap = null;
        _triangleBitmap?.Dispose(); _triangleBitmap = null;
        _wheel?.Dispose(); _wheel = null;
        _wheelOuterPx = 0;
        _ringBitmapHue = double.NaN;
    }
}
