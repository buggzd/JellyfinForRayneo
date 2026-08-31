using System;
using UnityEngine;

namespace JellyfinForRayNeo
{
    public sealed class JellyfinSessionStore
    {
        private const string Prefix = "JellyfinForRayNeo.Session.";
        private const string DeviceIdKey = Prefix + "DeviceId";
        private const string ServerUrlKey = Prefix + "ServerUrl";
        private const string ServerNameKey = Prefix + "ServerName";
        private const string ServerVersionKey = Prefix + "ServerVersion";
        private const string ServerIdKey = Prefix + "ServerId";
        private const string AccessTokenKey = Prefix + "AccessToken";
        private const string UserIdKey = Prefix + "UserId";
        private const string UserNameKey = Prefix + "UserName";

        public string GetOrCreateDeviceId()
        {
            string existing = PlayerPrefs.GetString(DeviceIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            string created = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceIdKey, created);
            PlayerPrefs.Save();
            return created;
        }

        public bool TryLoad(out JellyfinSession session)
        {
            session = new JellyfinSession
            {
                DeviceId = GetOrCreateDeviceId(),
                ServerUrl = PlayerPrefs.GetString(ServerUrlKey, string.Empty),
                ServerName = PlayerPrefs.GetString(ServerNameKey, string.Empty),
                ServerVersion = PlayerPrefs.GetString(ServerVersionKey, string.Empty),
                ServerId = PlayerPrefs.GetString(ServerIdKey, string.Empty),
                AccessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty),
                UserId = PlayerPrefs.GetString(UserIdKey, string.Empty),
                UserName = PlayerPrefs.GetString(UserNameKey, string.Empty)
            };
            return session.IsValid;
        }

        public void Save(JellyfinSession session)
        {
            if (session == null || !session.IsValid)
            {
                throw new ArgumentException("A valid Jellyfin session is required.", nameof(session));
            }

            PlayerPrefs.SetString(DeviceIdKey, session.DeviceId);
            PlayerPrefs.SetString(ServerUrlKey, session.ServerUrl);
            PlayerPrefs.SetString(ServerNameKey, session.ServerName ?? string.Empty);
            PlayerPrefs.SetString(ServerVersionKey, session.ServerVersion ?? string.Empty);
            PlayerPrefs.SetString(ServerIdKey, session.ServerId ?? string.Empty);
            // Passwords are never stored. The access token is kept locally for MVP session restore.
            PlayerPrefs.SetString(AccessTokenKey, session.AccessToken);
            PlayerPrefs.SetString(UserIdKey, session.UserId);
            PlayerPrefs.SetString(UserNameKey, session.UserName ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void ClearSession()
        {
            PlayerPrefs.DeleteKey(ServerUrlKey);
            PlayerPrefs.DeleteKey(ServerNameKey);
            PlayerPrefs.DeleteKey(ServerVersionKey);
            PlayerPrefs.DeleteKey(ServerIdKey);
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.DeleteKey(UserNameKey);
            PlayerPrefs.Save();
        }
    }
}

