package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class RayNeoUsbProtocolTests
{
    @Test
    public void stereoReport_MatchesVerifiedHardwareCommand()
    {
        byte[] expected = new byte[64];
        expected[0] = 0x66;
        expected[1] = 6;
        assertArrayEquals(expected, RayNeoUsbProtocol.displayMode(true));
    }

    @Test
    public void mirrorReport_MatchesVerifiedHardwareCommand()
    {
        byte[] expected = new byte[64];
        expected[0] = 0x66;
        expected[1] = 7;
        assertArrayEquals(expected, RayNeoUsbProtocol.displayMode(false));
    }

    @Test
    public void unknownUsbIdentity_IsNotSentCommands()
    {
        assertTrue(RayNeoUsbProtocol.supports(7099, 44880));
        assertFalse(RayNeoUsbProtocol.supports(7099, 44881));
        assertFalse(RayNeoUsbProtocol.supports(14657, 44880));
        assertFalse(RayNeoUsbProtocol.supports(-1, -1));
    }
}
