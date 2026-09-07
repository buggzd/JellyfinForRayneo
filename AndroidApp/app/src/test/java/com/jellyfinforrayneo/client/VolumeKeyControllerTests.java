package com.jellyfinforrayneo.client;

import android.media.AudioManager;
import android.view.KeyEvent;

import org.junit.Test;

import java.util.ArrayList;
import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class VolumeKeyControllerTests
{
    @Test
    public void hardwareKeys_MapToTheirExactAudioDirections()
    {
        assertEquals(AudioManager.ADJUST_LOWER,
                VolumeKeyController.directionForKey(KeyEvent.KEYCODE_VOLUME_DOWN));
        assertEquals(AudioManager.ADJUST_RAISE,
                VolumeKeyController.directionForKey(KeyEvent.KEYCODE_VOLUME_UP));
        assertEquals(AudioManager.ADJUST_TOGGLE_MUTE,
                VolumeKeyController.directionForKey(KeyEvent.KEYCODE_VOLUME_MUTE));
        assertEquals(VolumeKeyController.UNKNOWN_DIRECTION,
                VolumeKeyController.directionForKey(KeyEvent.KEYCODE_DPAD_UP));
    }

    @Test
    public void rapidDirectionReversal_AdjustsAndPublishesTheNewDirection()
    {
        FakeAudio audio = new FakeAudio(10, 5);
        List<Integer> published = new ArrayList<>();
        VolumeKeyController controller = new VolumeKeyController(audio, published::add);

        controller.handle(AudioManager.ADJUST_LOWER, KeyEvent.ACTION_DOWN);
        controller.handle(AudioManager.ADJUST_LOWER, KeyEvent.ACTION_DOWN);
        controller.handle(AudioManager.ADJUST_RAISE, KeyEvent.ACTION_DOWN);
        controller.handle(AudioManager.ADJUST_RAISE, KeyEvent.ACTION_DOWN);
        controller.handle(AudioManager.ADJUST_LOWER, KeyEvent.ACTION_DOWN);

        assertEquals(List.of(
                AudioManager.ADJUST_LOWER,
                AudioManager.ADJUST_LOWER,
                AudioManager.ADJUST_RAISE,
                AudioManager.ADJUST_RAISE,
                AudioManager.ADJUST_LOWER), audio.adjustments);
        assertEquals(List.of(40, 30, 40, 50, 40), published);
    }

    @Test
    public void handledVolumeKeyUp_DoesNotAdjustOrPublishTwice()
    {
        FakeAudio audio = new FakeAudio(10, 5);
        List<Integer> published = new ArrayList<>();
        VolumeKeyController controller = new VolumeKeyController(audio, published::add);

        assertTrue(controller.handle(AudioManager.ADJUST_RAISE, KeyEvent.ACTION_DOWN));
        assertTrue(controller.handle(AudioManager.ADJUST_RAISE, KeyEvent.ACTION_UP));

        assertEquals(List.of(AudioManager.ADJUST_RAISE), audio.adjustments);
        assertEquals(List.of(60), published);
    }

    @Test
    public void unknownKeyAction_IsLeftForNormalDispatch()
    {
        FakeAudio audio = new FakeAudio(10, 5);
        VolumeKeyController controller = new VolumeKeyController(audio, percentage -> { });

        assertFalse(controller.handle(VolumeKeyController.UNKNOWN_DIRECTION, KeyEvent.ACTION_DOWN));
        assertFalse(controller.handle(AudioManager.ADJUST_RAISE, 99));
        assertEquals(List.of(), audio.adjustments);
    }

    private static final class FakeAudio implements VolumeKeyController.AudioOutput
    {
        private final int maximum;
        private int current;
        private final List<Integer> adjustments = new ArrayList<>();

        FakeAudio(int maximum, int current)
        {
            this.maximum = maximum;
            this.current = current;
        }

        @Override
        public void adjust(int direction)
        {
            adjustments.add(direction);
            if (direction == AudioManager.ADJUST_RAISE)
            {
                current = Math.min(maximum, current + 1);
            }
            else if (direction == AudioManager.ADJUST_LOWER)
            {
                current = Math.max(0, current - 1);
            }
        }

        @Override
        public int getCurrentVolume()
        {
            return current;
        }

        @Override
        public int getMaximumVolume()
        {
            return maximum;
        }
    }
}
