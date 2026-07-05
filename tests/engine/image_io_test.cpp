#include <gtest/gtest.h>
#include "prima/image_io.h"
#include <cstdio>
#include <fstream>
#include <vector>

using prima::ImageData;
using prima::loadImage;
using prima::loadImageFromMemory;
using prima::saveImagePng;
using prima::saveImageJpeg;

namespace {

std::string tempPath(const char* suffix) {
    const char* tmp = std::getenv("TEMP");
    if (!tmp) tmp = "/tmp";
    static int counter = 0;
    char buf[512];
    std::snprintf(buf, sizeof(buf), "%s/prima_test_%d_%s", tmp, counter++, suffix);
    return buf;
}

bool fileExists(const std::string& path) {
    std::ifstream f(path, std::ios::binary | std::ios::ate);
    return f.good() && f.tellg() > 0;
}

// Read an entire file into a byte vector.
std::vector<uint8_t> readFile(const std::string& path) {
    std::ifstream f(path, std::ios::binary);
    if (!f) return {};
    return std::vector<uint8_t>((std::istreambuf_iterator<char>(f)),
                                 std::istreambuf_iterator<char>());
}

// Write known RGBA8 pixels to a PNG and return the file bytes.
std::vector<uint8_t> makeTestPng(int w, int h, uint8_t r, uint8_t g,
                                  uint8_t b, uint8_t a) {
    std::vector<uint8_t> pixels(w * h * 4);
    for (int i = 0; i < w * h; ++i) {
        pixels[i * 4 + 0] = r;
        pixels[i * 4 + 1] = g;
        pixels[i * 4 + 2] = b;
        pixels[i * 4 + 3] = a;
    }
    std::string path = tempPath("gen.png");
    EXPECT_TRUE(saveImagePng(path.c_str(), pixels.data(), w, h));
    auto bytes = readFile(path);
    std::remove(path.c_str());
    return bytes;
}

}  // anonymous namespace

TEST(ImageIOTest, LoadPngFromMemory) {
    auto pngBytes = makeTestPng(1, 1, 255, 0, 0, 255);
    ASSERT_FALSE(pngBytes.empty());

    auto result = loadImageFromMemory(pngBytes.data(), pngBytes.size());
    ASSERT_TRUE(result.has_value());
    EXPECT_EQ(result->width, 1);
    EXPECT_EQ(result->height, 1);
    ASSERT_EQ(result->pixels.size(), 4);  // RGBA
    EXPECT_EQ(result->pixels[0], 255);   // R
    EXPECT_EQ(result->pixels[1], 0);     // G
    EXPECT_EQ(result->pixels[2], 0);     // B
    EXPECT_EQ(result->pixels[3], 255);   // A
}

TEST(ImageIOTest, CorruptDataReturnsNullopt) {
    const uint8_t garbage[] = { 0, 1, 2, 3, 4, 5 };
    auto result = loadImageFromMemory(garbage, sizeof(garbage));
    EXPECT_FALSE(result.has_value());
}

TEST(ImageIOTest, SaveAndReloadPngRoundTrip) {
    int w = 3, h = 3;
    std::vector<uint8_t> pixels(w * h * 4);
    for (int y = 0; y < h; ++y) {
        for (int x = 0; x < w; ++x) {
            int i = (y * w + x) * 4;
            pixels[i + 0] = static_cast<uint8_t>((x * 255) / (w - 1));
            pixels[i + 1] = static_cast<uint8_t>((y * 255) / (h - 1));
            pixels[i + 2] = 128;
            pixels[i + 3] = 255;
        }
    }

    std::string path = tempPath("roundtrip.png");
    EXPECT_TRUE(saveImagePng(path.c_str(), pixels.data(), w, h));
    ASSERT_TRUE(fileExists(path));

    auto loaded = loadImage(path.c_str());
    ASSERT_TRUE(loaded.has_value());
    EXPECT_EQ(loaded->width, w);
    EXPECT_EQ(loaded->height, h);
    ASSERT_EQ(loaded->pixels.size(), pixels.size());
    EXPECT_EQ(loaded->pixels, pixels);

    std::remove(path.c_str());
}

TEST(ImageIOTest, SaveAndReloadJpegRoundTrip) {
    int w = 2, h = 2;
    std::vector<uint8_t> pixels(w * h * 4);
    for (int i = 0; i < w * h; ++i) {
        pixels[i * 4 + 0] = 64;
        pixels[i * 4 + 1] = 128;
        pixels[i * 4 + 2] = 192;
        pixels[i * 4 + 3] = 255;
    }

    std::string path = tempPath("roundtrip.jpg");
    EXPECT_TRUE(saveImageJpeg(path.c_str(), pixels.data(), w, h, 90));
    ASSERT_TRUE(fileExists(path));

    auto loaded = loadImage(path.c_str());
    ASSERT_TRUE(loaded.has_value());
    EXPECT_EQ(loaded->width, w);
    EXPECT_EQ(loaded->height, h);
    EXPECT_GT(loaded->pixels.size(), 0);

    std::remove(path.c_str());
}

TEST(ImageIOTest, MissingFileReturnsNullopt) {
    auto result = loadImage("/nonexistent/path/to/image.png");
    EXPECT_FALSE(result.has_value());
}

TEST(ImageIOTest, AlphaPreservedInPng) {
    int w = 2, h = 2;
    // Semi-transparent red
    std::vector<uint8_t> pixels(w * h * 4);
    for (int i = 0; i < w * h; ++i) {
        pixels[i * 4 + 0] = 255;
        pixels[i * 4 + 1] = 0;
        pixels[i * 4 + 2] = 0;
        pixels[i * 4 + 3] = 128;
    }

    std::string path = tempPath("alpha.png");
    ASSERT_TRUE(saveImagePng(path.c_str(), pixels.data(), w, h));

    auto loaded = loadImage(path.c_str());
    ASSERT_TRUE(loaded.has_value());
    // PNG preserves alpha exactly
    for (int i = 0; i < w * h; ++i) {
        EXPECT_EQ(loaded->pixels[i * 4 + 3], 128) << "pixel " << i;
    }
    std::remove(path.c_str());
}
