using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace JellyfinForRayNeo.Tests
{
    public sealed class PlaybackLadderTests
    {
        [Test]
        public void PlaybackLadder_OrdersHardwareSoftwareAndServerFallbacks()
        {
            PlaybackCapabilities capabilities = PlaybackCapabilities.Create(
                new[] { "h264", "hevc" },
                new[] { "aac", "mp3" },
                true);
            JellyfinMediaSource source = CreateSource("mp4", "h264", "aac");

            List<PlaybackTier> tiers = PlaybackLadder.SelectTiers(
                source,
                source,
                capabilities,
                1,
                -1);

            CollectionAssert.AreEqual(
                new[]
                {
                    PlaybackTier.HardwareDirect,
                    PlaybackTier.HardwareLibVlcDirect,
                    PlaybackTier.SoftwareDirect,
                    PlaybackTier.ServerTranscode
                },
                tiers);
        }

        [Test]
        public void PlaybackLadder_UsesSoftwareBeforeServerForMkv()
        {
            PlaybackCapabilities capabilities = PlaybackCapabilities.Create(
                new[] { "h264" },
                new[] { "aac" },
                true);
            JellyfinMediaSource source = CreateSource("mkv", "hevc", "dts");

            List<PlaybackTier> tiers = PlaybackLadder.SelectTiers(
                source,
                source,
                capabilities,
                1,
                -1);

            CollectionAssert.AreEqual(
                new[]
                {
                    PlaybackTier.SoftwareDirect,
                    PlaybackTier.ServerTranscode
                },
                tiers);
        }

        [Test]
        public void PlaybackLadder_UsesLibVlcHardwareBeforeSoftwareForMkv()
        {
            PlaybackCapabilities capabilities = PlaybackCapabilities.Create(
                new[] { "hevc" },
                new[] { "aac" },
                true);
            JellyfinMediaSource source = CreateSource("mkv", "hevc", "eac3");

            CollectionAssert.AreEqual(
                new[]
                {
                    PlaybackTier.HardwareLibVlcDirect,
                    PlaybackTier.SoftwareDirect,
                    PlaybackTier.ServerTranscode
                },
                PlaybackLadder.SelectTiers(source, source, capabilities, 1, -1));
        }

        [Test]
        public void PlaybackLadder_BurnsImageSubtitleOnServer()
        {
            PlaybackCapabilities capabilities = PlaybackCapabilities.Create(
                new[] { "h264" },
                new[] { "aac" },
                true);
            JellyfinMediaSource source = CreateSource("mp4", "h264", "aac");
            source.MediaStreams.Add(new JellyfinMediaStream
            {
                Index = 4,
                Type = "Subtitle",
                Codec = "pgssub"
            });

            Assert.IsTrue(PlaybackLadder.RequiresSubtitleBurnIn(source, 4));
            CollectionAssert.AreEqual(
                new[] { PlaybackTier.ServerTranscode },
                PlaybackLadder.SelectTiers(source, source, capabilities, 1, 4));
        }

        [Test]
        public void PlaybackLadder_ChecksTheSelectedAudioTrackForSoftwareDecode()
        {
            PlaybackCapabilities capabilities = PlaybackCapabilities.Create(
                new[] { "h264" },
                new[] { "aac" },
                true);
            JellyfinMediaSource source = CreateSource("mkv", "hevc", "unsupported");
            source.MediaStreams.Add(new JellyfinMediaStream
            {
                Index = 2,
                Type = "Audio",
                Codec = "aac"
            });

            CollectionAssert.AreEqual(
                new[] { PlaybackTier.ServerTranscode },
                PlaybackLadder.SelectTiers(source, source, capabilities, 1, -1));
            CollectionAssert.AreEqual(
                new[] { PlaybackTier.SoftwareDirect, PlaybackTier.ServerTranscode },
                PlaybackLadder.SelectTiers(source, source, capabilities, 2, -1));
        }

        [Test]
        public void DynamicProfile_AdvertisesSoftwareFormatsAndCodecLimits()
        {
            PlaybackCapabilities capabilities = PlaybackCapabilities.Create(
                new[] { "h264" },
                new[] { "aac" },
                true,
                maximumWidth: 1920,
                maximumHeight: 1080,
                maximumVideoBitDepth: 10);

            JellyfinDeviceProfile profile = JellyfinDeviceProfile.CreateRayNeoAirProfile(
                20_000_000,
                capabilities);

            Assert.AreEqual("mp4,m4v,mov", profile.DirectPlayProfiles[0].Container);
            StringAssert.Contains("mkv", profile.DirectPlayProfiles[1].Container);
            Assert.AreEqual("Width", profile.CodecProfiles[0].Conditions[0].Property);
            Assert.AreEqual("1920", profile.CodecProfiles[0].Conditions[0].Value);
            Assert.AreEqual("h264", profile.TranscodingProfiles[0].VideoCodec);
            Assert.AreEqual("aac,mp3", profile.TranscodingProfiles[0].AudioCodec);
        }

        [Test]
        public void PlaybackRequest_SerializesTrackAndBurnInSelection()
        {
            JellyfinPlaybackInfoRequest request = new JellyfinPlaybackInfoRequest
            {
                AudioStreamIndex = 2,
                SubtitleStreamIndex = 5,
                MediaSourceId = "source",
                AlwaysBurnInSubtitleWhenTranscoding = true
            };

            string json = JsonConvert.SerializeObject(request);

            StringAssert.Contains("\"AudioStreamIndex\":2", json);
            StringAssert.Contains("\"SubtitleStreamIndex\":5", json);
            StringAssert.Contains("\"MediaSourceId\":\"source\"", json);
            StringAssert.Contains("\"AlwaysBurnInSubtitleWhenTranscoding\":true", json);
        }

        private static JellyfinMediaSource CreateSource(
            string container,
            string videoCodec,
            string audioCodec)
        {
            return new JellyfinMediaSource
            {
                Id = "source",
                Container = container,
                SupportsDirectPlay = true,
                SupportsTranscoding = true,
                TranscodingUrl = "/Videos/item/master.m3u8",
                DefaultAudioStreamIndex = 1,
                DefaultSubtitleStreamIndex = -1,
                MediaStreams = new List<JellyfinMediaStream>
                {
                    new JellyfinMediaStream
                    {
                        Index = 0,
                        Type = "Video",
                        Codec = videoCodec,
                        Width = 1920,
                        Height = 1080,
                        BitDepth = 8
                    },
                    new JellyfinMediaStream
                    {
                        Index = 1,
                        Type = "Audio",
                        Codec = audioCodec,
                        IsDefault = true
                    }
                }
            };
        }
    }

    public sealed class SubtitleParserTests
    {
        [Test]
        public void WebVttParser_ReturnsActiveMultilineCue()
        {
            const string content =
                "\uFEFFWEBVTT\n\n00:00:01.500 --> 00:00:04.000 align:center\n你好<br>RayNeo &amp; Jellyfin\n";

            SubtitleTrack track = SubtitleParser.Parse(content, "vtt");

            Assert.AreEqual(1, track.Cues.Count);
            Assert.AreEqual("你好\nRayNeo & Jellyfin", track.TextAt(2d));
            Assert.AreEqual(string.Empty, track.TextAt(5d));
        }

        [Test]
        public void SrtParser_AcceptsCommaMilliseconds()
        {
            const string content =
                "1\n00:01:02,250 --> 00:01:04,750\n第一行\n第二行\n";

            SubtitleTrack track = SubtitleParser.Parse(content, "srt");

            Assert.AreEqual("第一行\n第二行", track.TextAt(63d));
        }

        [Test]
        public void AssParser_RemovesStyleOverridesAndExpandsLineBreaks()
        {
            const string content =
                "[Events]\nDialogue: 0,0:00:02.00,0:00:05.00,Default,,0,0,0,,{\\b1}第一行\\N第二行";

            SubtitleTrack track = SubtitleParser.Parse(content, "ass");

            Assert.AreEqual("第一行\n第二行", track.TextAt(3d));
        }
    }
}
