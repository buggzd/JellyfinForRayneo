package com.jellyfinforrayneo.client;

final class SubtitleSize
{
    static final String DEFAULT = "normal";

    private SubtitleSize()
    {
    }

    static boolean isValid(String value)
    {
        return "small".equals(value) || DEFAULT.equals(value)
                || "large".equals(value) || "extra-large".equals(value);
    }

    static String normalize(String value)
    {
        return isValid(value) ? value : DEFAULT;
    }
}
