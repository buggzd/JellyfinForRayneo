package com.jellyfinforrayneo.client;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;

public final class CompanionBackgroundLayoutTests
{
    @Test
    public void crop_RoundTripsEveryRatioAndBoundary() throws Exception
    {
        for (String ratio : new String[]{"screen", "9:16", "3:4", "1:1", "original"})
        {
            JSONObject json = CompanionBackgroundLayout.DEFAULT.toJson();
            json.put("ratio", ratio).put("transparency", 0).put("zoom", 300).put("x", 0).put("y", 1000);
            CompanionBackgroundLayout layout = CompanionBackgroundLayout.parse(json.toString());
            assertNotNull(layout);
            assertEquals(json.toString(), layout.toJson().toString());
        }
    }

    @Test
    public void malformedCrop_IsRejectedBeforePersistence() throws Exception
    {
        for (Object[] change : new Object[][]{
                {"transparency", "50"}, {"transparency", 101}, {"transparency", -1},
                {"zoom", 99}, {"zoom", 301}, {"x", -1}, {"x", 1.5}, {"y", 1001},
                {"ratio", "9/16"}, {"ratio", 1}, {"url", "https://example.test"}})
        {
            JSONObject json = CompanionBackgroundLayout.DEFAULT.toJson();
            json.put((String) change[0], change[1]);
            assertNull(CompanionBackgroundLayout.parse(json.toString()));
        }
        assertNull(CompanionBackgroundLayout.parse(null));
        assertNull(CompanionBackgroundLayout.parse("{}"));
        assertNull(CompanionBackgroundLayout.parse(new String(new char[65536])));
    }

    @Test
    public void newImage_ResetsCropAndPreservesTransparency()
    {
        CompanionBackgroundLayout layout = CompanionBackgroundLayout.parse("{\"transparency\":22,\"ratio\":\"1:1\",\"zoom\":300,\"x\":0,\"y\":1000}");
        CompanionBackgroundLayout next = layout.centered();
        assertEquals(22, next.transparency);
        assertEquals("screen", next.ratio);
        assertEquals(100, next.zoom);
        assertEquals(500, next.x);
        assertEquals(500, next.y);
    }
}
