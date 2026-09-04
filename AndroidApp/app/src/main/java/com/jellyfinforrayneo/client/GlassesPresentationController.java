package com.jellyfinforrayneo.client;

import android.app.Activity;
import android.app.Presentation;
import android.content.Context;
import android.hardware.display.DisplayManager;
import android.os.Bundle;
import android.view.Display;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.widget.FrameLayout;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

final class GlassesPresentationController
{
    interface Callback extends GlassesWebViewController.BootstrapProvider
    {
        void onDisplayConnectionChanged(boolean connected);

        void onWebReadyChanged(boolean ready);

        void onGlassesMessage(GlassesMessage message);
    }

    private final Activity activity;
    private final DisplayManager displayManager;
    private final Callback callback;
    private final DisplayManager.DisplayListener displayListener =
            new DisplayManager.DisplayListener()
            {
                @Override
                public void onDisplayAdded(int displayId)
                {
                    refreshDisplay();
                }

                @Override
                public void onDisplayRemoved(int displayId)
                {
                    refreshDisplay();
                }

                @Override
                public void onDisplayChanged(int displayId)
                {
                    refreshDisplay();
                }
            };

    private GlassesPresentation presentation;
    private DisplayModeStateMachine.State displayState;
    private int activeDisplayId = Display.INVALID_DISPLAY;
    private boolean started;

    GlassesPresentationController(
            Activity activity,
            DisplayModeStateMachine.State initialState,
            Callback callback)
    {
        this.activity = activity;
        displayManager = (DisplayManager) activity.getSystemService(Context.DISPLAY_SERVICE);
        displayState = initialState;
        this.callback = callback;
    }

    void start()
    {
        if (started)
        {
            return;
        }
        started = true;
        if (displayManager != null)
        {
            displayManager.registerDisplayListener(displayListener, null);
        }
        refreshDisplay();
    }

    void refresh()
    {
        refreshDisplay();
    }

    void stop()
    {
        if (!started)
        {
            return;
        }
        started = false;
        if (displayManager != null)
        {
            displayManager.unregisterDisplayListener(displayListener);
        }
        dismissPresentation();
        callback.onDisplayConnectionChanged(false);
    }

    void setDisplayState(DisplayModeStateMachine.State state)
    {
        displayState = state;
        if (presentation != null)
        {
            presentation.setDisplayState(state);
        }
    }

    boolean dispatchCommand(String command)
    {
        return presentation != null && presentation.dispatchCommand(command);
    }

    void refreshBootstrap()
    {
        if (presentation != null)
        {
            presentation.refreshBootstrap();
        }
    }

    boolean isConnected()
    {
        return activeDisplayId != Display.INVALID_DISPLAY;
    }

    private void refreshDisplay()
    {
        if (!started || activity.isFinishing())
        {
            return;
        }
        Display selected = findBestDisplay();
        int selectedId = selected == null ? Display.INVALID_DISPLAY : selected.getDisplayId();
        if (selectedId == activeDisplayId && presentation != null)
        {
            return;
        }

        dismissPresentation();
        activeDisplayId = selectedId;
        if (selected == null)
        {
            callback.onDisplayConnectionChanged(false);
            return;
        }

        try
        {
            presentation = new GlassesPresentation(selected);
            presentation.show();
            callback.onDisplayConnectionChanged(true);
        }
        catch (RuntimeException exception)
        {
            dismissPresentation();
            callback.onDisplayConnectionChanged(false);
        }
    }

    private Display findBestDisplay()
    {
        if (displayManager == null)
        {
            return null;
        }
        Set<Integer> presentationIds = new HashSet<>();
        for (Display display : displayManager.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION))
        {
            if (display != null)
            {
                presentationIds.add(display.getDisplayId());
            }
        }

        Display[] displays = displayManager.getDisplays();
        List<DisplaySelector.Candidate> candidates = new ArrayList<>();
        for (Display display : displays)
        {
            candidates.add(new DisplaySelector.Candidate(
                    display.getDisplayId(),
                    display.isValid(),
                    display.getState() == Display.STATE_ON,
                    presentationIds.contains(display.getDisplayId()),
                    display.getName()));
        }
        int index = DisplaySelector.selectBestIndex(candidates, Display.DEFAULT_DISPLAY);
        return index < 0 ? null : displays[index];
    }

    private void dismissPresentation()
    {
        GlassesPresentation current = presentation;
        presentation = null;
        activeDisplayId = Display.INVALID_DISPLAY;
        if (current != null)
        {
            current.release();
        }
        callback.onWebReadyChanged(false);
    }

    private final class GlassesPresentation extends Presentation
    {
        private GlassesWebViewController webController;

        GlassesPresentation(Display display)
        {
            super(activity, display);
            setCancelable(false);
        }

        @Override
        protected void onCreate(Bundle savedInstanceState)
        {
            super.onCreate(savedInstanceState);
            Window window = getWindow();
            if (window != null)
            {
                window.addFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
                window.setLayout(
                        ViewGroup.LayoutParams.MATCH_PARENT,
                        ViewGroup.LayoutParams.MATCH_PARENT);
            }
            FrameLayout root = new FrameLayout(getContext());
            root.setBackgroundColor(android.graphics.Color.BLACK);
            setContentView(root);
            webController = new GlassesWebViewController(
                    root,
                    callback,
                    new GlassesWebViewController.Callback()
                    {
                        @Override
                        public void onReadyChanged(boolean ready)
                        {
                            callback.onWebReadyChanged(ready);
                        }

                        @Override
                        public void onMessage(GlassesMessage message)
                        {
                            callback.onGlassesMessage(message);
                        }
                    });
            webController.start(displayState);
        }

        void setDisplayState(DisplayModeStateMachine.State state)
        {
            if (webController != null)
            {
                webController.setDisplayState(state);
            }
        }

        boolean dispatchCommand(String command)
        {
            return webController != null && webController.dispatchCommand(command);
        }

        void refreshBootstrap()
        {
            if (webController != null)
            {
                webController.refreshBootstrap();
            }
        }

        void release()
        {
            if (webController != null)
            {
                webController.destroy();
                webController = null;
            }
            try
            {
                if (isShowing())
                {
                    dismiss();
                }
            }
            catch (RuntimeException ignored)
            {
            }
        }
    }
}
