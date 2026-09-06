package com.jellyfinforrayneo.client;

import org.json.JSONObject;
import org.junit.Test;

import java.util.HashMap;
import java.util.Map;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public final class SessionRepositoryTests
{
    @Test
    public void saveWithoutPersistence_KeepsOnlyProcessSession()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);

        repository.save(validSession(), false);

        assertNotNull(repository.getSession());
        assertFalse(repository.isPersisted());
        assertFalse(store.values.containsKey(SessionRepository.KEY_SESSION));
    }

    @Test
    public void clear_RemovesPersistedAndTransientSession()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);

        repository.clear();

        assertNull(repository.getSession());
        assertFalse(repository.isPersisted());
    }

    @Test
    public void restore_InvalidPersistedSessionIsRemoved()
    {
        FakeStore store = new FakeStore();
        store.putString(SessionRepository.KEY_SESSION, "{\"accessToken\":\"only\"}");
        SessionRepository repository = new SessionRepository(store);

        assertNull(repository.getSession());
        assertFalse(store.values.containsKey(SessionRepository.KEY_SESSION));
    }

    @Test
    public void save_PersistsCanonicalWhitelist()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);

        repository.save(validSession(), true);

        assertTrue(repository.isPersisted());
        assertNotNull(new SessionRepository(store).getSession());
        assertFalse(store.values.containsKey(SessionRepository.KEY_SESSION));
    }

    @Test
    public void restore_LegacyPayloadRewritesCanonicalWhitelist() throws Exception
    {
        FakeStore store = new FakeStore();
        JSONObject legacy = validSession().toJsonObject();
        legacy.put("createdAt", 1_700_000_000_000L);
        legacy.put("ignored", "must-not-survive");
        store.putString(SessionRepository.KEY_SESSION, legacy.toString());
        SessionRepository repository = new SessionRepository(store);

        assertNotNull(repository.getSession());

        JSONObject canonical = repository.getSession().toJsonObject();
        assertEquals(1, repository.accountSummaries().length());
        assertFalse(store.values.containsKey(SessionRepository.KEY_SESSION));
        assertNotNull(new SessionRepository(store).getSession());
        assertEquals(8, canonical.length());
        assertFalse(canonical.has("createdAt"));
        assertFalse(canonical.has("ignored"));
    }

    @Test
    public void preferenceNames_RemainCompatibleWithLegacyActivity()
    {
        assertEquals("jellyfin_companion", SessionRepository.PREFERENCES_NAME);
        assertEquals("session_json", SessionRepository.KEY_SESSION);
        assertEquals("device_id", SessionRepository.KEY_DEVICE_ID);
        assertEquals("server_url", SessionRepository.KEY_SERVER_URL);
        assertEquals("username", SessionRepository.KEY_USER_NAME);
        assertEquals("display_mode", SessionRepository.KEY_DISPLAY_MODE);
    }

    @Test
    public void stereoPreference_SurvivesRecreationAndSessionLogout()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        StereoScreenSettings settings = StereoScreenSettings.parse("{\"depthLevel\":3,\"sizePercent\":85}");
        repository.setStereoScreenSettings(settings);
        repository.save(validSession(), true);
        repository.clear();

        SessionRepository restored = new SessionRepository(store);
        assertTrue(settings.sameAs(restored.getStereoScreenSettings()));
        assertNull(restored.getSession());
    }

    @Test
    public void uiTheme_SurvivesRecreationAndLogoutWithoutAffectingSession()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        String accountId = repository.getActiveId();
        repository.setUiTheme(UiTheme.SIMPLE);
        assertEquals(accountId, repository.getActiveId());
        assertNotNull(repository.getSession());

        SessionRepository restored = new SessionRepository(store);
        assertEquals(UiTheme.SIMPLE, restored.getUiTheme());
        restored.clear();
        assertEquals(UiTheme.SIMPLE, new SessionRepository(store).getUiTheme());
        restored.setUiTheme(UiTheme.DEFAULT);
        assertEquals(UiTheme.DEFAULT, new SessionRepository(store).getUiTheme());
    }

    @Test
    public void invalidUiThemeInput_CannotOverwritePreference()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.setUiTheme(UiTheme.SIMPLE);
        for (String value : new String[]{null, "", "simpleui", " simpleUI", "{}", new String(new char[65536])})
        {
            assertFalse(UiTheme.isValid(value));
            repository.setUiTheme(value);
            assertEquals(UiTheme.SIMPLE, repository.getUiTheme());
        }
    }

    @Test
    public void absentOrCorruptUiTheme_RestoresLiquidGlass()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        assertEquals(UiTheme.DEFAULT, repository.getUiTheme());
        store.putString(SessionRepository.KEY_UI_THEME, "unknown-theme");
        assertEquals(UiTheme.DEFAULT, repository.getUiTheme());
    }

    @Test
    public void touchpadBackground_SurvivesRecreationThemeChangeAndLogout()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        String activeId = repository.getActiveId();
        repository.setTouchpadBackground("black");
        assertEquals(activeId, repository.getActiveId());
        assertNotNull(repository.getSession());

        SessionRepository restored = new SessionRepository(store);
        assertEquals("black", restored.getTouchpadBackground());
        restored.setUiTheme(UiTheme.SIMPLE);
        restored.setTouchpadBackground("texture");
        restored.setUiTheme(UiTheme.DEFAULT);
        restored.clear();
        assertEquals("texture", new SessionRepository(store).getTouchpadBackground());
        assertNull(restored.getSession());
    }

    @Test
    public void invalidTouchpadBackground_CannotOverwritePreference()
    {
        SessionRepository repository = new SessionRepository(new FakeStore());
        repository.setTouchpadBackground("black");
        for (String value : new String[]{null, "", "BLACK", " black", "{}", new String(new char[65536])})
        {
            repository.setTouchpadBackground(value);
            assertEquals("black", repository.getTouchpadBackground());
        }
    }

    @Test
    public void absentOrCorruptTouchpadBackground_PreservesThemeDefault()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        assertEquals("texture", repository.getTouchpadBackground());
        repository.setUiTheme(UiTheme.SIMPLE);
        assertEquals("black", repository.getTouchpadBackground());
        store.putString(SessionRepository.KEY_TOUCHPAD_BACKGROUND, "unknown");
        assertEquals("black", repository.getTouchpadBackground());
        repository.setUiTheme(UiTheme.DEFAULT);
        assertEquals("texture", repository.getTouchpadBackground());
    }

    @Test
    public void missingOrCorruptStereoPreference_UsesConservativeDefault()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        assertTrue(StereoScreenSettings.DEFAULT.sameAs(repository.getStereoScreenSettings()));
        store.putString(SessionRepository.KEY_STEREO_SCREEN, "{\"depthLevel\":999,\"sizePercent\":100}");
        assertTrue(StereoScreenSettings.DEFAULT.sameAs(repository.getStereoScreenSettings()));
        repository.setStereoScreenSettings(null);
        assertTrue(StereoScreenSettings.DEFAULT.sameAs(repository.getStereoScreenSettings()));
    }

    @Test
    public void switch_RemembersTwoServersAndRestoresTheSelectedAccount()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        String first = repository.getActiveId();
        repository.save(account("https://other.example.test", "user-id", "other-token"), true);
        String second = repository.getActiveId();

        assertEquals(2, repository.accountSummaries().length());
        assertEquals("Home", repository.activate(first).getServerName());
        assertEquals(first, new SessionRepository(store).getActiveId());
        assertEquals("https://other.example.test", repository.activate(second).getServerUrl());
        assertEquals(second, new SessionRepository(store).getActiveId());
    }

    @Test
    public void save_SameServerSupportsDifferentUsersAndRefreshesExistingLogin()
    {
        SessionRepository repository = new SessionRepository(new FakeStore());
        repository.save(validSession(), true);
        String first = repository.getActiveId();
        repository.save(account("http://jellyfin.local:8096", "another-user", "second-token"), true);
        assertEquals(2, repository.accountSummaries().length());
        repository.save(account("http://jellyfin.local:8096", "user-id", "refreshed-token"), true);

        assertEquals(first, repository.getActiveId());
        assertEquals(2, repository.accountSummaries().length());
        assertTrue(repository.getSession().toJson().contains("refreshed-token"));
    }

    @Test
    public void clear_InvalidActiveLoginDoesNotForgetOtherAccounts()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        String first = repository.getActiveId();
        repository.save(account("https://other.example.test", "other", "other-token"), true);
        String removed = repository.getActiveId();
        repository.clear();

        SessionRepository restored = new SessionRepository(store);
        assertNull(restored.getSession());
        assertEquals(1, restored.accountSummaries().length());
        assertNull(restored.activate(removed));
        assertNotNull(restored.activate(first));
    }

    @Test
    public void remove_InactiveAccountKeepsCurrentConnectionAndPreferences()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        String first = repository.getActiveId();
        repository.save(account("https://other.example.test", "other", "other-token"), true);
        String active = repository.getActiveId();
        assertTrue(repository.remove(first));
        assertEquals(active, repository.getActiveId());
        assertEquals(active, new SessionRepository(store).getActiveId());
        assertEquals(1, repository.accountSummaries().length());
    }

    @Test
    public void ephemeralLogin_CanSwitchInProcessButNeverRestoresAfterRestart()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        String persisted = repository.getActiveId();
        repository.save(account("https://other.example.test", "other", "ephemeral-token"), false);
        String ephemeral = repository.getActiveId();
        repository.activate(persisted);
        assertNotNull(repository.activate(ephemeral));
        assertFalse(store.getString(SessionRepository.KEY_ACCOUNTS, "").contains("ephemeral-token"));

        SessionRepository restored = new SessionRepository(store);
        assertNull(restored.getSession());
        assertEquals(1, restored.accountSummaries().length());
        assertNotNull(restored.activate(persisted));
        assertNull(restored.activate(ephemeral));
    }

    @Test
    public void saveWithoutRemembering_RemovesOlderPersistedTokenForSameAccount()
    {
        FakeStore store = new FakeStore();
        SessionRepository repository = new SessionRepository(store);
        repository.save(validSession(), true);
        repository.save(account("http://jellyfin.local:8096", "user-id", "ephemeral-token"), false);
        assertNotNull(repository.getSession());
        assertEquals(0, new SessionRepository(store).accountSummaries().length());
    }

    @Test
    public void summaries_ExposeOnlyBoundedAccountMetadata() throws Exception
    {
        SessionRepository repository = new SessionRepository(new FakeStore());
        repository.save(validSession(), true);
        JSONObject summary = repository.accountSummaries().getJSONObject(0);
        assertEquals(8, summary.length());
        assertEquals(repository.getActiveId(), summary.getString("id"));
        assertTrue(summary.getBoolean("active"));
        assertTrue(summary.getBoolean("saved"));
        assertFalse(summary.has("accessToken"));
        assertFalse(summary.has("password"));
        assertFalse(summary.has("userId"));
        assertFalse(summary.has("deviceId"));
        assertFalse(summary.toString().contains("access-token"));
    }

    @Test
    public void unknownOrUnboundedId_CannotSwitchOrRemoveAnAccount()
    {
        SessionRepository repository = new SessionRepository(new FakeStore());
        repository.save(validSession(), true);
        String active = repository.getActiveId();
        for (String id : new String[] {null, "", active + "x", "00000000000000000000000000000000"})
        {
            assertNull(repository.activate(id));
            assertFalse(repository.remove(id));
            assertEquals(active, repository.getActiveId());
        }
    }

    @Test
    public void fullAccountList_RejectsNewLoginWithoutEvictingAnyAccount()
    {
        SessionRepository repository = new SessionRepository(new FakeStore());
        for (int index = 0; index < SessionRepository.MAX_ACCOUNTS; index++)
        {
            assertTrue(repository.save(account("https://media.example.test", "user-" + index, "token"), true));
        }
        String active = repository.getActiveId();
        assertFalse(repository.save(validSession(), true));
        assertEquals(active, repository.getActiveId());
        assertEquals(SessionRepository.MAX_ACCOUNTS, repository.accountSummaries().length());
        assertTrue(repository.save(account("https://media.example.test", "user-0", "new-token"), true));
    }

    @Test
    public void corruptRegistry_CannotRestoreLegacyOrArbitraryActiveSession()
    {
        FakeStore store = new FakeStore();
        store.putString(SessionRepository.KEY_SESSION, validSession().toJson());
        store.putString(SessionRepository.KEY_ACCOUNTS, "invalid");
        SessionRepository repository = new SessionRepository(store);
        assertNull(repository.getSession());
        assertEquals(0, repository.accountSummaries().length());
        assertFalse(store.values.containsKey(SessionRepository.KEY_SESSION));
    }

    private static SessionPayload account(String server, String userId, String token)
    {
        SessionPayload session = SessionPayload.create(server, "Media", "10.10", "server-id", token,
                userId, "Test user", "device-id");
        assertNotNull(session);
        return session;
    }

    private static SessionPayload validSession()
    {
        SessionPayload session = SessionPayload.create(
                "http://jellyfin.local:8096",
                "Home",
                "10.10",
                "server-id",
                "access-token",
                "user-id",
                "RayNeo",
                "device-id");
        assertNotNull(session);
        return session;
    }

    private static final class FakeStore implements SessionRepository.Store
    {
        final Map<String, String> values = new HashMap<>();

        @Override
        public String getString(String key, String fallback)
        {
            return values.getOrDefault(key, fallback);
        }

        @Override
        public void putString(String key, String value)
        {
            values.put(key, value);
        }

        @Override
        public void remove(String key)
        {
            values.remove(key);
        }
    }
}
