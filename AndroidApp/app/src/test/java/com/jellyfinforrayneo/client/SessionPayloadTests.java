package com.jellyfinforrayneo.client;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;

public final class SessionPayloadTests
{
    @Test
    public void fromJson_RebuildsOnlyWhitelistedFields() throws Exception
    {
        JSONObject source = validJson();
        source.put("password", "must-not-survive");
        source.put("createdAt", 42);

        SessionPayload payload = SessionPayload.fromJson(source.toString());

        assertNotNull(payload);
        JSONObject normalized = new JSONObject(payload.toJson());
        assertEquals(8, normalized.length());
        assertFalse(normalized.has("password"));
        assertFalse(normalized.has("createdAt"));
        assertEquals("http://jellyfin.local:8096", normalized.getString("serverUrl"));
    }

    @Test
    public void fromJson_RejectsMissingRequiredValues() throws Exception
    {
        JSONObject source = validJson();
        source.remove("accessToken");

        assertNull(SessionPayload.fromJson(source.toString()));
    }

    @Test
    public void fromJson_RejectsNonStringSecurityValues() throws Exception
    {
        JSONObject source = validJson();
        source.put("accessToken", 1234);

        assertNull(SessionPayload.fromJson(source.toString()));
    }

    @Test
    public void fromJson_RejectsOversizedFields() throws Exception
    {
        JSONObject source = validJson();
        source.put("accessToken", repeat('x', SessionPayload.MAX_ACCESS_TOKEN_LENGTH + 1));

        assertNull(SessionPayload.fromJson(source.toString()));
    }

    @Test
    public void normalizeServerUrl_RejectsCredentialsQueryAndFragment()
    {
        assertInvalidUrl("http://user:password@jellyfin.local:8096");
        assertInvalidUrl("https://jellyfin.local/path?token=secret");
        assertInvalidUrl("https://jellyfin.local/path#fragment");
        assertInvalidUrl("ftp://jellyfin.local/media");
    }

    @Test
    public void normalizeServerUrl_AddsDefaultSchemeAndPreservesSubpath() throws Exception
    {
        assertEquals(
                "http://jellyfin.local:8096/media",
                SessionPayload.normalizeServerUrl("jellyfin.local:8096/media/"));
    }

    @Test
    public void normalizeServerUrl_AcceptsBracketedIpv6PortAndSubpath() throws Exception
    {
        assertEquals(
                "http://[2001:db8::20]:8096/jellyfin",
                SessionPayload.normalizeServerUrl("[2001:db8::20]:8096/jellyfin/"));
        assertEquals(
                "https://[2001:db8::20]/jellyfin",
                SessionPayload.normalizeServerUrl("https://[2001:db8::20]/jellyfin/"));
    }

    @Test
    public void normalizeServerUrl_BracketsBareIpv6WithoutPort() throws Exception
    {
        assertEquals(
                "http://[2001:db8::20]",
                SessionPayload.normalizeServerUrl("2001:db8::20"));
        assertEquals(
                "https://[2001:db8::20]/jellyfin",
                SessionPayload.normalizeServerUrl("https://2001:db8::20/jellyfin/"));
    }

    @Test
    public void normalizeServerUrl_RejectsScopedIpv6AndInvalidPort()
    {
        assertInvalidUrl("http://[fe80::1%25wlan0]:8096");
        assertInvalidUrl("http://[2001:db8::20]:99999");
    }

    @Test
    public void toString_DoesNotExposeSessionValues() throws Exception
    {
        JSONObject source = validJson();
        SessionPayload payload = SessionPayload.fromJson(source.toString());

        assertNotNull(payload);
        String diagnostic = payload.toString();
        assertFalse(diagnostic.contains(source.getString("serverUrl")));
        assertFalse(diagnostic.contains(source.getString("accessToken")));
        assertFalse(diagnostic.contains(source.getString("userId")));
    }

    private static JSONObject validJson() throws Exception
    {
        JSONObject source = new JSONObject();
        source.put("serverUrl", "http://jellyfin.local:8096/");
        source.put("serverName", "Home");
        source.put("serverVersion", "10.10");
        source.put("serverId", "server-id");
        source.put("accessToken", "access-token");
        source.put("userId", "user-id");
        source.put("userName", "RayNeo");
        source.put("deviceId", "device-id");
        return source;
    }

    private static void assertInvalidUrl(String value)
    {
        try
        {
            SessionPayload.normalizeServerUrl(value);
        }
        catch (Exception expected)
        {
            return;
        }
        throw new AssertionError("Expected URL to be rejected: " + value);
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
