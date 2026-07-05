#ifndef PRIMA_VIEWPORT_H
#define PRIMA_VIEWPORT_H

namespace prima {

// Maps between target (display) space and canvas (document) space.
//
// panX/panY is the canvas-space point that sits at the target's origin (0,0).
// zoom is target pixels per canvas pixel (zoom > 1 magnifies). So:
//
//     targetX = (canvasX - panX) * zoom
//     canvasX =  targetX / zoom  + panX
//
// The managed side (Prima.App.Viewport) mirrors this exact mapping for input
// hit-testing; keep the two in sync.
struct Viewport {
    double panX = 0.0;
    double panY = 0.0;
    double zoom = 1.0;

    double targetToCanvasX(double tx) const { return tx / zoom + panX; }
    double targetToCanvasY(double ty) const { return ty / zoom + panY; }
    double canvasToTargetX(double cx) const { return (cx - panX) * zoom; }
    double canvasToTargetY(double cy) const { return (cy - panY) * zoom; }
};

}  // namespace prima

#endif  // PRIMA_VIEWPORT_H
