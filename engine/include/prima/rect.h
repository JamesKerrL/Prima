#ifndef PRIMA_RECT_H
#define PRIMA_RECT_H

namespace prima {

// Integer pixel rect; width <= 0 or height <= 0 means empty.
struct RectI {
    int x = 0, y = 0, width = 0, height = 0;

    bool empty() const { return width <= 0 || height <= 0; }

    // Grows this rect to be the bounding box covering both this and o.
    // If o is empty, this rect is unchanged.
    // If this is empty and o is not, this becomes equal to o.
    void unionWith(const RectI& o) {
        if (o.empty()) {
            return;
        }
        if (empty()) {
            x = o.x;
            y = o.y;
            width = o.width;
            height = o.height;
            return;
        }
        int x1 = (x < o.x) ? x : o.x;
        int y1 = (y < o.y) ? y : o.y;
        int x2 = ((x + width) > (o.x + o.width)) ? (x + width) : (o.x + o.width);
        int y2 = ((y + height) > (o.y + o.height)) ? (y + height) : (o.y + o.height);
        x = x1;
        y = y1;
        width = x2 - x1;
        height = y2 - y1;
    }

    // Returns a new RectI clamped to canvas bounds [0,0]..[w,h).
    // If this rect lies entirely outside the bounds, returns an empty rect.
    RectI intersect(int w, int h) const {
        RectI result;
        result.x = (x < 0) ? 0 : x;
        result.y = (y < 0) ? 0 : y;
        int right = x + width;
        int bottom = y + height;
        int clamped_right = (right > w) ? w : right;
        int clamped_bottom = (bottom > h) ? h : bottom;
        result.width = clamped_right - result.x;
        result.height = clamped_bottom - result.y;
        if (result.width < 0) result.width = 0;
        if (result.height < 0) result.height = 0;
        return result;
    }
};

}  // namespace prima

#endif  // PRIMA_RECT_H
