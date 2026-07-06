#include "prima/d3d11_renderer.h"

#include <cstring>
#include <new>
#include <vector>

#include <d3d11.h>
#include <wrl/client.h>

#include "cpu_sampling.h"

using Microsoft::WRL::ComPtr;

namespace prima {
namespace {

// RAII bundle for the D3D11 device + immediate context.
struct D3d11Device {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    D3D_FEATURE_LEVEL featureLevel = D3D_FEATURE_LEVEL_9_1;
    bool isWarp = false;

    static bool createWithDriver(D3D_DRIVER_TYPE driverType, D3d11Device& out) {
        // No D3D11_CREATE_DEVICE_DEBUG: the debug layer needs the Windows SDK
        // d3d11 SDK layers, which are absent under a plain MinGW toolchain.
        // Default feature-level list (nullptr): the stub only needs copy ops,
        // so even a 10-level WARP device is fine.
        const HRESULT hr = D3D11CreateDevice(
            nullptr, driverType, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION,
            out.device.ReleaseAndGetAddressOf(), &out.featureLevel,
            out.context.ReleaseAndGetAddressOf());
        return SUCCEEDED(hr) && out.device && out.context;
    }

    static bool create(D3d11Driver driver, D3d11Device& out) {
        if (driver != D3d11Driver::Warp &&
            createWithDriver(D3D_DRIVER_TYPE_HARDWARE, out)) {
            out.isWarp = false;
            return true;
        }
        if (driver != D3d11Driver::Hardware &&
            createWithDriver(D3D_DRIVER_TYPE_WARP, out)) {
            out.isWarp = true;
            return true;
        }
        return false;
    }
};

// Direct3D 11 backend, plumbing milestone: composes on the CPU (same shared
// sampling as SoftwareRenderer, so output is byte-identical), then round-trips
// the result through the GPU — upload to a DEFAULT texture, CopyResource to a
// STAGING texture, Map/readback into the caller's target. The round-trip is a
// lossless R8G8B8A8_UNORM copy; it exists to exercise the device/texture/
// readback machinery the real GPU compositing follow-up needs. If any GPU call
// fails mid-render, the scratch buffer is copied straight to the target so
// output stays correct (render() has no error channel by design).
class D3D11Renderer : public Renderer {
public:
    explicit D3D11Renderer(D3d11Device device) : device_(std::move(device)) {}

    void render(const Canvas& canvas, const RenderTarget& target,
                const Viewport& viewport, Rgba background) override {
        const int w = target.width;
        const int h = target.height;
        if (w <= 0 || h <= 0) return;

        // CPU compose into pooled scratch (tight stride, w*4).
        const int scratchStride = w * Canvas::kBytesPerPixel;
        scratch_.resize(static_cast<std::size_t>(scratchStride) * h);
        RenderTarget scratchTarget{scratch_.data(), w, h, scratchStride};
        sampleCanvasToTarget(canvas, scratchTarget, viewport, background);

        if (!roundTripThroughGpu(target, scratchStride)) {
            copyScratchToTarget(target, scratchStride);
        }
    }

    const char* name() const override { return "d3d11"; }

private:
    bool ensureTextures(int w, int h) {
        if (gpuTexture_ && texWidth_ == w && texHeight_ == h) return true;
        gpuTexture_.Reset();
        stagingTexture_.Reset();

        D3D11_TEXTURE2D_DESC desc = {};
        desc.Width = static_cast<UINT>(w);
        desc.Height = static_cast<UINT>(h);
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;

        if (FAILED(device_.device->CreateTexture2D(
                &desc, nullptr, gpuTexture_.ReleaseAndGetAddressOf())))
            return false;

        desc.Usage = D3D11_USAGE_STAGING;
        desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

        if (FAILED(device_.device->CreateTexture2D(
                &desc, nullptr, stagingTexture_.ReleaseAndGetAddressOf()))) {
            gpuTexture_.Reset();
            return false;
        }

        texWidth_ = w;
        texHeight_ = h;
        return true;
    }

    bool roundTripThroughGpu(const RenderTarget& target, int scratchStride) {
        if (!ensureTextures(target.width, target.height)) return false;

        device_.context->UpdateSubresource(gpuTexture_.Get(), 0, nullptr,
                                           scratch_.data(),
                                           static_cast<UINT>(scratchStride), 0);
        device_.context->CopyResource(stagingTexture_.Get(), gpuTexture_.Get());

        D3D11_MAPPED_SUBRESOURCE mapped = {};
        if (FAILED(device_.context->Map(stagingTexture_.Get(), 0,
                                        D3D11_MAP_READ, 0, &mapped)))
            return false;

        // Row-by-row: mapped.RowPitch may be padded past w*4, and
        // target.stride is caller-controlled.
        const auto* src = static_cast<const uint8_t*>(mapped.pData);
        const std::size_t rowBytes =
            static_cast<std::size_t>(target.width) * Canvas::kBytesPerPixel;
        for (int y = 0; y < target.height; ++y) {
            std::memcpy(target.pixels + static_cast<std::size_t>(y) * target.stride,
                        src + static_cast<std::size_t>(y) * mapped.RowPitch,
                        rowBytes);
        }
        device_.context->Unmap(stagingTexture_.Get(), 0);
        return true;
    }

    void copyScratchToTarget(const RenderTarget& target, int scratchStride) {
        const std::size_t rowBytes =
            static_cast<std::size_t>(target.width) * Canvas::kBytesPerPixel;
        for (int y = 0; y < target.height; ++y) {
            std::memcpy(target.pixels + static_cast<std::size_t>(y) * target.stride,
                        scratch_.data() + static_cast<std::size_t>(y) * scratchStride,
                        rowBytes);
        }
    }

    D3d11Device device_;
    std::vector<uint8_t> scratch_;
    ComPtr<ID3D11Texture2D> gpuTexture_;
    ComPtr<ID3D11Texture2D> stagingTexture_;
    int texWidth_ = 0;
    int texHeight_ = 0;
};

}  // namespace

std::unique_ptr<Renderer> createD3d11Renderer(D3d11Driver driver) {
    D3d11Device device;
    if (!D3d11Device::create(driver, device)) return nullptr;
    return std::unique_ptr<Renderer>(new (std::nothrow)
                                         D3D11Renderer(std::move(device)));
}

}  // namespace prima
