#include "prima/brush.h"

#include <algorithm>
#include <cmath>
#include <cstring>

#include "prima/brush_math.h"

namespace prima {

RectI RoundDabSource::stamp(float cx, float cy, float radius, float hardness,
                            float flow, CoverageCell* coverage, int canvasWidth,
                            int canvasHeight) {
    if (radius <= 0.f) return RectI{};

    // Bounding box of the dab, rounded outward to integer pixel bounds. Must
    // reach the same sqrt(2)/2 AA extent dabCoverage uses, or the outermost
    // fringe pixels get cropped.
    float outer = radius + 0.70710678f;
    int minX = static_cast<int>(std::floor(cx - outer));
    int minY = static_cast<int>(std::floor(cy - outer));
    int maxX = static_cast<int>(std::ceil(cx + outer));   // exclusive
    int maxY = static_cast<int>(std::ceil(cy + outer));   // exclusive

    RectI bbox{minX, minY, maxX - minX, maxY - minY};
    RectI clamped = bbox.intersect(canvasWidth, canvasHeight);
    if (clamped.empty()) return RectI{};

    int x0 = clamped.x;
    int y0 = clamped.y;
    int x1 = clamped.x + clamped.width;   // exclusive
    int y1 = clamped.y + clamped.height;  // exclusive

    // Track actual touched pixel bounds so the returned dirty rect is exact.
    int touchMinX = x1, touchMinY = y1, touchMaxX = x0, touchMaxY = y0;

    for (int py = y0; py < y1; ++py) {
        float dy = (static_cast<float>(py) + 0.5f) - cy;
        int rowBase = py * canvasWidth;
        for (int px = x0; px < x1; ++px) {
            float dx = (static_cast<float>(px) + 0.5f) - cx;
            float d = std::sqrt(dx * dx + dy * dy);
            float cov = dabCoverage(d, radius, hardness);
            if (cov > 0.f) {
                CoverageCell& cell = coverage[rowBase + px];
                cell.shape = unionCoverage(cell.shape, cov);
                cell.buildup = accumulateCoverage(cell.buildup, flow, cov);
                if (px < touchMinX) touchMinX = px;
                if (py < touchMinY) touchMinY = py;
                if (px > touchMaxX) touchMaxX = px;
                if (py > touchMaxY) touchMaxY = py;
            }
        }
    }

    if (touchMaxX < touchMinX || touchMaxY < touchMinY) return RectI{};
    return RectI{touchMinX, touchMinY,
                 touchMaxX - touchMinX + 1, touchMaxY - touchMinY + 1};
}

BrushEngine::BrushEngine() : dabSource_(std::make_unique<RoundDabSource>()) {}

void BrushEngine::beginStroke(Canvas& canvas, const BrushParams& params) {
    if (canvas_ != nullptr) {
        endStroke();  // implicitly finalize the previous stroke; discard rect
    }

    const std::size_t byteSize = canvas.byteSize();
    const std::size_t pixelCount =
        static_cast<std::size_t>(canvas.width()) * static_cast<std::size_t>(canvas.height());

    // Lazily size scratch buffers, reusing across the document's lifetime — only
    // beginStroke may resize, never the addSamples hot path.
    if (baseline_.size() != byteSize) {
        baseline_.resize(byteSize);
    }
    if (coverage_.size() != pixelCount) {
        coverage_.resize(pixelCount);
    }

    // Snapshot the canvas — the frozen "before" image every dab composites
    // against (prevents self-overlap double-darkening).
    std::memcpy(baseline_.data(), canvas.pixels(), byteSize);
    baselineWidth_ = canvas.width();
    baselineHeight_ = canvas.height();

    // Zero the coverage buffer only over the previous stroke's dirty rect (the
    // buffer starts zero-initialized, so the first-ever stroke clears nothing).
    if (!strokeDirty_.empty()) {
        int width = canvas.width();
        int x0 = strokeDirty_.x;
        int y0 = strokeDirty_.y;
        int x1 = strokeDirty_.x + strokeDirty_.width;
        int y1 = strokeDirty_.y + strokeDirty_.height;
        for (int py = y0; py < y1; ++py) {
            std::memset(coverage_.data() + (static_cast<std::size_t>(py) * width + x0), 0,
                        static_cast<std::size_t>(strokeDirty_.width) * sizeof(CoverageCell));
        }
    }

    strokeDirty_ = RectI{};
    canvas_ = &canvas;
    params_ = params;
    emitter_.begin(params);
}

RectI BrushEngine::addSamples(const InputSample* samples, int count) {
    if (canvas_ == nullptr) return RectI{};

    RectI callDirty{};
    emitter_.addSamples(samples, count, [&](const DabPoint& dab) {
        RectI r = stampDab(dab);
        callDirty.unionWith(r);
        strokeDirty_.unionWith(r);
    });
    return callDirty;
}

RectI BrushEngine::endStroke() {
    if (canvas_ == nullptr) return RectI{};

    RectI callDirty{};
    // Process the emitter's final dab(s) while canvas_ is still valid — stampDab
    // and recomposite depend on it. Only clear canvas_ AFTER end() returns.
    emitter_.end([&](const DabPoint& dab) {
        RectI r = stampDab(dab);
        callDirty.unionWith(r);
        strokeDirty_.unionWith(r);
    });

    canvas_ = nullptr;
    return callDirty;
}

bool BrushEngine::strokeActive() const { return canvas_ != nullptr; }

RectI BrushEngine::stampDab(const DabPoint& dab) {
    RectI r = dabSource_->stamp(dab.x, dab.y, dab.radius, params_.hardness,
                                dab.flow, coverage_.data(), canvas_->width(),
                                canvas_->height());
    if (r.empty()) return r;
    recomposite(r);
    return r;
}

void BrushEngine::recomposite(const RectI& r) {
    const int width = canvas_->width();
    uint8_t* dst = canvas_->pixels();
    const uint8_t* base = baseline_.data();

    const float opacity = params_.opacity;
    const float colorAlpha = params_.color.a / 255.f;
    const Rgba color = params_.color;

    int x0 = r.x;
    int y0 = r.y;
    int x1 = r.x + r.width;
    int y1 = r.y + r.height;

    for (int py = y0; py < y1; ++py) {
        int rowBase = py * width;
        for (int px = x0; px < x1; ++px) {
            int idx = rowBase + px;
            const CoverageCell& cell = coverage_[idx];
            float covNorm =
                resolveStrokeCoverage(cell.shape, cell.buildup) / 65535.f;
            float sa = covNorm * opacity * colorAlpha;

            int b = idx * 4;
            Rgba baselinePixel{base[b], base[b + 1], base[b + 2], base[b + 3]};
            Rgba out = blendSourceOver(baselinePixel, color, sa);

            dst[b] = out.r;
            dst[b + 1] = out.g;
            dst[b + 2] = out.b;
            dst[b + 3] = out.a;
        }
    }
}

bool BrushEngine::readBaselineRegion(int x, int y, int w, int h,
                                     uint8_t* dst) const {
    if (baseline_.empty() || w <= 0 || h <= 0 || dst == nullptr) return false;
    if (x < 0 || y < 0 || x + w > baselineWidth_ || y + h > baselineHeight_) {
        return false;
    }

    const uint8_t* src = baseline_.data();
    const std::size_t rowBytes = static_cast<std::size_t>(w) * 4;
    for (int row = 0; row < h; ++row) {
        const std::size_t srcOffset =
            (static_cast<std::size_t>(y + row) * baselineWidth_ + x) * 4;
        std::memcpy(dst + static_cast<std::size_t>(row) * rowBytes,
                    src + srcOffset, rowBytes);
    }
    return true;
}

}  // namespace prima
