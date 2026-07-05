namespace Prima.App;

/// <summary>
/// Maps between target (display) space and canvas (document) space.
/// <para>
/// <see cref="PanX"/>/<see cref="PanY"/> is the canvas-space point at the
/// target's origin (0,0); <see cref="Zoom"/> is target pixels per canvas pixel.
/// This mirrors the engine's <c>prima::Viewport</c> exactly — the two must stay
/// in sync so input hit-testing lines up with what the renderer draws.
/// </para>
/// </summary>
public struct Viewport(double panX, double panY, double zoom)
{
    public double PanX = panX;
    public double PanY = panY;
    public double Zoom = zoom;

    public static Viewport Identity => new(0, 0, 1.0);

    public readonly double TargetToCanvasX(double tx) => tx / Zoom + PanX;
    public readonly double TargetToCanvasY(double ty) => ty / Zoom + PanY;
    public readonly double CanvasToTargetX(double cx) => (cx - PanX) * Zoom;
    public readonly double CanvasToTargetY(double cy) => (cy - PanY) * Zoom;
}
