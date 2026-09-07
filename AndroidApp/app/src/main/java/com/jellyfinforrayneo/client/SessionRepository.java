package com.jellyfinforrayneo.client;

import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

final class SessionRepository
{
    static final String PREFERENCES_NAME = "jellyfin_companion";
    static final String KEY_SESSION = "session_json";
    static final String KEY_ACCOUNTS = "accounts_v1";
    static final int MAX_ACCOUNTS = 12;
    private static final int MAX_ACCOUNTS_JSON_LENGTH = MAX_ACCOUNTS * SessionPayload.MAX_JSON_LENGTH + 4_096;
    static final String KEY_DEVICE_ID = "device_id";
    static final String KEY_SERVER_URL = "server_url";
    static final String KEY_USER_NAME = "username";
    static final String KEY_DISPLAY_MODE = "display_mode";
    static final String KEY_STEREO_SCREEN = "stereo_screen_settings";
    static final String KEY_UI_THEME = "ui_theme";
    static final String KEY_TOUCHPAD_BACKGROUND = "touchpad_background";
    static final String KEY_COMPANION_BACKGROUND_LAYOUT = "companion_background_layout";
    static final String KEY_COMPANION_GLASS_TRANSPARENCY = "companion_glass_transparency";

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
    private final Map<String, Account> accounts = new LinkedHashMap<>();
    private String activeId = "";
    private boolean loaded;

    private static final class Account
    {
        final SessionPayload session;
        final boolean persisted;

        Account(SessionPayload session, boolean persisted)
        {
            this.session = session;
            this.persisted = persisted;
        }
    }

    SessionRepository(SharedPreferences preferences)
    {
        this(new PreferencesStore(preferences));
    }

    SessionRepository(Store store)
    {
        this.store = store;
    }

    private void load()
    {
        if (loaded)
        {
            return;
        }
        loaded = true;
        String stored = store.getString(KEY_ACCOUNTS, "");
        if (!stored.isEmpty() && stored.length() <= MAX_ACCOUNTS_JSON_LENGTH)
        {
            try
            {
                JSONObject root = new JSONObject(stored);
                JSONArray entries = root.getJSONArray("accounts");
                for (int index = 0; index < Math.min(entries.length(), MAX_ACCOUNTS); index++)
                {
                    JSONObject entry = entries.optJSONObject(index);
                    if (entry == null)
                    {
                        continue;
                    }
                    String id = entry.optString("id", "");
                    SessionPayload session = SessionPayload.fromJson(entry.optString("session", ""));
                    if (validAccountId(id) && session != null && findAccount(session).isEmpty())
                    {
                        accounts.put(id, new Account(session, true));
                    }
                }
                String restoredId = root.optString("activeId", "");
                activeId = accounts.containsKey(restoredId) ? restoredId : "";
            }
            catch (Exception ignored)
            {
                accounts.clear();
            }
        }
        if (stored.isEmpty())
        {
            SessionPayload legacy = SessionPayload.fromJson(store.getString(KEY_SESSION, ""));
            if (legacy != null)
            {
                activeId = newAccountId();
                accounts.put(activeId, new Account(legacy, true));
            }
        }
        persistAccounts();
        store.remove(KEY_SESSION);
    }

    private void persistAccounts()
    {
        JSONObject root = new JSONObject();
        JSONArray entries = new JSONArray();
        try
        {
            for (Map.Entry<String, Account> item : accounts.entrySet())
            {
                if (item.getValue().persisted)
                {
                    JSONObject entry = new JSONObject();
                    entry.put("id", item.getKey());
                    entry.put("session", item.getValue().session.toJson());
                    entries.put(entry);
                }
            }
            Account active = accounts.get(activeId);
            root.put("activeId", active != null && active.persisted ? activeId : "");
            root.put("accounts", entries);
            store.putString(KEY_ACCOUNTS, root.toString());
        }
        catch (Exception ignored)
        {
            throw new IllegalStateException("Unable to save validated accounts.");
        }
    }

    private String findAccount(SessionPayload session)
    {
        for (Map.Entry<String, Account> item : accounts.entrySet())
        {
            SessionPayload existing = item.getValue().session;
            if (existing.getServerUrl().equals(session.getServerUrl())
                    && existing.getUserId().equals(session.getUserId()))
            {
                return item.getKey();
            }
        }
        return "";
    }

    private static String newAccountId()
    {
        return UUID.randomUUID().toString().replace("-", "");
    }

