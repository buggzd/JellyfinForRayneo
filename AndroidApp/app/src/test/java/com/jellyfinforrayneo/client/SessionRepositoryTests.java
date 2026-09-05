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
        assertNotNull(SessionPayload.fromJson(
                store.getString(SessionRepository.KEY_SESSION, "")));
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

        JSONObject canonical = new JSONObject(
                store.getString(SessionRepository.KEY_SESSION, ""));
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
