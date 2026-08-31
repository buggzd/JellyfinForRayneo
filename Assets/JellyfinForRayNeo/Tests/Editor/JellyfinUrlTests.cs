using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JellyfinForRayNeo.Tests
{
    public sealed class JellyfinUrlTests
    {
        [Test]
        public void NormalizeServerUrl_AddsHttpSchemeAndRemovesTrailingSlash()
        {
            Assert.AreEqual("http://192.168.1.20:8096", JellyfinUrl.NormalizeServerUrl("192.168.1.20:8096/"));
        }

        [Test]
        public void NormalizeServerUrl_PreservesReverseProxySubpath()
        {
            Assert.AreEqual("https://media.example.com/jellyfin", JellyfinUrl.NormalizeServerUrl("https://media.example.com/jellyfin/"));
            Assert.AreEqual(
                "https://media.example.com/jellyfin/System/Info/Public",
                JellyfinUrl.Combine("https://media.example.com/jellyfin", "/System/Info/Public"));
        }

        [Test]
        public void Combine_PreservesAbsoluteTranscodingUrlAndQuery()
        {
            Assert.AreEqual(
                "https://stream.example.com/Videos/item/master.m3u8?mediaSourceId=source-1",
                JellyfinUrl.Combine(
                    "https://media.example.com/jellyfin",
                    "https://stream.example.com/Videos/item/master.m3u8?mediaSourceId=source-1"));
        }

        [Test]
        public void Combine_RejectsUnsupportedAbsoluteScheme()
        {
            Assert.Throws<ArgumentException>(() =>
                JellyfinUrl.Combine("https://media.example.com", "file:///tmp/video.mp4"));
        }

        [Test]
        public void NormalizeServerUrl_RejectsQueryAndFragments()
        {
            Assert.Throws<ArgumentException>(() => JellyfinUrl.NormalizeServerUrl("https://media.example.com?token=nope"));
            Assert.Throws<ArgumentException>(() => JellyfinUrl.NormalizeServerUrl("https://media.example.com/#part"));
        }

        [Test]
        public void WithQuery_EscapesValuesAndAppendsToExistingQuery()
        {
            string result = JellyfinUrl.WithQuery(
                "https://media.example.com/Items?limit=10",
                new[]
                {
                    new KeyValuePair<string, string>("searchTerm", "银河 剧场"),
                    new KeyValuePair<string, string>("includeItemTypes", "Movie,Series")
                });

            StringAssert.Contains("&searchTerm=%E9%93%B6%E6%B2%B3%20%E5%89%A7%E5%9C%BA", result);
            StringAssert.Contains("&includeItemTypes=Movie%2CSeries", result);
        }

        [Test]
        public void AuthorizationHeader_ContainsStableClientIdentityAndOptionalToken()
        {
            string header = JellyfinUrl.BuildAuthorizationHeader("device-123", "token-456");
            StringAssert.StartsWith("MediaBrowser ", header);
            StringAssert.Contains("Client=\"Jellyfin for RayNeo\"", header);
            StringAssert.Contains("DeviceId=\"device-123\"", header);
            StringAssert.Contains("Token=\"token-456\"", header);
        }
    }
}
