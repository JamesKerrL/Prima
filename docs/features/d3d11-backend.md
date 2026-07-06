# Direct3D 11 render backend

GPU backend #1 (decision: D3D11 first, superseding the OpenGL-first plan;
Vulkan/Metal come later behind the same `Renderer` seam for other platforms).

## Shipped (plumbing milestone)

- `createD3d11Renderer(D3d11Driver)` — hardware device with WARP fallback
  (WARP needs no GPU/display, keeping the backend headless-testable); nullptr
  when D3D11 is unavailable. Public header exposes only the factory + driver
  enum; all `d3d11.h`/WRL usage is private to `engine/src/d3d11_renderer.cpp`.
- ABI: `prima_renderer_create_d3d11` (always exported, NULL when compiled out)
  and `prima_renderer_name`. App layer: `Renderer.CreateD3D11()` (nullable),
  `Renderer.CreateBest()` (D3D11 ?? software), `Renderer.Name`.
- Build: `PRIMA_ENABLE_D3D11` CMake option, ON for WIN32; links system
  `d3d11 dxgi dxguid` import libs (MinGW-supplied).
- Tests: engine GoogleTest (WARP creation hard-asserted, software-parity
  byte-compares, target-size-change path) + app xUnit (create-or-null,
  `CreateBest`, render parity).

### Stub render semantics (and why)

`render()` composes on the CPU via the shared sampling kernel
(`engine/src/cpu_sampling.h`, byte-identical to `SoftwareRenderer`), then
round-trips the result through the GPU: `UpdateSubresource` → `CopyResource`
to a staging texture → `Map`/readback (row-by-row; `RowPitch` may be padded).
The round-trip is a lossless `R8G8B8A8_UNORM` copy, so parity tests are exact,
while the device/texture/readback machinery the real GPU path needs is
genuinely exercised. On any HRESULT failure mid-render it degrades to copying
the scratch buffer straight to the target (output stays correct; `render` has
no error channel by design).

`CanvasControl` intentionally stays on `CreateSoftware()`: the identity
round-trip is strictly slower than software on the stroke hot path.

## Follow-up (this file's remaining scope)

- [ ] GPU compositing: viewport sampling in a shader (canvas as a GPU texture,
      dab-dirty-rect uploads), replacing the CPU compose + readback
- [ ] Zero-copy present: Avalonia composition/swapchain texture interop
      instead of readback into the `WriteableBitmap`
- [ ] Switch `CanvasControl` to `Renderer.CreateBest()` once GPU compositing
      makes it a win; keep software fallback path tested
- [ ] Backend preference / diagnostics surface (which backend is active)

Delete this file when the ROADMAP "D3D11 GPU compositing" box is checked.
