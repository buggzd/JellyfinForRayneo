package com.jellyfinforrayneo.client;

final class UiTheme
{
    static final String DEFAULT = "liquid-glass";
    static final String SIMPLE = "simpleUI";

    private UiTheme()
    {
    }

    static boolean isValid(String value)
    {
        return DEFAULT.equals(value) || SIMPLE.equals(value);
    }

    static String normalize(String value)
    {
        return SIMPLE.equals(value) ? SIMPLE : DEFAULT;
    }
}
