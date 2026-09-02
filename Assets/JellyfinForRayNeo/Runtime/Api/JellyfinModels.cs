using System;
using System.Collections.Generic;

namespace JellyfinForRayNeo
{
    [Serializable]
    public sealed class JellyfinAuthenticationRequest
    {
        public string Username;
        public string Pw;
    }

    [Serializable]
    public sealed class JellyfinAuthenticationResult
    {
        public JellyfinUser User;
        public string AccessToken;
        public string ServerId;
    }

    [Serializable]
    public sealed class JellyfinUser
    {
        public string Id;
        public string Name;
        public string PrimaryImageTag;
    }

    [Serializable]
    public sealed class JellyfinPublicSystemInfo
    {
        public string LocalAddress;
        public string ServerName;
        public string Version;
        public string ProductName;
        public string Id;
        public bool? StartupWizardCompleted;
    }

    [Serializable]
    public sealed class JellyfinQueryResult
    {
        public List<JellyfinItem> Items = new List<JellyfinItem>();
        public int TotalRecordCount;
        public int StartIndex;
    }

    [Serializable]
    public sealed class JellyfinItem
    {
        public string Id;
        public string Name;
        public string OriginalTitle;
        public string Type;
        public string MediaType;
        public string Overview;
        public int? ProductionYear;
        public long? RunTimeTicks;
        public double? PrimaryImageAspectRatio;
        public Dictionary<string, string> ImageTags;
        public List<string> BackdropImageTags;
        public string ParentBackdropItemId;
        public string SeriesName;
        public string SeriesId;
        public string SeasonName;
        public int? IndexNumber;
        public int? ParentIndexNumber;
        public JellyfinUserData UserData;
        public List<JellyfinMediaSource> MediaSources;
        public List<string> Genres;
        public List<string> Taglines;
        public List<string> Tags;
        public List<string> ProductionLocations;
        public List<JellyfinStudio> Studios;
        public List<JellyfinPerson> People;
        public List<JellyfinExternalUrl> ExternalUrls;
        public Dictionary<string, string> ProviderIds;
        public string OfficialRating;
        public float? CommunityRating;
        public float? CriticRating;
        public string PremiereDate;
        public string EndDate;
        public string Status;
        public string VideoType;

