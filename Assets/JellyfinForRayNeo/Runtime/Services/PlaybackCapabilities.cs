using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyfinForRayNeo
{
    public sealed class PlaybackCapabilities
    {
        private static PlaybackCapabilities _detected;

        private readonly HashSet<string> _hardwareVideoCodecs;
        private readonly HashSet<string> _hardwareAudioCodecs;
        private readonly HashSet<string> _hardwareContainers;
        private readonly HashSet<string> _softwareVideoCodecs;
        private readonly HashSet<string> _softwareAudioCodecs;
        private readonly HashSet<string> _softwareContainers;

        private PlaybackCapabilities(
            IEnumerable<string> hardwareVideoCodecs,
            IEnumerable<string> hardwareAudioCodecs,
            IEnumerable<string> hardwareContainers,
            bool softwareDecoderAvailable,
            int maximumWidth,
            int maximumHeight,
            int maximumVideoBitDepth)
        {
            _hardwareVideoCodecs = NormalizeSet(hardwareVideoCodecs);
            _hardwareAudioCodecs = NormalizeSet(hardwareAudioCodecs);
            _hardwareContainers = NormalizeSet(hardwareContainers);
            SoftwareDecoderAvailable = softwareDecoderAvailable;
            MaximumWidth = Math.Max(640, maximumWidth);
            MaximumHeight = Math.Max(360, maximumHeight);
            MaximumVideoBitDepth = Math.Max(8, maximumVideoBitDepth);

            _softwareVideoCodecs = NormalizeSet(new[]
            {
                "h264", "hevc", "vp8", "vp9", "av1", "mpeg2video", "mpeg4",
                "vc1", "wmv3", "theora"
            });
            _softwareAudioCodecs = NormalizeSet(new[]
            {
                "aac", "mp3", "ac3", "eac3", "truehd", "dts", "flac", "alac",
                "opus", "vorbis", "pcm_s16le", "pcm_s24le"
            });
            _softwareContainers = NormalizeSet(new[]
            {
                "mp4", "m4v", "mov", "mkv", "webm", "avi", "ts", "m2ts",
                "mpeg", "mpg", "ogg", "ogv", "wmv", "flv"
            });
        }

        public bool SoftwareDecoderAvailable { get; }

        public int MaximumWidth { get; }

        public int MaximumHeight { get; }

        public int MaximumVideoBitDepth { get; }

        public IReadOnlyCollection<string> HardwareVideoCodecs
        {
            get { return _hardwareVideoCodecs; }
        }

        public static PlaybackCapabilities Detect()
        {
            if (_detected != null)
            {
                return _detected;
            }

            HashSet<string> videoCodecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> audioCodecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidMediaCodecProbe.Populate(videoCodecs, audioCodecs);
#endif
            if (videoCodecs.Count == 0)
            {
                videoCodecs.UnionWith(new[] { "h264", "hevc" });
            }
            if (audioCodecs.Count == 0)
            {
                audioCodecs.UnionWith(new[] { "aac", "mp3", "ac3", "eac3" });
            }

            int maxTexture = Math.Max(2048, SystemInfo.maxTextureSize);
            _detected = new PlaybackCapabilities(
                videoCodecs,
                audioCodecs,
                new[] { "mp4", "m4v", "mov" },
                LibVlcPlaybackEngine.IsNativeLibraryAvailable,
                Math.Min(3840, maxTexture),
                Math.Min(2160, maxTexture),
                10);

            Debug.Log(
                "Playback capabilities: hardware video="
                + string.Join(",", _detected._hardwareVideoCodecs.OrderBy(value => value))
                + "; software fallback=" + _detected.SoftwareDecoderAvailable);
            return _detected;
        }

        public static PlaybackCapabilities CreateConservative()
        {
            return Create(
                new[] { "h264", "hevc" },
                new[] { "aac", "mp3", "ac3", "eac3" },
                false);
        }

        public static PlaybackCapabilities Create(
            IEnumerable<string> hardwareVideoCodecs,
            IEnumerable<string> hardwareAudioCodecs,
            bool softwareDecoderAvailable,
            IEnumerable<string> hardwareContainers = null,
            int maximumWidth = 3840,
            int maximumHeight = 2160,
            int maximumVideoBitDepth = 10)
        {
            return new PlaybackCapabilities(
                hardwareVideoCodecs,
                hardwareAudioCodecs,
                hardwareContainers ?? new[] { "mp4", "m4v", "mov" },
                softwareDecoderAvailable,
                maximumWidth,
                maximumHeight,
                maximumVideoBitDepth);
        }

        public bool CanHardwareDirectPlay(
            JellyfinMediaSource source,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            if (source == null
                || !source.SupportsDirectPlay
                || !_hardwareContainers.Contains(NormalizeContainer(source.Container))
                || HasImageSubtitle(source, subtitleStreamIndex))
            {
                return false;
            }

            JellyfinMediaStream video = FirstStream(source, "Video");
            if (video != null)
            {
                if (!_hardwareVideoCodecs.Contains(NormalizeCodec(video.Codec)))
                {
                    return false;
                }
                if (video.Width.HasValue && video.Width.Value > MaximumWidth)
                {
                    return false;
                }
                if (video.Height.HasValue && video.Height.Value > MaximumHeight)
                {
                    return false;
                }
                if (video.BitDepth.HasValue && video.BitDepth.Value > MaximumVideoBitDepth)
                {
                    return false;
                }
            }

            JellyfinMediaStream audio = ResolveAudioStream(source, audioStreamIndex);
            return audio == null
                || string.IsNullOrWhiteSpace(audio.Codec)
                || _hardwareAudioCodecs.Contains(NormalizeCodec(audio.Codec));
        }

        public bool CanSoftwareDirectPlay(
            JellyfinMediaSource source,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            if (!SoftwareDecoderAvailable
                || source == null
                || !source.SupportsDirectPlay
                || HasImageSubtitle(source, subtitleStreamIndex))
            {
                return false;
            }

            string container = NormalizeContainer(source.Container);
            JellyfinMediaStream video = FirstStream(source, "Video");
            JellyfinMediaStream audio = ResolveAudioStream(source, audioStreamIndex);
            return _softwareContainers.Contains(container)
                && (video == null || _softwareVideoCodecs.Contains(NormalizeCodec(video.Codec)))
                && (audio == null || _softwareAudioCodecs.Contains(NormalizeCodec(audio.Codec)));
        }

        public bool CanLibVlcHardwareDirectPlay(
            JellyfinMediaSource source,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            if (!SoftwareDecoderAvailable
                || source == null
                || !source.SupportsDirectPlay
                || !_softwareContainers.Contains(NormalizeContainer(source.Container))
                || HasImageSubtitle(source, subtitleStreamIndex))
            {
                return false;
            }

            JellyfinMediaStream video = FirstStream(source, "Video");
            if (video != null)
            {
                if (!_hardwareVideoCodecs.Contains(NormalizeCodec(video.Codec))
                    || video.Width.HasValue && video.Width.Value > MaximumWidth
                    || video.Height.HasValue && video.Height.Value > MaximumHeight
                    || video.BitDepth.HasValue && video.BitDepth.Value > MaximumVideoBitDepth)
                {
                    return false;
                }
            }

            JellyfinMediaStream audio = ResolveAudioStream(source, audioStreamIndex);
            return audio == null
                || string.IsNullOrWhiteSpace(audio.Codec)
                || _softwareAudioCodecs.Contains(NormalizeCodec(audio.Codec));
        }

        public List<JellyfinDirectPlayProfile> CreateDirectPlayProfiles()
        {
            List<JellyfinDirectPlayProfile> profiles = new List<JellyfinDirectPlayProfile>
            {
                new JellyfinDirectPlayProfile
                {
                    Container = Join(_hardwareContainers),
                    Type = "Video",
                    VideoCodec = Join(_hardwareVideoCodecs),
                    AudioCodec = Join(_hardwareAudioCodecs)
                }
            };
            if (SoftwareDecoderAvailable)
            {
                profiles.Add(new JellyfinDirectPlayProfile
                {
                    Container = Join(_softwareContainers),
                    Type = "Video",
                    VideoCodec = Join(_softwareVideoCodecs),
                    AudioCodec = Join(_softwareAudioCodecs)
                });
            }
            return profiles;
        }

        public List<JellyfinCodecProfile> CreateCodecProfiles()
        {
            HashSet<string> codecs = new HashSet<string>(_hardwareVideoCodecs, StringComparer.OrdinalIgnoreCase);
            if (SoftwareDecoderAvailable)
            {
                codecs.UnionWith(_softwareVideoCodecs);
            }

            return new List<JellyfinCodecProfile>
            {
                new JellyfinCodecProfile
                {
                    Type = "Video",
                    Codec = Join(codecs),
                    Conditions = new List<JellyfinProfileCondition>
                    {
                        MaximumCondition("Width", MaximumWidth),
                        MaximumCondition("Height", MaximumHeight),
                        MaximumCondition("VideoBitDepth", MaximumVideoBitDepth)
                    },
                    ApplyConditions = new List<JellyfinProfileCondition>()
                }
            };
        }

        public static bool IsTextSubtitleCodec(string codec)
        {
            string value = NormalizeCodec(codec);
            return value == "vtt"
                || value == "webvtt"
                || value == "srt"
                || value == "subrip"
                || value == "ass"
                || value == "ssa"
                || value == "mov_text"
                || value == "tx3g"
                || value == "text";
        }

        public static JellyfinMediaStream ResolveAudioStream(
            JellyfinMediaSource source,
            int? requestedIndex)
        {
            List<JellyfinMediaStream> streams = StreamsOfType(source, "Audio");
            if (streams.Count == 0)
            {
                return null;
            }
            int? selected = requestedIndex ?? source.DefaultAudioStreamIndex;
            JellyfinMediaStream match = selected.HasValue
                ? streams.FirstOrDefault(stream => stream.Index == selected.Value)
                : null;
            return match
                ?? streams.FirstOrDefault(stream => stream.IsDefault)
                ?? streams[0];
        }

        public static JellyfinMediaStream ResolveSubtitleStream(
            JellyfinMediaSource source,
            int? requestedIndex)
        {
            if (requestedIndex.HasValue && requestedIndex.Value < 0)
            {
                return null;
            }
            List<JellyfinMediaStream> streams = StreamsOfType(source, "Subtitle");
            int? selected = requestedIndex ?? (source != null ? source.DefaultSubtitleStreamIndex : null);
            JellyfinMediaStream match = selected.HasValue
                ? streams.FirstOrDefault(stream => stream.Index == selected.Value)
                : null;
            return match ?? streams.FirstOrDefault(stream => stream.IsForced && stream.IsDefault);
        }

        public static List<JellyfinMediaStream> StreamsOfType(
            JellyfinMediaSource source,
            string type)
        {
            return source != null && source.MediaStreams != null
                ? source.MediaStreams
                    .Where(stream => stream != null
                        && string.Equals(stream.Type, type, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                : new List<JellyfinMediaStream>();
        }

        public static string NormalizeCodec(string codec)
        {
            string value = string.IsNullOrWhiteSpace(codec)
                ? string.Empty
                : codec.Trim().ToLowerInvariant();
            switch (value)
            {
                case "avc":
                case "avc1":
                    return "h264";
                case "h265":
                case "hev1":
                case "hvc1":
                    return "hevc";
                case "ac-3":
                    return "ac3";
                case "ec-3":
                    return "eac3";
                default:
                    return value;
            }
        }

        private static bool HasImageSubtitle(JellyfinMediaSource source, int? index)
        {
            JellyfinMediaStream subtitle = ResolveSubtitleStream(source, index);
            return subtitle != null && !IsTextSubtitleCodec(subtitle.Codec);
        }

        private static JellyfinMediaStream FirstStream(JellyfinMediaSource source, string type)
        {
            return StreamsOfType(source, type).FirstOrDefault();
        }

        private static HashSet<string> NormalizeSet(IEnumerable<string> values)
        {
            return new HashSet<string>(
                (values ?? Enumerable.Empty<string>())
                    .Select(value => NormalizeCodec(value))
                    .Where(value => !string.IsNullOrEmpty(value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeContainer(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static string Join(IEnumerable<string> values)
        {
            string[] preferred =
            {
                "mp4", "m4v", "mov", "mkv", "webm", "avi", "ts", "m2ts",
                "mpeg", "mpg", "ogg", "ogv", "wmv", "flv",
                "h264", "hevc", "vp9", "vp8", "av1", "mpeg2video", "mpeg4",
                "vc1", "wmv3", "theora",
                "aac", "mp3", "ac3", "eac3", "opus", "vorbis", "flac", "alac",
                "truehd", "dts", "pcm_s16le", "pcm_s24le"
            };
            return string.Join(
                ",",
                values
                    .OrderBy(value =>
                    {
                        int index = Array.IndexOf(preferred, value);
                        return index >= 0 ? index : preferred.Length;
                    })
                    .ThenBy(value => value, StringComparer.OrdinalIgnoreCase));
        }

        private static JellyfinProfileCondition MaximumCondition(string property, int value)
        {
            return new JellyfinProfileCondition
            {
                Condition = "LessThanEqual",
                Property = property,
                Value = value.ToString(),
                IsRequired = false
            };
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static class AndroidMediaCodecProbe
        {
            public static void Populate(ISet<string> videoCodecs, ISet<string> audioCodecs)
            {
                try
                {
                    using (AndroidJavaObject codecList = new AndroidJavaObject(
                               "android.media.MediaCodecList",
                               1))
                    {
                        AndroidJavaObject[] infos = codecList.Call<AndroidJavaObject[]>("getCodecInfos");
                        if (infos == null)
                        {
                            return;
                        }
                        foreach (AndroidJavaObject info in infos)
                        {
                            using (info)
                            {
                                if (info == null || info.Call<bool>("isEncoder"))
                                {
                                    continue;
                                }
                                string name = info.Call<string>("getName") ?? string.Empty;
                                bool hardware = IsHardwareCodec(info, name);
                                string[] types = info.Call<string[]>("getSupportedTypes");
                                if (types == null)
                                {
                                    continue;
                                }
                                foreach (string mimeType in types)
                                {
                                    string codec = CodecForMimeType(mimeType);
                                    if (string.IsNullOrEmpty(codec))
                                    {
                                        continue;
                                    }
                                    if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (hardware)
                                        {
                                            videoCodecs.Add(codec);
                                        }
                                    }
                                    else if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                                    {
                                        audioCodecs.Add(codec);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Unable to inspect Android MediaCodecList: " + exception.Message);
                }
            }

            private static bool IsHardwareCodec(AndroidJavaObject info, string name)
            {
                try
                {
                    using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 29)
                        {
                            return info.Call<bool>("isHardwareAccelerated");
                        }
                    }
                }
                catch
                {
                }

                string lower = name.ToLowerInvariant();
                return !lower.Contains("google")
                    && !lower.Contains("android")
                    && !lower.Contains("ffmpeg")
                    && !lower.Contains("software")
                    && !lower.StartsWith("c2.android", StringComparison.Ordinal);
            }

            private static string CodecForMimeType(string mimeType)
            {
                switch ((mimeType ?? string.Empty).ToLowerInvariant())
                {
                    case "video/avc": return "h264";
                    case "video/hevc": return "hevc";
                    case "video/x-vnd.on2.vp8": return "vp8";
                    case "video/x-vnd.on2.vp9": return "vp9";
                    case "video/av01": return "av1";
                    case "audio/mp4a-latm": return "aac";
                    case "audio/mpeg": return "mp3";
                    case "audio/ac3": return "ac3";
                    case "audio/eac3": return "eac3";
                    case "audio/opus": return "opus";
                    case "audio/vorbis": return "vorbis";
                    case "audio/flac": return "flac";
                    default: return null;
                }
            }
        }
#endif
    }
}
