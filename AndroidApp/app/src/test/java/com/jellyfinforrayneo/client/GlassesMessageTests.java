package com.jellyfinforrayneo.client;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;

public final class GlassesMessageTests
{
    @Test
    public void parse_AcceptsUnauthorizedAndRuntimeState() throws Exception
    {
        GlassesMessage unauthorized = GlassesMessage.parse("{\"type\":\"unauthorized\"}");
        GlassesMessage runtime = GlassesMessage.parse(
                "{\"type\":\"runtime_state\",\"state\":\"ready\"}");

        assertNotNull(unauthorized);
        assertEquals(GlassesMessage.Type.UNAUTHORIZED, unauthorized.type);
        assertNotNull(runtime);
        assertEquals(GlassesMessage.Type.RUNTIME_STATE, runtime.type);
        assertEquals("none", runtime.errorCode);
    }

    @Test
    public void parse_ValidatesAndNormalizesRuntimeErrorCode()
    {
        GlassesMessage network = GlassesMessage.parse(
                "{\"type\":\"runtime_state\",\"state\":\"error\",\"errorCode\":\"NETWORK\"}");
        GlassesMessage legacy = GlassesMessage.parse(
                "{\"type\":\"runtime_state\",\"state\":\"error\"}");
        GlassesMessage ready = GlassesMessage.parse(
                "{\"type\":\"runtime_state\",\"state\":\"ready\",\"errorCode\":\"http\"}");

        assertNotNull(network);
        assertEquals("network", network.errorCode);
        assertNotNull(legacy);
        assertEquals("unknown", legacy.errorCode);
        assertNotNull(ready);
        assertEquals("none", ready.errorCode);
    }

    @Test
    public void parse_RejectsUnknownAndInvalidPlaybackState()
    {
        assertNull(GlassesMessage.parse("{\"type\":\"execute\"}"));
        assertNull(GlassesMessage.parse(
                "{\"type\":\"playback_state\",\"state\":\"invalid\"}"));
        assertNull(GlassesMessage.parse(
                "{\"type\":\"runtime_state\",\"state\":\"error\",\"errorCode\":\"details\"}"));
        assertNull(GlassesMessage.parse(
                "{\"type\":\"search_state\",\"state\":\"visible\"}"));
    }

    @Test
    public void parse_AcceptsOnlyBoundedAsciiSearchState() throws Exception
    {
        GlassesMessage active = GlassesMessage.parse(
                "{\"type\":\"search_state\",\"state\":\"active\",\"query\":\"QYN 12\"}");
        GlassesMessage inactive = GlassesMessage.parse(
                "{\"type\":\"search_state\",\"state\":\"inactive\",\"query\":\"ignored\"}");

        assertNotNull(active);
        assertEquals(GlassesMessage.Type.SEARCH_STATE, active.type);
        assertEquals("qyn 12", active.query);
        assertNotNull(inactive);
        assertEquals("", inactive.query);
        assertNull(GlassesMessage.parse(
                "{\"type\":\"search_state\",\"state\":\"active\",\"query\":\"庆余年\"}"));

        JSONObject oversized = new JSONObject();
        oversized.put("type", "search_state");
        oversized.put("state", "active");
        oversized.put("query", repeat('a', GlassesMessage.MAX_SEARCH_QUERY_LENGTH + 1));
        assertNull(GlassesMessage.parse(oversized.toString()));
    }

    @Test
    public void parse_BoundsPlaybackFieldsAndTicks() throws Exception
    {
        JSONObject source = new JSONObject();
        source.put("type", "playback_state");
        source.put("state", "playing");
        source.put("title", repeat('x', 300));
        source.put("positionTicks", -10);
        source.put("durationTicks", Long.MAX_VALUE);
        source.put("playMethod", "TRANSCODE");

        GlassesMessage message = GlassesMessage.parse(source.toString());

        assertNotNull(message);
        assertEquals(180, message.title.length());
        assertEquals(0L, message.positionTicks);
        assertEquals(GlassesMessage.MAX_MEDIA_TICKS, message.durationTicks);
        assertEquals("Transcode", message.playMethod);
    }

    private static String repeat(char value, int count)
    {
        StringBuilder result = new StringBuilder(count);
        for (int index = 0; index < count; index++)
        {
            result.append(value);
        }
        return result.toString();
    }
}
