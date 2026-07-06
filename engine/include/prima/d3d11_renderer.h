#ifndef PRIMA_D3D11_RENDERER_H
#define PRIMA_D3D11_RENDERER_H

#include <memory>

#include "prima/renderer.h"

namespace prima {

// Which D3D11 driver to request. Auto tries hardware first, then falls back to
// WARP (the in-box software rasterizer, which needs no GPU or display — this is
// what keeps the backend headless-testable). Warp forces the software
// rasterizer directly, so tests can exercise the headless path explicitly.
enum class D3d11Driver { Auto, Hardware, Warp };

// Create the Direct3D 11 render backend. Returns nullptr when the requested
// device cannot be created (no D3D11 runtime, driver failure). Never throws.
// Only compiled when PRIMA_HAS_D3D11 is defined (Windows builds).
std::unique_ptr<Renderer> createD3d11Renderer(D3d11Driver driver = D3d11Driver::Auto);

}  // namespace prima

#endif  // PRIMA_D3D11_RENDERER_H
