package com.jellyfinforrayneo.client;

import org.json.JSONObject;

/** Non-destructive crop and appearance settings for the private phone image. */
final class CompanionBackgroundLayout
{
    static final int MAX_JSON_LENGTH = 160;
    static final CompanionBackgroundLayout DEFAULT = new CompanionBackgroundLayout(65, "screen", 100, 500, 500, "auto");

    final int transparency;
    final String ratio;
    final int zoom;
    final int x;
    final int y;
    final String textColor;

    private CompanionBackgroundLayout(int transparency, String ratio, int zoom, int x, int y, String textColor)
    {
        this.transparency = transparency;
        this.ratio = ratio;
        this.zoom = zoom;
        this.x = x;
        this.y = y;
        this.textColor = textColor;
    }

    static CompanionBackgroundLayout parse(String payload)
    {
        if (payload == null || payload.length() > MAX_JSON_LENGTH)
        {
            return null;
        }
        try
        {
            JSONObject json = new JSONObject(payload);
            Object ratio = json.opt("ratio");
            Object textColor = json.has("textColor") ? json.opt("textColor") : "auto";
            if (json.length() != (json.has("textColor") ? 6 : 5)
                    || !("auto".equals(textColor) || "light".equals(textColor) || "dark".equals(textColor))
                    || !(ratio instanceof String)
                    || !("screen".equals(ratio) || "9:16".equals(ratio) || "3:4".equals(ratio)
                    || "1:1".equals(ratio) || "original".equals(ratio)))
            {
                return null;
            }
            int transparency = integer(json.opt("transparency"), 0, 100);
            int zoom = integer(json.opt("zoom"), 100, 300);
            int x = integer(json.opt("x"), 0, 1000);
            int y = integer(json.opt("y"), 0, 1000);
            return transparency < 0 || zoom < 0 || x < 0 || y < 0 ? null
                    : new CompanionBackgroundLayout(transparency, (String) ratio, zoom, x, y, (String) textColor);
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    private static int integer(Object value, int minimum, int maximum)
    {
        if (!(value instanceof Number))
        {
            return -1;
        }
        double number = ((Number) value).doubleValue();
        return Double.isFinite(number) && number == Math.rint(number) && number >= minimum && number <= maximum
                ? (int) number : -1;
    }

    CompanionBackgroundLayout centered()
    {
        return new CompanionBackgroundLayout(transparency, "screen", 100, 500, 500, textColor);
    }

    JSONObject toJson()
    {
        JSONObject json = new JSONObject();
        try
        {
            json.put("transparency", transparency);
            json.put("ratio", ratio);
            json.put("zoom", zoom);
            json.put("x", x);
            json.put("y", y);
            json.put("textColor", textColor);
        }
        catch (Exception ignored)
        {
        }
        return json;
    }
}
