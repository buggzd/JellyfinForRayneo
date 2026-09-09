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
        GlassesMessage unauthorized = GlassesMessage.parse("{\"type\":\"unauthorized\",\"catalogGeneration\":3}");
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

    @Test
    public void unauthorized_RequiresBoundedGenerationToProtectNewlySwitchedAccount()
    {
        assertNull(GlassesMessage.parse("{\"type\":\"unauthorized\"}"));
        for (String value : new String[] {"-1", "1.5", "2147483648", "\"3\"", "null"})
        {
            assertNull(GlassesMessage.parse("{\"type\":\"unauthorized\",\"catalogGeneration\":" + value + "}"));
        }
        GlassesMessage message = GlassesMessage.parse("{\"type\":\"unauthorized\",\"catalogGeneration\":3}");
        assertNotNull(message);
        assertEquals(3, message.catalogGeneration);
    }

    @Test
    public void appearanceMessages_AcceptOnlyExactPreferenceValues() throws Exception
    {
        for (String theme : new String[]{"liquid-glass", "simpleUI"})
        {
            GlassesMessage message = GlassesMessage.parse(new JSONObject()
                    .put("type", "set_ui_theme").put("value", theme).toString());
            assertNotNull(message);
            assertEquals(GlassesMessage.Type.SET_UI_THEME, message.type);
            assertEquals(theme, message.preferenceValue);
        }
        for (String language : new String[]{"system", "zh-CN", "en"})
        {
            GlassesMessage message = GlassesMessage.parse(new JSONObject()
                    .put("type", "set_language").put("value", language).toString());
            assertNotNull(message);
            assertEquals(GlassesMessage.Type.SET_LANGUAGE, message.type);
            assertEquals(language, message.preferenceValue);
        }
        for (String size : new String[]{"small", "normal", "large", "extra-large"})
        {
            GlassesMessage message = GlassesMessage.parse(new JSONObject()
                    .put("type", "set_subtitle_size").put("value", size).toString());
            assertNotNull(message);
            assertEquals(GlassesMessage.Type.SET_SUBTITLE_SIZE, message.type);
            assertEquals(size, message.preferenceValue);
        }
        for (String type : new String[]{"set_ui_theme", "set_subtitle_size", "set_language"})
        {
            assertNull(GlassesMessage.parse(new JSONObject().put("type", type).toString()));
            for (Object invalid : new Object[]{JSONObject.NULL, true, 125, new JSONObject(), "", " LARGE", "simpleui", "large ", repeat('x', 8193)})
            {
                assertNull(GlassesMessage.parse(new JSONObject()
                        .put("type", type).put("value", invalid).toString()));
            }
        }
        assertNull(GlassesMessage.parse("{\"type\":\"set_ui_theme\",\"value\":\"large\"}"));
        assertNull(GlassesMessage.parse("{\"type\":\"set_subtitle_size\",\"value\":\"simpleUI\"}"));
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
