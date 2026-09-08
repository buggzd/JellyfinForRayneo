package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class PlaybackSnapshotTests
{
    @Test
    public void seekPermission_RequiresBooleanFocusAndActivePlayback()
    {
        PlaybackSnapshot snapshot = new PlaybackSnapshot();
        snapshot.update(GlassesMessage.parse("{\"type\":\"playback_state\",\"state\":\"paused\",\"seekEnabled\":true}"));
        assertTrue(snapshot.isSeekEnabled());
        assertTrue(snapshot.toJson().optBoolean("seekEnabled"));
        for (String value : new String[] { "\"true\"", "1", "null", "false" })
        {
            snapshot.update(GlassesMessage.parse("{\"type\":\"playback_state\",\"state\":\"playing\",\"seekEnabled\":" + value + "}"));
            assertFalse(snapshot.isSeekEnabled());
        }
        for (String state : new String[] { "stopped", "preparing", "error" })
        {
            snapshot.update(GlassesMessage.parse("{\"type\":\"playback_state\",\"state\":\"" + state + "\",\"seekEnabled\":true}"));
            assertFalse(snapshot.isSeekEnabled());
        }
    }

    @Test
    public void clearingPlayback_RemovesCircularSeekPermission()
    {
        PlaybackSnapshot snapshot = new PlaybackSnapshot();
        snapshot.update(GlassesMessage.parse("{\"type\":\"playback_state\",\"state\":\"playing\",\"seekEnabled\":true}"));
        snapshot.clear();
        assertFalse(snapshot.isSeekEnabled());
        assertFalse(snapshot.toJson().optBoolean("seekEnabled"));
    }
}
