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
    public sealed class JellyfinQuickConnectResult
    {
        public bool Authenticated;
        public string Secret;
        public string Code;
        public string DeviceId;
        public string DeviceName;
        public string AppName;
        public string AppVersion;
        public string DateAdded;
    }

    [Serializable]
    public sealed class JellyfinQuickConnectAuthenticationRequest
    {
        public string Secret;
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
        public string CollectionType;
        public string ParentId;
        public string Overview;
        public int? ProductionYear;
        public long? RunTimeTicks;
        public int? ChildCount;
        public int? RecursiveItemCount;
        public double? PrimaryImageAspectRatio;
        public Dictionary<string, string> ImageTags;
        public List<string> BackdropImageTags;
        public string ParentBackdropItemId;
        public string SeriesName;
        public string SeriesId;
        public string SeasonId;
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
        public List<JellyfinChapter> Chapters;

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

        public bool IsBrowsableContainer
        {
            get
            {
                switch ((Type ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "collectionfolder":
                    case "folder":
                    case "boxset":
                    case "playlist":
                    case "photoalbum":
                    case "musicalbum":
                    case "musicartist":
                        return true;
                    default:
                        return false;
                }
            }
        }

        public int? VisibleChildCount
        {
            get { return ChildCount ?? RecursiveItemCount; }
        }

        public string Subtitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SeriesName))
                {
                    if (ParentIndexNumber.HasValue && IndexNumber.HasValue)
                    {
                        return string.Format("{0}  S{1}E{2}", SeriesName, ParentIndexNumber.Value, IndexNumber.Value);
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
        public int? UnplayedItemCount;
        public bool IsFavorite;
        public string LastPlayedDate;
        public bool Played;
        public string Key;
    }

    [Serializable]
    public sealed class JellyfinChapter
    {
        public long StartPositionTicks;
        public string Name;
        public string ImageTag;
    }

    public sealed class JellyfinItemsQuery
    {
        public string ParentId;
        public string SearchTerm;
        public string IncludeItemTypes;
        public string ExcludeItemTypes;
        public string SortBy;
        public string SortOrder;
        public string Filters;
        public string GenreIds;
        public int StartIndex;
        public int Limit = 30;
        public bool Recursive;
        public bool EnableTotalRecordCount = true;
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
        public long? Size;
        public int? Bitrate;
        public long? RunTimeTicks;
        public bool SupportsTranscoding;
        public bool SupportsDirectStream;
        public bool SupportsDirectPlay;
        public List<JellyfinMediaStream> MediaStreams;
        public string DirectStreamUrl;
        public string TranscodingUrl;
        public string TranscodingContainer;
        public string TranscodingSubProtocol;
        public int? DefaultAudioStreamIndex;
        public int? DefaultSubtitleStreamIndex;
    }

    [Serializable]
    public sealed class JellyfinMediaStream
    {
        public int Index;
        public string Codec;
        public string Type;
        public string Language;
        public string DisplayTitle;
        public string Title;
        public bool IsDefault;
        public bool IsForced;
        public bool IsHearingImpaired;
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
        public int? SampleRate;
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
        public int? AudioStreamIndex;
        public int? SubtitleStreamIndex;
        public string MediaSourceId;
        public int? MaxStreamingBitrate;
        public int? MaxAudioChannels;
        public bool EnableDirectPlay = true;
        public bool EnableDirectStream = true;
        public bool EnableTranscoding = true;
        public bool AllowVideoStreamCopy = true;
        public bool AllowAudioStreamCopy = true;
        public bool AlwaysBurnInSubtitleWhenTranscoding;
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
        public List<JellyfinContainerProfile> ContainerProfiles;
        public List<JellyfinCodecProfile> CodecProfiles;
        public List<JellyfinSubtitleProfile> SubtitleProfiles;

        public static JellyfinDeviceProfile CreateRayNeoAirProfile(int maxBitrate)
        {
            return CreateRayNeoAirProfile(maxBitrate, PlaybackCapabilities.Detect());
        }

        public static JellyfinDeviceProfile CreateRayNeoAirProfile(
            int maxBitrate,
            PlaybackCapabilities capabilities)
        {
            capabilities = capabilities ?? PlaybackCapabilities.CreateConservative();
            List<JellyfinDirectPlayProfile> directPlayProfiles =
                capabilities.CreateDirectPlayProfiles();
            return new JellyfinDeviceProfile
            {
                Name = "RayNeo Air / Unity Android",
                MaxStreamingBitrate = maxBitrate,
                MaxStaticBitrate = maxBitrate,
                DirectPlayProfiles = directPlayProfiles,
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
                ContainerProfiles = new List<JellyfinContainerProfile>(),
                CodecProfiles = capabilities.CreateCodecProfiles(),
                SubtitleProfiles = new List<JellyfinSubtitleProfile>
                {
                    new JellyfinSubtitleProfile { Format = "vtt", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "srt", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "subrip", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "ass", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "ssa", Method = "External" },
                    new JellyfinSubtitleProfile { Format = "pgssub", Method = "Encode" },
                    new JellyfinSubtitleProfile { Format = "dvdsub", Method = "Encode" },
                    new JellyfinSubtitleProfile { Format = "dvbsub", Method = "Encode" }
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

    [Serializable]
    public sealed class JellyfinContainerProfile
    {
        public string Type;
        public string Container;
        public string SubContainer;
        public List<JellyfinProfileCondition> Conditions;
    }

    [Serializable]
    public sealed class JellyfinCodecProfile
    {
        public string Type;
        public string Codec;
        public string Container;
        public string SubContainer;
        public List<JellyfinProfileCondition> Conditions;
        public List<JellyfinProfileCondition> ApplyConditions;
    }

    [Serializable]
    public sealed class JellyfinProfileCondition
    {
        public string Condition;
        public string Property;
        public string Value;
        public bool IsRequired;
    }

    public enum PlaybackTier
    {
        HardwareDirect = 0,
        HardwareLibVlcDirect = 1,
        SoftwareDirect = 2,
        ServerTranscode = 3
    }

    public sealed class JellyfinPlaybackSelection
    {
        public string MediaSourceId;
        public int? AudioStreamIndex;
        public int? SubtitleStreamIndex;
    }

    public sealed class JellyfinPlaybackPlan
    {
        public JellyfinItem Item;
        public JellyfinMediaSource MediaSource;
        public string Url;
        public string PlaySessionId;
        public string PlayMethod;
        public long StartPositionTicks;
        public PlaybackTier Tier;
        public int? AudioStreamIndex;
        public int? SubtitleStreamIndex;
        public int LocalAudioTrackIndex;
        public int LocalAudioTrackCount;
        public string SubtitleUrl;
        public string SubtitleCodec;
        public bool SubtitleBurnedIn;

        public string TierLabel
        {
            get
            {
                switch (Tier)
                {
                    case PlaybackTier.HardwareDirect:
                        return "硬件解码";
                    case PlaybackTier.HardwareLibVlcDirect:
                        return "兼容容器硬件优先";
                    case PlaybackTier.SoftwareDirect:
                        return "软件解码";
                    default:
                        return "服务器转码";
                }
            }
        }
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
        public int? AudioStreamIndex;
        public int? SubtitleStreamIndex;
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
        public int? AudioStreamIndex;
        public int? SubtitleStreamIndex;
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
