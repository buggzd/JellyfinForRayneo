package com.jellyfinforrayneo.client;

import org.json.JSONObject;

import java.net.Inet6Address;
import java.net.InetAddress;
import java.net.URI;
import java.util.Locale;

final class SessionPayload
{
    static final int MAX_JSON_LENGTH = 16_384;
    static final int MAX_SERVER_URL_LENGTH = 2_048;
    static final int MAX_SERVER_NAME_LENGTH = 512;
    static final int MAX_SERVER_VERSION_LENGTH = 128;
    static final int MAX_IDENTIFIER_LENGTH = 512;
    static final int MAX_ACCESS_TOKEN_LENGTH = 4_096;
    static final int MAX_USER_NAME_LENGTH = 512;

    private final String serverUrl;
    private final String serverName;
    private final String serverVersion;
    private final String serverId;
    private final String accessToken;
    private final String userId;
    private final String userName;
    private final String deviceId;

    private SessionPayload(
            String serverUrl,
            String serverName,
            String serverVersion,
            String serverId,
            String accessToken,
            String userId,
            String userName,
            String deviceId)
    {
        this.serverUrl = serverUrl;
        this.serverName = serverName;
        this.serverVersion = serverVersion;
        this.serverId = serverId;
        this.accessToken = accessToken;
        this.userId = userId;
        this.userName = userName;
        this.deviceId = deviceId;
    }

