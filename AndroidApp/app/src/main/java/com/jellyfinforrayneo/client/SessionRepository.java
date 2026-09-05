package com.jellyfinforrayneo.client;

import android.content.SharedPreferences;

import java.util.UUID;

final class SessionRepository
{
    static final String PREFERENCES_NAME = "jellyfin_companion";
    static final String KEY_SESSION = "session_json";
    static final String KEY_DEVICE_ID = "device_id";
    static final String KEY_SERVER_URL = "server_url";
    static final String KEY_USER_NAME = "username";
    static final String KEY_DISPLAY_MODE = "display_mode";
    static final String KEY_STEREO_SCREEN = "stereo_screen_settings";

    interface Store
    {
        String getString(String key, String fallback);

        void putString(String key, String value);

        void remove(String key);
    }

    private static final class PreferencesStore implements Store
    {
        private final SharedPreferences preferences;

        PreferencesStore(SharedPreferences preferences)
        {
            this.preferences = preferences;
        }

        @Override
        public String getString(String key, String fallback)
        {
            return preferences.getString(key, fallback);
        }

        @Override
        public void putString(String key, String value)
        {
            preferences.edit().putString(key, value).apply();
        }

        @Override
        public void remove(String key)
        {
            preferences.edit().remove(key).apply();
        }
    }

    private final Store store;
    private SessionPayload transientSession;

    SessionRepository(SharedPreferences preferences)
    {
        this(new PreferencesStore(preferences));
    }

    SessionRepository(Store store)
    {
        this.store = store;
    }

    synchronized SessionPayload getSession()
    {
        if (transientSession != null)
        {
            return transientSession;
        }

        String stored = store.getString(KEY_SESSION, "");
        SessionPayload restored = SessionPayload.fromJson(stored);
        if (restored == null && !stored.isEmpty())
        {
            store.remove(KEY_SESSION);
        }
        else if (restored != null)
        {
            transientSession = restored;
            String canonical = restored.toJson();
            if (!canonical.equals(stored))
            {
                store.putString(KEY_SESSION, canonical);
            }
        }
        return restored;
    }

    synchronized void save(SessionPayload session, boolean persist)
    {
        if (session == null)
        {
            throw new IllegalArgumentException("A validated session is required.");
        }

        transientSession = session;
        store.putString(KEY_SERVER_URL, session.getServerUrl());
        store.putString(KEY_USER_NAME, session.getUserName());
        if (persist)
        {
            store.putString(KEY_SESSION, session.toJson());
        }
        else
        {
            store.remove(KEY_SESSION);
        }
    }

    synchronized void clear()
    {
        transientSession = null;
        store.remove(KEY_SESSION);
    }

    synchronized boolean hasSession()
    {
        return getSession() != null;
    }

    synchronized boolean isPersisted()
    {
        return SessionPayload.fromJson(store.getString(KEY_SESSION, "")) != null;
    }

    synchronized String getOrCreateDeviceId()
    {
        String existing = store.getString(KEY_DEVICE_ID, "").trim();
        if (!existing.isEmpty() && existing.length() <= SessionPayload.MAX_IDENTIFIER_LENGTH)
        {
            return existing;
        }
        String created = UUID.randomUUID().toString().replace("-", "");
        store.putString(KEY_DEVICE_ID, created);
        return created;
    }

    String getServerHint()
    {
        return store.getString(KEY_SERVER_URL, "");
    }

    String getUserNameHint()
    {
        return store.getString(KEY_USER_NAME, "");
    }

    void setServerHint(String serverUrl)
    {
        store.putString(KEY_SERVER_URL, serverUrl == null ? "" : serverUrl);
    }

    void setUserNameHint(String userName)
    {
        store.putString(KEY_USER_NAME, userName == null ? "" : userName);
    }

    String getDisplayMode()
    {
        return DisplayModeStateMachine.normalizeMode(
                store.getString(KEY_DISPLAY_MODE, DisplayModeStateMachine.MIRROR_2D));
    }

    void setDisplayMode(String mode)
    {
        store.putString(KEY_DISPLAY_MODE, DisplayModeStateMachine.normalizeMode(mode));
    }

    StereoScreenSettings getStereoScreenSettings()
    {
        StereoScreenSettings settings = StereoScreenSettings.parse(store.getString(KEY_STEREO_SCREEN, ""));
        return settings == null ? StereoScreenSettings.DEFAULT : settings;
    }

    void setStereoScreenSettings(StereoScreenSettings settings)
    {
        if (settings != null)
        {
            store.putString(KEY_STEREO_SCREEN, settings.toJson().toString());
        }
    }
}
