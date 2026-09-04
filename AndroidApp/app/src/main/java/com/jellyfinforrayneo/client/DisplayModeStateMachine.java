package com.jellyfinforrayneo.client;

import java.util.Locale;

final class DisplayModeStateMachine
{
    static final String MIRROR_2D = "mirror_2d";
    static final String STEREO_SCREEN = "stereo_screen";
    static final int COMMAND_3D = 6;
    static final int COMMAND_2D = 7;
    static final long TRANSITION_TIMEOUT_MS = 1_500L;

    enum Action
    {
        NONE,
        SWITCH_TO_2D,
        SWITCH_TO_3D
    }

    static final class State
    {
        final String requestedMode;
        final String activeMode;
        final boolean displayModeApplied;
        final boolean displayModeTransitioning;
        final boolean connected;
        final String message;

        State(
                String requestedMode,
                String activeMode,
                boolean displayModeApplied,
                boolean displayModeTransitioning,
                boolean connected,
                String message)
        {
            this.requestedMode = requestedMode;
            this.activeMode = activeMode;
            this.displayModeApplied = displayModeApplied;
            this.displayModeTransitioning = displayModeTransitioning;
            this.connected = connected;
            this.message = message;
        }
    }

    private String requestedMode;
    private String activeMode = MIRROR_2D;
    private boolean applied;
    private boolean transitioning;
    private boolean connected;
    private long transitionDeadline;
    private String message;

    DisplayModeStateMachine(String initialMode)
    {
        requestedMode = normalizeMode(initialMode);
        message = disconnectedMessage();
    }

    synchronized Action requestMode(String mode, long nowMs)
    {
        requestedMode = normalizeMode(mode);
        if (!connected)
        {
            activeMode = MIRROR_2D;
            applied = false;
            transitioning = false;
            message = disconnectedMessage();
            return Action.NONE;
        }
        if (applied && requestedMode.equals(activeMode) && !transitioning)
        {
            message = successMessage();
            return Action.NONE;
        }
        return beginTransition(nowMs);
    }

    synchronized Action setConnected(boolean connected, long nowMs)
    {
        if (!connected)
        {
            this.connected = false;
            activeMode = MIRROR_2D;
            applied = false;
            transitioning = false;
            message = "眼镜已断开；重新连接后会应用所选模式。";
            return Action.NONE;
        }

        boolean wasConnected = this.connected;
        this.connected = true;
        if (wasConnected && transitioning)
        {
            return Action.NONE;
        }
        if (!wasConnected || !applied || !requestedMode.equals(activeMode))
        {
            return beginTransition(nowMs);
        }
        return Action.NONE;
    }

    synchronized Action onCommandResponse(int command, boolean success, long nowMs)
    {
        if (!transitioning || command != expectedCommand())
        {
            return Action.NONE;
        }
        if (!success)
        {
            return fail("眼镜拒绝显示模式切换，已安全回退到镜像 2D。");
        }

        transitioning = false;
        activeMode = requestedMode;
        applied = true;
        message = successMessage();
        return Action.NONE;
    }

    synchronized Action onSdkFailure(long nowMs)
    {
        return fail("眼镜显示模式切换失败，已安全回退到镜像 2D。");
    }

    synchronized Action tick(long nowMs)
    {
        if (transitioning && nowMs >= transitionDeadline)
        {
            return fail("眼镜没有确认显示模式，已安全回退到镜像 2D。");
        }
        return Action.NONE;
    }

    synchronized Action pause()
    {
        transitioning = false;
        activeMode = MIRROR_2D;
        applied = false;
        message = "应用已暂停，眼镜已恢复为 2D 模式。";
        return Action.SWITCH_TO_2D;
    }

    synchronized State snapshot()
    {
        return new State(
                requestedMode,
                activeMode,
                applied,
                transitioning,
                connected,
                message);
    }

    private Action beginTransition(long nowMs)
    {
        transitioning = true;
        applied = false;
        transitionDeadline = nowMs + TRANSITION_TIMEOUT_MS;
        message = STEREO_SCREEN.equals(requestedMode)
                ? "正在切换到左右眼立体画面…"
                : "正在切换到双眼镜像画面…";
        return STEREO_SCREEN.equals(requestedMode)
                ? Action.SWITCH_TO_3D
                : Action.SWITCH_TO_2D;
    }

    private Action fail(String reason)
    {
        transitioning = false;
        activeMode = MIRROR_2D;
        applied = false;
        message = reason;
        return Action.SWITCH_TO_2D;
    }

    private int expectedCommand()
    {
        return STEREO_SCREEN.equals(requestedMode) ? COMMAND_3D : COMMAND_2D;
    }

    private String disconnectedMessage()
    {
        return STEREO_SCREEN.equals(requestedMode)
                ? "立体屏幕已保存，连接眼镜后自动启用。"
                : "镜像 2D 已保存，连接眼镜后自动启用。";
    }

    private String successMessage()
    {
        return STEREO_SCREEN.equals(activeMode)
                ? "立体屏幕已启用：同一 WebView 帧正在左右眼重放。"
                : "镜像 2D 已启用：双眼显示同一幅完整画面。";
    }

    static String normalizeMode(String value)
    {
        String normalized = value == null ? "" : value.trim().toLowerCase(Locale.US);
        if (STEREO_SCREEN.equals(normalized)
                || "stereo".equals(normalized)
                || "3d".equals(normalized))
        {
            return STEREO_SCREEN;
        }
        return MIRROR_2D;
    }
}
