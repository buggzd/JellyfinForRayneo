namespace JellyfinForRayNeo
{
    public sealed class JellyfinSession
    {
        public string ServerUrl;
        public string ServerName;
        public string ServerVersion;
        public string ServerId;
        public string AccessToken;
        public string UserId;
        public string UserName;
        public string DeviceId;

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ServerUrl)
                    && !string.IsNullOrWhiteSpace(AccessToken)
                    && !string.IsNullOrWhiteSpace(UserId)
                    && !string.IsNullOrWhiteSpace(DeviceId);
            }
        }
    }
}