        public bool IsPlayable
        {
            get
            {
                return string.Equals(MediaType, "Video", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Type, "Movie", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Type, "Episode", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Type, "Video", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string Subtitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SeriesName))
                {
                    if (ParentIndexNumber.HasValue && IndexNumber.HasValue)
                    {
                        return string.Format("{0}  S{1:00}E{2:00}", SeriesName, ParentIndexNumber.Value, IndexNumber.Value);
                    }
                    return SeriesName;
                }

                return ProductionYear.HasValue ? ProductionYear.Value.ToString() : Type;
            }
        }
    }

    [Serializable]
    public sealed class JellyfinUserData
    {
        public double? PlayedPercentage;
        public long PlaybackPositionTicks;
        public int PlayCount;
        public bool IsFavorite;
        public string LastPlayedDate;
        public bool Played;
        public string Key;
    }

    [Serializable]
    public sealed class JellyfinStudio
    {
        public string Name;
        public string Id;
    }

    [Serializable]
    public sealed class JellyfinPerson
    {
        public string Name;
        public string Id;
        public string Role;
        public string Type;
        public string PrimaryImageTag;
    }

    [Serializable]
    public sealed class JellyfinExternalUrl
    {
        public string Name;
        public string Url;
    }

    [Serializable]
    public sealed class JellyfinMediaSource
    {
        public string Protocol;
        public string Id;
        public string Path;
        public string Container;
        public string Name;
        public string ETag;
        public long? RunTimeTicks;
        public bool SupportsTranscoding;
        public bool SupportsDirectStream;
        public bool SupportsDirectPlay;
        public List<JellyfinMediaStream> MediaStreams;
        public string DirectStreamUrl;
        public string TranscodingUrl;
        public string TranscodingContainer;
        public string TranscodingSubProtocol;
    }

    [Serializable]
    public sealed class JellyfinMediaStream
    {
        public int Index;
        public string Codec;
        public string Type;
        public string Language;
        public string DisplayTitle;
        public bool IsDefault;
        public bool IsForced;
        public bool SupportsExternalStream;
        public string DeliveryUrl;
        public string Profile;
        public string Level;
        public string PixelFormat;
        public string VideoRange;
        public string VideoRangeType;
        public string ColorSpace;
        public string ColorTransfer;
        public string ColorPrimaries;
        public int? Width;
        public int? Height;
        public int? BitRate;
        public int? BitDepth;
        public int? Channels;
        public string ChannelLayout;
        public float? AverageFrameRate;
        public float? RealFrameRate;
        public bool IsInterlaced;
        public bool IsExternal;
    }

    [Serializable]
    public sealed class JellyfinPlaybackInfoRequest
    {
        public string UserId;
        public long? StartTimeTicks;
        public int? MaxStreamingBitrate;
        public int? MaxAudioChannels;
        public bool EnableDirectPlay = true;
        public bool EnableDirectStream = true;
        public bool EnableTranscoding = true;
        public bool AllowVideoStreamCopy = true;
        public bool AllowAudioStreamCopy = true;
        public JellyfinDeviceProfile DeviceProfile;
    }

    [Serializable]
    public sealed class JellyfinPlaybackInfoResponse
    {
        public List<JellyfinMediaSource> MediaSources;
        public string PlaySessionId;
        public string ErrorCode;
    }

    [Serializable]
    public sealed class JellyfinDeviceProfile
    {
        public string Name;
        public int MaxStreamingBitrate;
        public int MaxStaticBitrate;
        public List<JellyfinDirectPlayProfile> DirectPlayProfiles;
        public List<JellyfinTranscodingProfile> TranscodingProfiles;
        public List<JellyfinSubtitleProfile> SubtitleProfiles;

        public static JellyfinDeviceProfile CreateRayNeoAirProfile(int maxBitrate)
        {
            return new JellyfinDeviceProfile
            {
                Name = "RayNeo Air / Unity Android",
                MaxStreamingBitrate = maxBitrate,
                MaxStaticBitrate = maxBitrate,
                DirectPlayProfiles = new List<JellyfinDirectPlayProfile>
                {
                    new JellyfinDirectPlayProfile
                    {
                        Container = "mp4,m4v,mov",
                        Type = "Video",
                        VideoCodec = "h264,hevc",
                        AudioCodec = "aac,mp3,ac3,eac3"
                    }
                },
                TranscodingProfiles = new List<JellyfinTranscodingProfile>
                {
                    new JellyfinTranscodingProfile
                    {
                        Container = "ts",
                        Type = "Video",
                        VideoCodec = "h264",
                        AudioCodec = "aac,mp3",
                        Protocol = "hls",
                        Context = "Streaming",
                        MaxAudioChannels = "2",
                        MinSegments = 2,
                        SegmentLength = 6,
                        EnableSubtitlesInManifest = true
                    }
                },
                SubtitleProfiles = new List<JellyfinSubtitleProfile>
                {
                    new JellyfinSubtitleProfile { Format = "vtt", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "srt", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "ass", Method = "Encode" },
                    new JellyfinSubtitleProfile { Format = "ssa", Method = "Encode" },
                    new JellyfinSubtitleProfile { Format = "pgssub", Method = "Encode" }
                }
            };
        }
    }

    [Serializable]
    public sealed class JellyfinDirectPlayProfile
    {
        public string Container;
        public string AudioCodec;
        public string VideoCodec;
        public string Type;
    }

    [Serializable]
    public sealed class JellyfinTranscodingProfile
    {
        public string Container;
        public string Type;
        public string VideoCodec;
        public string AudioCodec;
        public string Protocol;
        public string Context;
        public string MaxAudioChannels;
        public int MinSegments;
        public int SegmentLength;
        public bool EnableSubtitlesInManifest;
    }

    [Serializable]
    public sealed class JellyfinSubtitleProfile
    {
        public string Format;
        public string Method;
    }

    public sealed class JellyfinPlaybackPlan
    {
        public JellyfinItem Item;
        public JellyfinMediaSource MediaSource;
        public string Url;
        public string PlaySessionId;
        public string PlayMethod;
        public long StartPositionTicks;
    }

    [Serializable]
    public sealed class JellyfinPlaybackStartInfo
    {
        public bool CanSeek;
        public string ItemId;
        public string MediaSourceId;
        public bool IsPaused;
        public bool IsMuted;
        public long? PositionTicks;
        public string PlayMethod;
        public string PlaySessionId;
        public string RepeatMode = "RepeatNone";
        public string PlaybackOrder = "Default";
    }

    [Serializable]
    public sealed class JellyfinPlaybackProgressInfo
    {
        public bool CanSeek;
        public string ItemId;
        public string MediaSourceId;
        public bool IsPaused;
        public bool IsMuted;
        public long? PositionTicks;
        public string PlayMethod;
        public string PlaySessionId;
        public string RepeatMode = "RepeatNone";
        public string PlaybackOrder = "Default";
    }

    [Serializable]
    public sealed class JellyfinPlaybackStopInfo
    {
        public string ItemId;
        public string MediaSourceId;
        public long? PositionTicks;
        public string PlaySessionId;
        public bool Failed;
    }
}
