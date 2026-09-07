package com.jellyfinforrayneo.client;

import android.media.AudioManager;
import android.view.KeyEvent;

final class VolumeKeyController
{
    static final int UNKNOWN_DIRECTION = Integer.MIN_VALUE;

    interface AudioOutput
    {
        void adjust(int direction);

        int getCurrentVolume();

        int getMaximumVolume();
    }

    interface VolumeSink
    {
        void publish(int percentage);
    }

    private final AudioOutput audio;
    private final VolumeSink sink;

    VolumeKeyController(AudioOutput audio, VolumeSink sink)
    {
        this.audio = audio;
        this.sink = sink;
    }

    boolean handle(int direction, int action)
    {
        if (direction == UNKNOWN_DIRECTION)
        {
            return false;
        }
        if (action == KeyEvent.ACTION_UP)
        {
            return true;
        }
        if (action != KeyEvent.ACTION_DOWN)
        {
            return false;
        }

        audio.adjust(direction);
        int maximum = audio.getMaximumVolume();
        int current = audio.getCurrentVolume();
        int percentage = maximum <= 0
                ? 0
                : Math.round(Math.max(0, Math.min(current, maximum)) * 100f / maximum);
        sink.publish(percentage);
        return true;
    }

    static int directionForKey(int keyCode)
    {
        switch (keyCode)
        {
            case KeyEvent.KEYCODE_VOLUME_UP:
                return AudioManager.ADJUST_RAISE;
            case KeyEvent.KEYCODE_VOLUME_DOWN:
                return AudioManager.ADJUST_LOWER;
            case KeyEvent.KEYCODE_VOLUME_MUTE:
                return AudioManager.ADJUST_TOGGLE_MUTE;
            default:
                return UNKNOWN_DIRECTION;
        }
    }
}
