package com.jellyfinforrayneo.client;

import org.junit.Test;

import java.net.InetAddress;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

public final class JellyfinDiscoveryServiceTests
{
    @Test
    public void sanitizeAddress_ReplacesUnspecifiedHostWithPacketSource() throws Exception
    {
        assertEquals(
                "http://192.0.2.25:8096/jellyfin",
                JellyfinDiscoveryService.sanitizeAddress(
                        "http://0.0.0.0:8096/jellyfin/",
                        InetAddress.getByName("192.0.2.25")));
    }

    @Test
    public void sanitizeAddress_AcceptsCaseInsensitiveHttpScheme() throws Exception
    {
        assertEquals(
                "http://jellyfin.example:8096",
                JellyfinDiscoveryService.sanitizeAddress(
                        "HTTP://jellyfin.example:8096/",
                        InetAddress.getByName("192.0.2.25")));
    }

    @Test
    public void sanitizeAddress_RejectsCredentialsQueryAndUnsupportedScheme() throws Exception
    {
        InetAddress source = InetAddress.getByName("192.0.2.25");

        assertNull(JellyfinDiscoveryService.sanitizeAddress(
                "http://user:value@jellyfin.example:8096",
                source));
        assertNull(JellyfinDiscoveryService.sanitizeAddress(
                "http://jellyfin.example:8096/?value=unexpected",
                source));
        assertNull(JellyfinDiscoveryService.sanitizeAddress(
                "ftp://jellyfin.example/media",
                source));
    }
}
