package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class DisplayModeStateMachineTests
{
    @Test
    public void connectAndConfirmStereo_SeparatesTransitionFromAppliedState()
    {
        DisplayModeStateMachine machine = new DisplayModeStateMachine(
                DisplayModeStateMachine.STEREO_SCREEN);

        assertEquals(
                DisplayModeStateMachine.Action.SWITCH_TO_3D,
                machine.setConnected(true, 100L));
        DisplayModeStateMachine.State transitioning = machine.snapshot();
        assertTrue(transitioning.displayModeTransitioning);
        assertFalse(transitioning.displayModeApplied);

        machine.onCommandResponse(DisplayModeStateMachine.COMMAND_3D, true, 200L);

        DisplayModeStateMachine.State applied = machine.snapshot();
        assertFalse(applied.displayModeTransitioning);
        assertTrue(applied.displayModeApplied);
        assertEquals(DisplayModeStateMachine.STEREO_SCREEN, applied.activeMode);
    }

    @Test
    public void timeout_RevealsSafeMirrorUntilExplicitRetry()
    {
        DisplayModeStateMachine machine = new DisplayModeStateMachine(
                DisplayModeStateMachine.STEREO_SCREEN);
        machine.setConnected(true, 0L);

        assertEquals(
                DisplayModeStateMachine.Action.SWITCH_TO_2D,
                machine.tick(DisplayModeStateMachine.TRANSITION_TIMEOUT_MS));
        DisplayModeStateMachine.State failed = machine.snapshot();
        assertFalse(failed.displayModeTransitioning);
        assertFalse(failed.displayModeApplied);
        assertEquals(DisplayModeStateMachine.MIRROR_2D, failed.activeMode);
        assertEquals(DisplayModeStateMachine.STEREO_SCREEN, failed.requestedMode);

        assertEquals(
                DisplayModeStateMachine.Action.NONE,
                machine.tick(DisplayModeStateMachine.TRANSITION_TIMEOUT_MS + 60_000L));
        assertEquals(
                DisplayModeStateMachine.Action.SWITCH_TO_3D,
                machine.requestMode(
                        DisplayModeStateMachine.STEREO_SCREEN,
                        DisplayModeStateMachine.TRANSITION_TIMEOUT_MS + 60_001L));
    }

    @Test
    public void disconnect_EndsTransitionAndReconnectsUsingSavedRequest()
    {
        DisplayModeStateMachine machine = new DisplayModeStateMachine(
                DisplayModeStateMachine.STEREO_SCREEN);
        machine.setConnected(true, 0L);

        machine.setConnected(false, 10L);

        DisplayModeStateMachine.State disconnected = machine.snapshot();
        assertFalse(disconnected.displayModeTransitioning);
        assertEquals(DisplayModeStateMachine.MIRROR_2D, disconnected.activeMode);
        assertEquals(
                DisplayModeStateMachine.Action.SWITCH_TO_3D,
                machine.setConnected(true, 20L));
    }

    @Test
    public void duplicateConnectionWhileTransitioning_DoesNotRepeatSdkCommand()
    {
        DisplayModeStateMachine machine = new DisplayModeStateMachine(
                DisplayModeStateMachine.STEREO_SCREEN);

        assertEquals(
                DisplayModeStateMachine.Action.SWITCH_TO_3D,
                machine.setConnected(true, 0L));
        assertEquals(
                DisplayModeStateMachine.Action.NONE,
                machine.setConnected(true, 100L));

        DisplayModeStateMachine.State state = machine.snapshot();
        assertTrue(state.displayModeTransitioning);
        assertFalse(state.displayModeApplied);
        assertEquals(DisplayModeStateMachine.STEREO_SCREEN, state.requestedMode);
    }

    @Test
    public void pause_AlwaysRequestsBestEffort2DAndKeepsPreference()
    {
        DisplayModeStateMachine machine = new DisplayModeStateMachine(
                DisplayModeStateMachine.STEREO_SCREEN);
        machine.setConnected(true, 0L);
        machine.onCommandResponse(DisplayModeStateMachine.COMMAND_3D, true, 10L);

        assertEquals(DisplayModeStateMachine.Action.SWITCH_TO_2D, machine.pause());
        DisplayModeStateMachine.State paused = machine.snapshot();
        assertEquals(DisplayModeStateMachine.STEREO_SCREEN, paused.requestedMode);
        assertEquals(DisplayModeStateMachine.MIRROR_2D, paused.activeMode);
        assertFalse(paused.displayModeTransitioning);
    }
}
