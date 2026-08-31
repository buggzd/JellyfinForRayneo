using UnityEngine;

namespace JellyfinForRayNeo
{
    public static class AppConstants
    {
        public const string ClientName = "Jellyfin for RayNeo";
        public const string ClientVersion = "0.1.0";
        public const string DefaultDeviceName = "RayNeo Air";
        public const long TicksPerSecond = 10_000_000L;

        public static string DeviceName
        {
            get
            {
                string model = SystemInfo.deviceModel;
                return string.IsNullOrWhiteSpace(model) ? DefaultDeviceName : model;
            }
        }
    }
}

