using Newtonsoft.Json;
using NUnit.Framework;

namespace JellyfinForRayNeo.Tests
{
    public sealed class JellyfinModelTests
    {
        [Test]
        public void AuthenticationResponse_DeserializesUserAndToken()
        {
            const string json = "{\"User\":{\"Id\":\"user-id\",\"Name\":\"Ray User\"},\"AccessToken\":\"access-token\",\"ServerId\":\"server-id\"}";
            JellyfinAuthenticationResult result = JsonConvert.DeserializeObject<JellyfinAuthenticationResult>(json);

            Assert.NotNull(result);
            Assert.NotNull(result.User);
            Assert.AreEqual("user-id", result.User.Id);
            Assert.AreEqual("Ray User", result.User.Name);
            Assert.AreEqual("access-token", result.AccessToken);
        }

        [Test]
        public void ItemResponse_DeserializesPosterAndResumeMetadata()
        {
            const string json = "{\"Id\":\"item-id\",\"Name\":\"Example\",\"Type\":\"Movie\",\"MediaType\":\"Video\",\"ImageTags\":{\"Primary\":\"image-tag\"},\"UserData\":{\"PlaybackPositionTicks\":120000000,\"PlayedPercentage\":42.5}}";
            JellyfinItem item = JsonConvert.DeserializeObject<JellyfinItem>(json);

            Assert.AreEqual("image-tag", item.ImageTags["Primary"]);
            Assert.AreEqual(120000000L, item.UserData.PlaybackPositionTicks);
            Assert.AreEqual(42.5d, item.UserData.PlayedPercentage.Value, 0.001d);
            Assert.IsTrue(item.IsPlayable);
        }

        [Test]
        public void ItemResponse_DeserializesExpandedDetailMetadata()
        {
            const string json = "{\"OriginalTitle\":\"Original\",\"Tags\":[\"Drama\"],\"ProductionLocations\":[\"Japan\"],\"Studios\":[{\"Name\":\"Studio A\"}],\"People\":[{\"Name\":\"Director A\",\"Type\":\"Director\"}],\"ProviderIds\":{\"Tmdb\":\"123\"},\"CriticRating\":91,\"PremiereDate\":\"2025-04-01T00:00:00.0000000Z\",\"MediaSources\":[{\"MediaStreams\":[{\"Type\":\"Video\",\"Codec\":\"h264\",\"Width\":1920,\"Height\":1080,\"VideoRange\":\"SDR\"}]}]}";
            JellyfinItem item = JsonConvert.DeserializeObject<JellyfinItem>(json);

            Assert.AreEqual("Original", item.OriginalTitle);
            Assert.AreEqual("Drama", item.Tags[0]);
            Assert.AreEqual("Studio A", item.Studios[0].Name);
            Assert.AreEqual("Director", item.People[0].Type);
            Assert.AreEqual("123", item.ProviderIds["Tmdb"]);
            Assert.AreEqual(1920, item.MediaSources[0].MediaStreams[0].Width);
            Assert.AreEqual("SDR", item.MediaSources[0].MediaStreams[0].VideoRange);
        }

        [Test]
        public void OverviewMarkup_IsConvertedToReadablePlainText()
        {
            const string overview = "<p>第一行<br>第二行 &amp; 内容</p><ul><li>条目一</li><li>条目二</li></ul>";

            string result = JellyfinText.ToPlainText(overview);

            Assert.AreEqual("第一行\n第二行 & 内容\n• 条目一\n• 条目二", result);
            StringAssert.DoesNotContain("<br>", result);
        }

        [Test]
        public void RayNeoPlaybackProfile_RequestsAndroidFriendlyHlsFallback()
        {
            JellyfinDeviceProfile profile = JellyfinDeviceProfile.CreateRayNeoAirProfile(20_000_000);

            Assert.AreEqual("RayNeo Air / Unity Android", profile.Name);
            Assert.AreEqual("mp4,m4v,mov", profile.DirectPlayProfiles[0].Container);
            Assert.AreEqual("hls", profile.TranscodingProfiles[0].Protocol);
            Assert.AreEqual("h264", profile.TranscodingProfiles[0].VideoCodec);
            Assert.AreEqual("2", profile.TranscodingProfiles[0].MaxAudioChannels);
        }

        [Test]
        public void SessionRequiresServerTokenUserAndDevice()
        {
            JellyfinSession session = new JellyfinSession
            {
                ServerUrl = "http://localhost:8096",
                AccessToken = "token",
                UserId = "user",
                DeviceId = "device"
            };

            Assert.IsTrue(session.IsValid);
            session.AccessToken = string.Empty;
            Assert.IsFalse(session.IsValid);
        }
    }
}
