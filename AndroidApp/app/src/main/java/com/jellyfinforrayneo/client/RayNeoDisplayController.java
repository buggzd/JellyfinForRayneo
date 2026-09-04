package com.jellyfinforrayneo.client;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;

import com.tcl.xr.api.AirApi;
import com.tcl.xr.api.USBDeviceEventListener;

final class RayNeoDisplayController
{
    private static final String TAG = "RayNeoDisplay";
    private static final long STATE_TICK_MS = 100L;

    interface Listener
    {
        void onDisplayModeStateChanged(DisplayModeStateMachine.State state);
    }

    private final Activity activity;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final DisplayModeStateMachine stateMachine;
    private final Listener listener;
    private final Runnable stateTick = new Runnable()
    {
        @Override
        public void run()
        {
            if (destroyed)
            {
                return;
            }
            execute(stateMachine.tick(SystemClock.uptimeMillis()));
            scheduleTick();
        }
    };
    private final USBDeviceEventListener usbListener = new USBDeviceEventListener()
    {
        @Override
        public void onSensorChanged(
                float[] gyroscope,
                float[] accelerometer,
                float[] magnetometer,
                float[] quaternion,
                long timestamp)
        {
            // This application intentionally has no head-tracked scene.
        }

        @Override
        public void onCommandResp(byte command, boolean success, String ignoredMessage)
        {
            handler.post(() ->
            {
                if (destroyed)
                {
                    return;
                }
                execute(stateMachine.onCommandResponse(
                        command & 0xff,
                        success,
                        SystemClock.uptimeMillis()));
            });
        }
    };

    private AirApi airApi;
    private DisplayModeStateMachine.State lastPublishedState;
    private boolean sdkInitialized;
    private boolean paused;
    private boolean destroyed;

    RayNeoDisplayController(Activity activity, String initialMode, Listener listener)
    {
        this.activity = activity;
        stateMachine = new DisplayModeStateMachine(initialMode);
        this.listener = listener;
    }

    void start()
    {
        ensureSdkInitialized();
        publish();
        scheduleTick();
    }

    void setConnected(boolean connected)
    {
        long nowMs = SystemClock.uptimeMillis();
        if (paused && connected)
        {
            stateMachine.setConnected(true, nowMs);
            execute(stateMachine.pause());
            return;
        }
        execute(stateMachine.setConnected(connected, nowMs));
    }

    void requestMode(String mode)
    {
        execute(stateMachine.requestMode(mode, SystemClock.uptimeMillis()));
    }

    void onResume()
    {
        if (destroyed)
        {
            return;
        }
        paused = false;
        ensureSdkInitialized();
        if (sdkInitialized)
        {
            try
            {
                airApi.OnResume();
            }
            catch (RuntimeException exception)
            {
                Log.w(TAG, "RayNeo SDK resume failed.");
            }
        }
        DisplayModeStateMachine.State state = stateMachine.snapshot();
        if (state.connected)
        {
            execute(stateMachine.setConnected(true, SystemClock.uptimeMillis()));
        }
        scheduleTick();
    }

    void onPause()
    {
        if (!destroyed)
        {
            paused = true;
            handler.removeCallbacks(stateTick);
            execute(stateMachine.pause());
        }
    }

    void destroy()
    {
        if (destroyed)
        {
            return;
        }
        execute(stateMachine.pause());
        destroyed = true;
        handler.removeCallbacks(stateTick);
        if (airApi != null && sdkInitialized)
        {
            try
            {
                airApi.unRegisterUSBDeviceListener();
                airApi.Destroy();
            }
            catch (RuntimeException exception)
            {
                Log.w(TAG, "RayNeo SDK shutdown was incomplete.");
            }
        }
        airApi = null;
        sdkInitialized = false;
    }

    DisplayModeStateMachine.State getState()
    {
        return stateMachine.snapshot();
    }

    private void ensureSdkInitialized()
    {
        if (destroyed || sdkInitialized)
        {
            return;
        }
        try
        {
            airApi = AirApi.ins();
            airApi.init(activity);
            airApi.registerUSBDeviceListener(usbListener);
            sdkInitialized = true;
        }
        catch (RuntimeException | LinkageError exception)
        {
            airApi = null;
            sdkInitialized = false;
            Log.w(TAG, "RayNeo SDK initialization failed.");
        }
    }

    private void execute(DisplayModeStateMachine.Action action)
    {
        DisplayModeStateMachine.State before = stateMachine.snapshot();
        if (action != DisplayModeStateMachine.Action.NONE)
        {
            ensureSdkInitialized();
            if (!sdkInitialized || airApi == null)
            {
                if (before.displayModeTransitioning)
                {
                    stateMachine.onSdkFailure(SystemClock.uptimeMillis());
                }
            }
            else
            {
                try
                {
                    if (action == DisplayModeStateMachine.Action.SWITCH_TO_3D)
                    {
                        airApi.switchTo3DMode();
                    }
                    else
                    {
                        airApi.switchTo2DMode();
                    }
                }
                catch (RuntimeException | LinkageError exception)
                {
                    if (before.displayModeTransitioning)
                    {
                        stateMachine.onSdkFailure(SystemClock.uptimeMillis());
                    }
                    bestEffort2D();
                    Log.w(TAG, "RayNeo display mode command failed.");
                }
            }
        }
        publish();
    }

    private void bestEffort2D()
    {
        if (airApi == null || !sdkInitialized)
        {
            return;
        }
        try
        {
            airApi.switchTo2DMode();
        }
        catch (RuntimeException | LinkageError ignored)
        {
        }
    }

    private void publish()
    {
        DisplayModeStateMachine.State next = stateMachine.snapshot();
        if (sameState(lastPublishedState, next))
        {
            return;
        }
        lastPublishedState = next;
        if (listener != null)
        {
            listener.onDisplayModeStateChanged(next);
        }
    }

    private static boolean sameState(
            DisplayModeStateMachine.State first,
            DisplayModeStateMachine.State second)
    {
        return first != null
                && second != null
                && first.requestedMode.equals(second.requestedMode)
                && first.activeMode.equals(second.activeMode)
                && first.displayModeApplied == second.displayModeApplied
                && first.displayModeTransitioning == second.displayModeTransitioning
                && first.connected == second.connected
                && first.message.equals(second.message);
    }

    private void scheduleTick()
    {
        handler.removeCallbacks(stateTick);
        if (!destroyed && !paused)
        {
            handler.postDelayed(stateTick, STATE_TICK_MS);
        }
    }
}
