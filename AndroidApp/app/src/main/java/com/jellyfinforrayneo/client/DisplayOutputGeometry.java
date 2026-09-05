package com.jellyfinforrayneo.client;

import org.json.JSONObject;

final class DisplayOutputGeometry
{
    static final DisplayOutputGeometry EMPTY = new DisplayOutputGeometry(0, 0, 0, 0, 0f);

    final int modeWidth;
    final int modeHeight;
    final int viewWidth;
    final int viewHeight;
    final float refreshRate;
    final boolean stereoReady;

    DisplayOutputGeometry(int modeWidth, int modeHeight, int viewWidth, int viewHeight, float refreshRate)
    {
        this.modeWidth = modeWidth;
        this.modeHeight = modeHeight;
        this.viewWidth = viewWidth;
        this.viewHeight = viewHeight;
        this.refreshRate = Float.isFinite(refreshRate) ? refreshRate : 0f;
        stereoReady = StereoScreenGeometry.supportsOutput(modeWidth, modeHeight, viewWidth, viewHeight);
    }

    boolean sameAs(DisplayOutputGeometry other)
    {
        return other != null && modeWidth == other.modeWidth && modeHeight == other.modeHeight
                && viewWidth == other.viewWidth && viewHeight == other.viewHeight
                && refreshRate == other.refreshRate;
    }

    JSONObject toJson()
    {
        JSONObject result = new JSONObject();
        try
        {
            result.put("modeWidth", modeWidth);
            result.put("modeHeight", modeHeight);
            result.put("viewWidth", viewWidth);
            result.put("viewHeight", viewHeight);
            result.put("refreshRate", refreshRate);
            result.put("stereoReady", stereoReady);
        }
        catch (Exception ignored)
        {
        }
        return result;
    }
}
