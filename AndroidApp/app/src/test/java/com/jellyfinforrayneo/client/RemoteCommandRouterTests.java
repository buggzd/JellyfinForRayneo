package com.jellyfinforrayneo.client;

import org.junit.Test;

import java.util.ArrayList;
import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class RemoteCommandRouterTests
{
    @Test
    public void submit_AllowsOnlyBoundedWhitelist()
    {
        RemoteCommandRouter router = new RemoteCommandRouter();

        assertTrue(router.submit("up"));
        assertTrue(router.submit("submit"));
        assertFalse(router.submit("javascript:alert(1)"));
        assertFalse(router.submit("volume:999"));
        assertEquals(2, router.pendingCount());
    }

    @Test
    public void pendingQueue_DropsOldestAtCapacity()
    {
        RemoteCommandRouter router = new RemoteCommandRouter();
        List<String> delivered = new ArrayList<>();
        router.setSink(command ->
        {
            delivered.add(command);
            return true;
        });

        for (int index = 0; index < RemoteCommandRouter.MAX_PENDING_COMMANDS + 5; index++)
        {
            router.submit(index % 2 == 0 ? "left" : "right");
        }
        assertEquals(RemoteCommandRouter.MAX_PENDING_COMMANDS, router.pendingCount());

        router.setReady(true);

        assertEquals(RemoteCommandRouter.MAX_PENDING_COMMANDS, delivered.size());
        assertEquals(0, router.pendingCount());
        assertEquals("right", delivered.get(0));
    }

    @Test
    public void submit_MapsPhoneSubmitToDomEnter()
    {
        RemoteCommandRouter router = new RemoteCommandRouter();
        List<String> delivered = new ArrayList<>();
        router.setSink(command ->
        {
            delivered.add(command);
            return true;
        });
        router.setReady(true);

        router.submit("submit");

        assertEquals(List.of("enter"), delivered);
    }
}
