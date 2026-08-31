using System;

namespace JellyfinForRayNeo
{
    public sealed class JellyfinApiException : Exception
    {
        public long StatusCode { get; private set; }
        public string Endpoint { get; private set; }
        public string ResponseBody { get; private set; }

        public bool IsUnauthorized
        {
            get { return StatusCode == 401 || StatusCode == 403; }
        }

        public JellyfinApiException(string message, long statusCode, string endpoint, string responseBody = null, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            Endpoint = endpoint;
            ResponseBody = responseBody;
        }
    }
}