    static SessionPayload create(
            String serverUrl,
            String serverName,
            String serverVersion,
            String serverId,
            String accessToken,
            String userId,
            String userName,
            String deviceId)
    {
        try
        {
            String normalizedServerUrl = normalizeServerUrl(serverUrl);
            String normalizedServerName = normalize(serverName);
            String normalizedServerVersion = normalize(serverVersion);
            String normalizedServerId = normalize(serverId);
            String normalizedAccessToken = normalize(accessToken);
            String normalizedUserId = normalize(userId);
            String normalizedUserName = normalize(userName);
            String normalizedDeviceId = normalize(deviceId);

            if (normalizedServerUrl.length() > MAX_SERVER_URL_LENGTH
                    || normalizedServerName.length() > MAX_SERVER_NAME_LENGTH
                    || normalizedServerVersion.length() > MAX_SERVER_VERSION_LENGTH
                    || normalizedServerId.length() > MAX_IDENTIFIER_LENGTH
                    || normalizedAccessToken.isEmpty()
                    || normalizedAccessToken.length() > MAX_ACCESS_TOKEN_LENGTH
                    || normalizedUserId.isEmpty()
                    || normalizedUserId.length() > MAX_IDENTIFIER_LENGTH
                    || normalizedUserName.length() > MAX_USER_NAME_LENGTH
                    || normalizedDeviceId.isEmpty()
                    || normalizedDeviceId.length() > MAX_IDENTIFIER_LENGTH)
            {
                return null;
            }

            return new SessionPayload(
                    normalizedServerUrl,
                    normalizedServerName,
                    normalizedServerVersion,
                    normalizedServerId,
                    normalizedAccessToken,
                    normalizedUserId,
                    normalizedUserName,
                    normalizedDeviceId);
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    static SessionPayload fromJson(String value)
    {
        if (value == null || value.trim().isEmpty() || value.length() > MAX_JSON_LENGTH)
        {
            return null;
        }

        try
        {
            JSONObject source = new JSONObject(value);
            return create(
                    stringValue(source, "serverUrl"),
                    stringValue(source, "serverName"),
                    stringValue(source, "serverVersion"),
                    stringValue(source, "serverId"),
                    stringValue(source, "accessToken"),
                    stringValue(source, "userId"),
                    stringValue(source, "userName"),
                    stringValue(source, "deviceId"));
        }
        catch (Exception ignored)
        {
            return null;
        }
    }

    static String normalizeServerUrl(String value) throws Exception
    {
        String candidate = normalize(value);
        if (candidate.isEmpty())
        {
            throw new IllegalArgumentException("A Jellyfin server URL is required.");
        }
        if (!candidate.regionMatches(true, 0, "http://", 0, 7)
                && !candidate.regionMatches(true, 0, "https://", 0, 8))
        {
            if (candidate.contains("://"))
            {
                throw new IllegalArgumentException("Unsupported Jellyfin server URL scheme.");
            }
            candidate = "http://" + candidate;
        }
        candidate = bracketBareIpv6Authority(candidate);

        URI uri = new URI(candidate);
        String scheme = uri.getScheme();
        String rawAuthority = uri.getRawAuthority();
        if (scheme == null
                || (!("http".equalsIgnoreCase(scheme)) && !("https".equalsIgnoreCase(scheme)))
                || uri.getHost() == null
                || uri.getHost().isEmpty()
                || uri.getUserInfo() != null
                || rawAuthority == null
                || rawAuthority.contains("%")
                || uri.getRawQuery() != null
                || uri.getRawFragment() != null
                || uri.getPort() > 65_535)
        {
            throw new IllegalArgumentException("Invalid Jellyfin server URL.");
        }

        String path = uri.getRawPath() == null ? "" : uri.getRawPath();
        while (path.endsWith("/") && !path.isEmpty())
        {
            path = path.substring(0, path.length() - 1);
        }
        String normalized = scheme.toLowerCase(Locale.US) + "://" + rawAuthority + path;
        if (normalized.length() > MAX_SERVER_URL_LENGTH)
        {
            throw new IllegalArgumentException("Jellyfin server URL is too long.");
        }
        return normalized;
    }

    private static String bracketBareIpv6Authority(String value) throws Exception
    {
        int separator = value.indexOf("://");
        int authorityStart = separator < 0 ? 0 : separator + 3;
        int authorityEnd = value.length();
        for (char delimiter : new char[] {'/', '?', '#'})
        {
            int index = value.indexOf(delimiter, authorityStart);
            if (index >= 0 && index < authorityEnd)
            {
                authorityEnd = index;
            }
        }

        String authority = value.substring(authorityStart, authorityEnd);
        if (authority.startsWith("[")
                || authority.indexOf('@') >= 0
                || count(authority, ':') < 2)
        {
            return value;
        }
        if (authority.indexOf('%') >= 0)
        {
            throw new IllegalArgumentException("Scoped IPv6 URLs are not supported by WebView.");
        }

        InetAddress address = InetAddress.getByName(authority);
        if (!(address instanceof Inet6Address))
        {
            return value;
        }
        return value.substring(0, authorityStart)
                + "[" + authority + "]"
                + value.substring(authorityEnd);
    }

    private static int count(String value, char target)
    {
        int result = 0;
        for (int index = 0; index < value.length(); index++)
        {
            if (value.charAt(index) == target)
            {
                result++;
            }
        }
        return result;
    }

    JSONObject toJsonObject()
    {
        JSONObject result = new JSONObject();
        try
        {
            result.put("serverUrl", serverUrl);
            result.put("serverName", serverName);
            result.put("serverVersion", serverVersion);
            result.put("serverId", serverId);
            result.put("accessToken", accessToken);
            result.put("userId", userId);
            result.put("userName", userName);
            result.put("deviceId", deviceId);
        }
        catch (Exception ignored)
        {
            return new JSONObject();
        }
        return result;
    }

    String toJson()
    {
        return toJsonObject().toString();
    }

    String getServerUrl()
    {
        return serverUrl;
    }

    String getServerName()
    {
        return serverName;
    }

    String getServerVersion()
    {
        return serverVersion;
    }

    String getServerId()
    {
        return serverId;
    }

    String getUserName()
    {
        return userName;
    }

    private static String stringValue(JSONObject source, String key)
    {
        Object value = source.opt(key);
        return value instanceof String ? normalize((String) value) : "";
    }

    private static String normalize(String value)
    {
        return value == null ? "" : value.trim();
    }

    @Override
    public String toString()
    {
        return "SessionPayload{serverConfigured=" + (!serverUrl.isEmpty())
                + ", userIdPresent=" + (!userId.isEmpty())
                + ", tokenPresent=" + (!accessToken.isEmpty()) + "}";
    }
}
