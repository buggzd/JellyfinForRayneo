package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public final class StereoScreenSettingsTests
{
    @Test
    public void validPreferences_RoundTripAtEverySupportedSetting()
    {
        for (int depth = 0; depth <= 3; depth++)
        {
            for (int size = 80; size <= 95; size++)
            {
                StereoScreenSettings settings = StereoScreenSettings.parse(
                        "{\"depthLevel\":" + depth + ",\"sizePercent\":" + size + "}");
                assertNotNull(settings);
                assertEquals(depth * 8f / 1920f, settings.normalizedDisparity(), 0.000001f);
                assertEquals(size / 100f, settings.sizeFraction(), 0.000001f);
                assertTrue(settings.sameAs(StereoScreenSettings.parse(settings.toJson().toString())));
                assertEquals(2, settings.toJson().length());
            }
        }
    }

    @Test
    public void malformedOrUnboundedBridgeInput_IsRejected()
    {
        String[] invalid = {
                null, "", "broken", "[]", "{}", "{\"depthLevel\":1}",
                "{\"depthLevel\":-1,\"sizePercent\":90}",
                "{\"depthLevel\":4,\"sizePercent\":90}",
                "{\"depthLevel\":1,\"sizePercent\":79}",
                "{\"depthLevel\":1,\"sizePercent\":96}",
                "{\"depthLevel\":1.5,\"sizePercent\":90}",
                "{\"depthLevel\":1,\"sizePercent\":90.5}",
                "{\"depthLevel\":\"1\",\"sizePercent\":90}",
                "{\"depthLevel\":1,\"sizePercent\":\"90\"}",
                "{\"depthLevel\":true,\"sizePercent\":90}",
                "{\"depthLevel\":null,\"sizePercent\":90}",
                "{\"depthLevel\":{},\"sizePercent\":90}",
                "{\"depthLevel\":1e99,\"sizePercent\":90}",
                "{\"depthLevel\":1,\"sizePercent\":90,\"extra\":1}",
                new String(new char[129]).replace('\0', ' ')
        };
        for (String payload : invalid)
        {
            assertNull(payload, StereoScreenSettings.parse(payload));
        }
    }
}
