using System;
using System.Collections.Generic;
using System.Text;

namespace JellyfinForRayNeo
{
    public static class JellyfinUrl
    {
        public static string NormalizeServerUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("请输入 Jellyfin 服务器地址。", nameof(input));
            }

            string candidate = input.Trim();
            if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                candidate = "http://" + candidate;
            }

            Uri uri;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new ArgumentException("服务器地址必须是有效的 HTTP 或 HTTPS 地址。", nameof(input));
            }

            if (!string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new ArgumentException("服务器地址不能包含账号、查询参数或片段。", nameof(input));
            }

            string path = uri.AbsolutePath.TrimEnd('/');
            if (path == "/")
            {
                path = string.Empty;
            }

            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + path;
        }

        public static string Combine(string serverUrl, string path)
        {
            string candidate = (path ?? string.Empty).Trim();
            Uri absolute;
            if (!candidate.StartsWith("/", StringComparison.Ordinal)
                && Uri.TryCreate(candidate, UriKind.Absolute, out absolute))
            {
                if ((absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
                    || string.IsNullOrWhiteSpace(absolute.Host))
                {
                    throw new ArgumentException("媒体地址必须使用 HTTP 或 HTTPS。", nameof(path));
                }

                return absolute.AbsoluteUri;
            }

            return NormalizeServerUrl(serverUrl) + "/" + candidate.TrimStart('/');
        }

        public static string WithQuery(string url, IEnumerable<KeyValuePair<string, string>> parameters)
        {
            StringBuilder builder = new StringBuilder(url);
            bool hasQuery = url.IndexOf('?') >= 0;
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                builder.Append(hasQuery ? '&' : '?');
                hasQuery = true;
                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(pair.Value));
            }
            return builder.ToString();
        }

        public static string AppendApiKey(string url, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)
                || url.IndexOf("api_key=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url;
            }

            return WithQuery(url, new[]
            {
                new KeyValuePair<string, string>("api_key", accessToken)
            });
        }

        public static string BuildAuthorizationHeader(string deviceId, string token = null)
        {
            string header = string.Format(
                "MediaBrowser Client=\"{0}\", Device=\"{1}\", DeviceId=\"{2}\", Version=\"{3}\"",
                SanitizeHeaderValue(AppConstants.ClientName),
                SanitizeHeaderValue(AppConstants.DeviceName),
                SanitizeHeaderValue(deviceId),
                SanitizeHeaderValue(AppConstants.ClientVersion));

            if (!string.IsNullOrWhiteSpace(token))
            {
                header += ", Token=\"" + SanitizeHeaderValue(token) + "\"";
            }
            return header;
        }

        private static string SanitizeHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (character >= 32 && character != 127 && character != '"' && character != '\\')
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }
    }
}
