package com.jellyfinforrayneo.client;

import org.json.JSONObject;

final class StereoScreenSettings
{
    static final int MAX_JSON_LENGTH = 128;
    static final int MAX_DEPTH_LEVEL = 3;
    static final int MIN_SIZE_PERCENT = 80;
    static final int MAX_SIZE_PERCENT = 95;
    static final StereoScreenSettings DEFAULT = new StereoScreenSettings(1, 90);

    final int depthLevel;
    final int sizePercent;

    private StereoScreenSettings(int depthLevel, int sizePercent)
    {
        this.depthLevel = depthLevel;
        this.sizePercent = sizePercent;
    }

    static StereoScreenSettings parse(String payload)
    {
        if (payload == null || payload.length() > MAX_JSON_LENGTH)
        {
            return null;
        }
        try
        {
            JSONObject json = new JSONObject(payload);
            if (json.length() != 2)
            {
                return null;
            }
            int depth = boundedInteger(json.opt("depthLevel"), 0, MAX_DEPTH_LEVEL);
            int size = boundedInteger(json.opt("sizePercent"), MIN_SIZE_PERCENT, MAX_SIZE_PERCENT);
            return depth < 0 || size < 0 ? null : new StereoScreenSettings(depth, size);
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    private static int boundedInteger(Object value, int minimum, int maximum)
    {
        if (!(value instanceof Number))
        {
            return -1;
        }
        double number = ((Number) value).doubleValue();
        return Double.isFinite(number) && number == Math.rint(number)
                && number >= minimum && number <= maximum ? (int) number : -1;
    }

    float normalizedDisparity()
    {
        // Total L-R disparity: 0, 8, 16 or 24 pixels at 1920 pixels per eye.
        // Positive disparity adds convergence without assuming an optical zero distance.
        return depthLevel * 8f / 1920f;
    }

    float sizeFraction()
    {
        return sizePercent / 100f;
    }

    boolean sameAs(StereoScreenSettings other)
    {
        return other != null && depthLevel == other.depthLevel && sizePercent == other.sizePercent;
    }

    JSONObject toJson()
    {
        JSONObject json = new JSONObject();
        try
        {
            json.put("depthLevel", depthLevel);
            json.put("sizePercent", sizePercent);
        }
        catch (Exception ignored)
        {
        }
        return json;
    }
}
