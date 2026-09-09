package com.jellyfinforrayneo.client;

import org.junit.Test;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;

public final class UiLanguageTests
{
    @Test
    public void system_UsesSimplifiedChineseForChineseAndEnglishForOtherLanguages()
    {
        assertEquals("zh-CN", UiLanguage.resolve("system", "zh-TW"));
        assertEquals("zh-CN", UiLanguage.resolve("system", "zh-Hant-HK"));
        assertEquals("en", UiLanguage.resolve("system", "fr-FR"));
        assertEquals("en", UiLanguage.resolve("system", null));
        assertEquals("en", UiLanguage.resolve("en", "zh-CN"));
        assertEquals("zh-CN", UiLanguage.resolve("zh-CN", "en-US"));
    }

    @Test
    public void invalidPreference_FallsBackWithoutCoercingBridgeValues()
    {
        for (String value : new String[]{null, "", "EN", "en ", " zh-CN", "zh-TW"})
        {
            assertFalse(UiLanguage.isValid(value));
            assertEquals("system", UiLanguage.normalize(value));
        }
    }
}
