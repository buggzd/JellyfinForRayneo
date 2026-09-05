package com.jellyfinforrayneo.client;

final class StereoScreenGeometry
{
    static final float EDGE_MARGIN_FRACTION = 0.01f;
    static final float MAX_NORMALIZED_DISPARITY = 24f / 1920f;

    final int eyeWidth;
    final int height;
    final float disparity;
    final float scale;
    final float leftX;
    final float rightX;
    final float top;

    private StereoScreenGeometry(int eyeWidth, int height, float disparity, float scale)
    {
        this.eyeWidth = eyeWidth;
        this.height = height;
        this.disparity = disparity;
        this.scale = scale;
        float inset = (1f - scale) * eyeWidth * 0.5f;
        leftX = inset + disparity * 0.5f;
        rightX = inset - disparity * 0.5f;
        top = (1f - scale) * height * 0.5f;
    }

    static boolean hasFullSbsAspect(int width, int height)
    {
        // Full-SBS is two 16:9 eyes. Allow at most two pixels of width rounding.
        return width >= 640 && width <= 8192 && height >= 180 && height <= 4320
                && width % 2 == 0 && Math.abs(width * 9L - height * 32L) <= 18L;
    }

    static boolean supportsOutput(int modeWidth, int modeHeight, int viewWidth, int viewHeight)
    {
        return hasFullSbsAspect(modeWidth, modeHeight) && hasFullSbsAspect(viewWidth, viewHeight);
    }

    static StereoScreenGeometry create(
            int width, int height, float normalizedDisparity, float sizeFraction)
    {
        if (!hasFullSbsAspect(width, height)
                || !Float.isFinite(normalizedDisparity) || !Float.isFinite(sizeFraction)
                || normalizedDisparity < 0f || normalizedDisparity > MAX_NORMALIZED_DISPARITY
                || sizeFraction < StereoScreenSettings.MIN_SIZE_PERCENT / 100f
                || sizeFraction > StereoScreenSettings.MAX_SIZE_PERCENT / 100f)
        {
            return null;
        }
        int eyeWidth = width / 2;
        float scale = Math.min(sizeFraction,
                1f - normalizedDisparity - 2f * EDGE_MARGIN_FRACTION);
        return new StereoScreenGeometry(eyeWidth, height, normalizedDisparity * eyeWidth, scale);
    }
}
