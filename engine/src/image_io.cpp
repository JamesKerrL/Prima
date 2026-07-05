// Implementation of image load/save using stb_image (public domain).
// The STB_IMAGE_IMPLEMENTATION macro is defined here — this is the only
// translation unit that includes stb_image.h with the implementation.

#define STB_IMAGE_IMPLEMENTATION
#include "stb/stb_image.h"

#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb/stb_image_write.h"

#include "prima/image_io.h"
#include <cstring>

namespace prima {

// Callback for stbi_write_* to write into a std::vector<uint8_t>.
struct MemWriter {
    std::vector<uint8_t> buf;
    static void write(void* context, void* data, int size) {
        auto& self = *static_cast<MemWriter*>(context);
        auto* bytes = static_cast<const uint8_t*>(data);
        self.buf.insert(self.buf.end(), bytes, bytes + size);
    }
};

std::optional<ImageData> loadImage(const char* path) {
    int w, h, channels;
    unsigned char* data = stbi_load(path, &w, &h, &channels, 4);
    if (!data)
        return std::nullopt;
    ImageData result;
    result.width = w;
    result.height = h;
    result.pixels.assign(data, data + static_cast<std::size_t>(w) * h * 4);
    stbi_image_free(data);
    return result;
}

std::optional<ImageData> loadImageFromMemory(const uint8_t* data, std::size_t len) {
    int w, h, channels;
    unsigned char* pixels = stbi_load_from_memory(data, static_cast<int>(len),
                                                  &w, &h, &channels, 4);
    if (!pixels)
        return std::nullopt;
    ImageData result;
    result.width = w;
    result.height = h;
    result.pixels.assign(pixels, pixels + static_cast<std::size_t>(w) * h * 4);
    stbi_image_free(pixels);
    return result;
}

bool saveImagePng(const char* path, const uint8_t* rgbaPixels,
                  int width, int height) {
    int stride = width * 4;
    int ret = stbi_write_png(path, width, height, 4, rgbaPixels, stride);
    return ret != 0;
}

bool saveImageJpeg(const char* path, const uint8_t* rgbaPixels,
                   int width, int height, int quality) {
    int ret = stbi_write_jpg(path, width, height, 4, rgbaPixels, quality);
    return ret != 0;
}

}  // namespace prima
