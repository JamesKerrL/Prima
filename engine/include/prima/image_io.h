#ifndef PRIMA_IMAGE_IO_H
#define PRIMA_IMAGE_IO_H

#include <cstddef>
#include <cstdint>
#include <optional>
#include <vector>

namespace prima {

// RGBA8 pixel data loaded from an image file.
struct ImageData {
    int width;
    int height;
    std::vector<uint8_t> pixels;  // row-major RGBA8, stride == width * 4
};

// Load an image from a file path. Supports PNG, JPEG, BMP, GIF, etc.
// Returns nullopt on failure (bad path, corrupt data, unsupported format).
std::optional<ImageData> loadImage(const char* path);

// Load an image from an in-memory buffer.
std::optional<ImageData> loadImageFromMemory(const uint8_t* data, std::size_t len);

// Save RGBA8 pixels as PNG. Returns true on success.
bool saveImagePng(const char* path, const uint8_t* rgbaPixels,
                  int width, int height);

// Save RGBA8 pixels as JPEG with quality 1–100. Alpha channel is ignored
// (JPEG has no alpha). Returns true on success.
bool saveImageJpeg(const char* path, const uint8_t* rgbaPixels,
                   int width, int height, int quality);

}  // namespace prima

#endif  // PRIMA_IMAGE_IO_H
