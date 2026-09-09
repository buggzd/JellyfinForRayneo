package com.jellyfinforrayneo.client;

import java.util.Locale;

final class UiLanguage
{
    static final String DEFAULT = "system";

    private UiLanguage() {}

    static boolean isValid(String value)
    {
        return "system".equals(value) || "zh-CN".equals(value) || "en".equals(value);
    }

    static String normalize(String value)
    {
        return isValid(value) ? value : DEFAULT;
    }

    static String resolve(String value, String systemLanguage)
    {
        String preference = normalize(value);
        if (!DEFAULT.equals(preference))
        {
            return preference;
        }
        String system = systemLanguage == null ? "" : systemLanguage.toLowerCase(Locale.ROOT);
        return system.equals("zh") || system.startsWith("zh-") ? "zh-CN" : "en";
    }
}
