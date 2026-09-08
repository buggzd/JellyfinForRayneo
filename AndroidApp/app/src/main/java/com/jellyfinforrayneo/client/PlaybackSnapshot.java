package com.jellyfinforrayneo.client;

import org.json.JSONObject;

final class PlaybackSnapshot
{
    private String state = "stopped";
    private String itemId = "";
    private String title = "";
    private String subtitle = "";
    private String playMethod = "";
    private long positionTicks;
    private long durationTicks;
    private boolean seekEnabled;

    void update(GlassesMessage message)
    {
        if (message == null || message.type != GlassesMessage.Type.PLAYBACK_STATE)
        {
            return;
        }
        state = message.state;
        itemId = message.itemId;
        title = message.title;
        subtitle = message.subtitle;
        playMethod = message.playMethod;
        positionTicks = message.positionTicks;
        durationTicks = message.durationTicks;
        seekEnabled = message.seekEnabled;
    }

    void clear()
    {
        state = "stopped";
        itemId = "";
        title = "";
        subtitle = "";
        playMethod = "";
        positionTicks = 0L;
        durationTicks = 0L;
        seekEnabled = false;
    }

    boolean isSeekEnabled()
    {
        return seekEnabled;
    }

    JSONObject toJson()
    {
        JSONObject result = new JSONObject();
        try
        {
            result.put("state", state);
            result.put("itemId", itemId);
            result.put("title", title);
            result.put("subtitle", subtitle);
            result.put("playMethod", playMethod);
            result.put("positionTicks", positionTicks);
            result.put("durationTicks", durationTicks);
            result.put("seekEnabled", seekEnabled);
        }
        catch (Exception ignored)
        {
            return new JSONObject();
        }
        return result;
    }
}