    static boolean validAccountId(String id)
    {
        return id != null && id.matches("[a-f0-9]{32}");
    }

    synchronized SessionPayload getSession()
    {
        load();
        Account active = accounts.get(activeId);
        return active == null ? null : active.session;
    }

    synchronized String getActiveId()
    {
        load();
        return activeId;
    }

    synchronized boolean save(SessionPayload session, boolean persist)
    {
        if (session == null)
        {
            throw new IllegalArgumentException("A validated session is required.");
        }
        load();
        String id = findAccount(session);
        if (id.isEmpty())
        {
            if (accounts.size() >= MAX_ACCOUNTS)
            {
                return false;
            }
            id = newAccountId();
        }
        accounts.put(id, new Account(session, persist));
        activate(id);
        return true;
    }

    synchronized SessionPayload activate(String id)
    {
        load();
        Account account = validAccountId(id) ? accounts.get(id) : null;
        if (account == null)
        {
            return null;
        }
        activeId = id;
        store.putString(KEY_SERVER_URL, account.session.getServerUrl());
        store.putString(KEY_USER_NAME, account.session.getUserName());
        persistAccounts();
        return account.session;
    }

    synchronized boolean remove(String id)
    {
        load();
        if (!validAccountId(id) || accounts.remove(id) == null)
        {
            return false;
        }
        if (activeId.equals(id))
        {
            activeId = "";
        }
        persistAccounts();
        return true;
    }

    synchronized void clear()
    {
        load();
        remove(activeId);
    }

    synchronized boolean hasSession()
    {
        return getSession() != null;
    }

    synchronized boolean isPersisted()
    {
        load();
        Account active = accounts.get(activeId);
        return active != null && active.persisted;
    }

    // Phone metadata is deliberately independent of the credential-bearing session JSON.
    synchronized JSONArray accountSummaries()
    {
        load();
        JSONArray result = new JSONArray();
        for (Map.Entry<String, Account> item : accounts.entrySet())
        {
            Account account = item.getValue();
            SessionPayload session = account.session;
            JSONObject summary = new JSONObject();
            try
            {
                summary.put("id", item.getKey());
                summary.put("serverUrl", session.getServerUrl());
                summary.put("serverName", session.getServerName());
                summary.put("serverVersion", session.getServerVersion());
                summary.put("serverId", session.getServerId());
                summary.put("username", session.getUserName());
                summary.put("saved", account.persisted);
                summary.put("active", activeId.equals(item.getKey()));
                result.put(summary);
            }
            catch (Exception ignored)
            {
                // Only validated, fixed-shape metadata is published.
            }
        }
        return result;
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

    String getUiTheme()
    {
        return UiTheme.normalize(store.getString(KEY_UI_THEME, UiTheme.DEFAULT));
    }

    void setUiTheme(String theme)
    {
        if (UiTheme.isValid(theme))
        {
            store.putString(KEY_UI_THEME, theme);
        }
    }

    String getTouchpadBackground()
    {
        return CompanionSettingsPolicy.touchpadBackground(
                store.getString(KEY_TOUCHPAD_BACKGROUND, ""), getUiTheme());
    }

    void setTouchpadBackground(String background)
    {
        if (CompanionSettingsPolicy.isTouchpadBackground(background))
        {
            store.putString(KEY_TOUCHPAD_BACKGROUND, background);
        }
    }

    int getCompanionGlassTransparency()
    {
        Integer value = CompanionSettingsPolicy.parseGlassTransparency(
                store.getString(KEY_COMPANION_GLASS_TRANSPARENCY, ""));
        return value == null ? CompanionSettingsPolicy.DEFAULT_GLASS_TRANSPARENCY : value;
    }

    void setCompanionGlassTransparency(int value)
    {
        if (value >= 0 && value <= 100)
        {
            store.putString(KEY_COMPANION_GLASS_TRANSPARENCY, Integer.toString(value));
        }
    }

    CompanionBackgroundLayout getCompanionBackgroundLayout()
    {
        CompanionBackgroundLayout layout = CompanionBackgroundLayout.parse(store.getString(KEY_COMPANION_BACKGROUND_LAYOUT, ""));
        return layout == null ? CompanionBackgroundLayout.DEFAULT : layout;
    }

    void setCompanionBackgroundLayout(CompanionBackgroundLayout layout)
    {
        if (layout != null)
        {
            store.putString(KEY_COMPANION_BACKGROUND_LAYOUT, layout.toJson().toString());
        }
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
