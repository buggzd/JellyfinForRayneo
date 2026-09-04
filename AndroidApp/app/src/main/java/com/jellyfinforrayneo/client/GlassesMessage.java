package com.jellyfinforrayneo.client;

import org.json.JSONObject;

import java.util.Locale;

final class GlassesMessage
{
    static final int MAX_PAYLOAD_LENGTH = 8_192;
    static final int MAX_SEARCH_QUERY_LENGTH = 48;
    static final long MAX_MEDIA_TICKS = 10_000_000L * 60L * 60L * 24L * 366L;

    enum Type
    {
        MANAGE_LOGIN,
        LOGOUT,
        UNAUTHORIZED,
        PLAYBACK_STATE,
        RUNTIME_STATE,
        SEARCH_STATE
    }

    final Type type;
    final String state;
    final String errorCode;
    final String itemId;
    final String title;
    final String subtitle;
    final String playMethod;
    final String query;
    final long positionTicks;
    final long durationTicks;

    private GlassesMessage(
            Type type,
            String state,
            String errorCode,
            String itemId,
            String title,
            String subtitle,
            String playMethod,
            String query,
            long positionTicks,
            long durationTicks)
    {
        this.type = type;
        this.state = state;
        this.errorCode = errorCode;
        this.itemId = itemId;
        this.title = title;
        this.subtitle = subtitle;
        this.playMethod = playMethod;
        this.query = query;
        this.positionTicks = positionTicks;
        this.durationTicks = durationTicks;
    }

    static GlassesMessage parse(String payload)
    {
        if (payload == null || payload.trim().isEmpty() || payload.length() > MAX_PAYLOAD_LENGTH)
        {
            return null;
        }
        try
        {
            JSONObject source = new JSONObject(payload);
            Type type = parseType(text(source, "type", 32));
            if (type == null)
            {
                return null;
            }
            String state = text(source, "state", 32).toLowerCase(Locale.US);
            if (type == Type.PLAYBACK_STATE && !isPlaybackState(state))
            {
                return null;
            }
            if (type == Type.RUNTIME_STATE && !isRuntimeState(state))
            {
                return null;
            }
            if (type == Type.SEARCH_STATE && !isSearchState(state))
            {
                return null;
            }
            String errorCode = runtimeErrorCode(source, type, state);
            if (errorCode == null)
            {
                return null;
            }
            String query = searchQuery(source, type, state);
            if (query == null)
            {
                return null;
            }
            return new GlassesMessage(
                    type,
                    state,
                    errorCode,
                    text(source, "itemId", 128),
                    text(source, "title", 180),
                    text(source, "subtitle", 240),
                    playMethod(source),
                    query,
                    boundedLong(source, "positionTicks"),
                    boundedLong(source, "durationTicks"));
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    private static Type parseType(String value)
    {
        switch (value.toLowerCase(Locale.US))
        {
            case "manage_login":
                return Type.MANAGE_LOGIN;
            case "logout":
                return Type.LOGOUT;
            case "unauthorized":
                return Type.UNAUTHORIZED;
            case "playback_state":
                return Type.PLAYBACK_STATE;
            case "runtime_state":
                return Type.RUNTIME_STATE;
            case "search_state":
                return Type.SEARCH_STATE;
            default:
                return null;
        }
    }

    private static boolean isPlaybackState(String value)
    {
        return "preparing".equals(value)
                || "buffering".equals(value)
                || "playing".equals(value)
                || "paused".equals(value)
                || "ended".equals(value)
                || "error".equals(value)
                || "stopped".equals(value);
    }

    private static boolean isRuntimeState(String value)
    {
        return "booting".equals(value)
                || "loading".equals(value)
                || "ready".equals(value)
                || "no-session".equals(value)
                || "error".equals(value);
    }

    private static boolean isSearchState(String value)
    {
        return "active".equals(value) || "inactive".equals(value);
    }

    private static String searchQuery(JSONObject source, Type type, String state)
    {
        if (type != Type.SEARCH_STATE || "inactive".equals(state))
        {
            return "";
        }
        Object value = source.opt("query");
        if (value == null || value == JSONObject.NULL)
        {
            return "";
        }
        if (!(value instanceof String))
        {
            return null;
        }
        String normalized = ((String) value).toLowerCase(Locale.US);
        if (normalized.length() > MAX_SEARCH_QUERY_LENGTH)
        {
            return null;
        }
        for (int index = 0; index < normalized.length(); index++)
        {
            char character = normalized.charAt(index);
            boolean allowed = character >= 'a' && character <= 'z'
                    || character >= '0' && character <= '9'
                    || character == ' ';
            if (!allowed)
            {
                return null;
            }
        }
        return normalized;
    }

    private static String runtimeErrorCode(JSONObject source, Type type, String state)
    {
        if (type != Type.RUNTIME_STATE)
        {
            return "none";
        }

        String value = text(source, "errorCode", 32).toLowerCase(Locale.US);
        if (value.isEmpty())
        {
            return "error".equals(state) ? "unknown" : "none";
        }
        if (!isRuntimeErrorCode(value))
        {
            return null;
        }
        if (!"error".equals(state))
        {
            return "none";
        }
        return "none".equals(value) ? "unknown" : value;
    }

    private static boolean isRuntimeErrorCode(String value)
    {
        return "none".equals(value)
                || "network".equals(value)
                || "http".equals(value)
                || "response".equals(value)
                || "unknown".equals(value);
    }

    private static String text(JSONObject source, String key, int maximumLength)
    {
        Object value = source.opt(key);
        if (!(value instanceof String))
        {
            return "";
        }
        String normalized = ((String) value).trim();
        return normalized.length() <= maximumLength
                ? normalized
                : normalized.substring(0, maximumLength);
    }

    private static long boundedLong(JSONObject source, String key)
    {
        Object value = source.opt(key);
        if (!(value instanceof Number))
        {
            return 0L;
        }
        return Math.max(0L, Math.min(MAX_MEDIA_TICKS, ((Number) value).longValue()));
    }

    private static String playMethod(JSONObject source)
    {
        String value = text(source, "playMethod", 32).toLowerCase(Locale.US);
        switch (value)
        {
            case "directplay":
                return "DirectPlay";
            case "directstream":
                return "DirectStream";
            case "transcode":
                return "Transcode";
            default:
                return "";
        }
    }
}
