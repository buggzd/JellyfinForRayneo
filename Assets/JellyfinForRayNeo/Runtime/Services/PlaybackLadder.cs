using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyfinForRayNeo
{
    public static class PlaybackLadder
    {
        public static List<PlaybackTier> SelectTiers(
            JellyfinMediaSource directSource,
            JellyfinMediaSource transcodeSource,
            PlaybackCapabilities capabilities,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            capabilities = capabilities ?? PlaybackCapabilities.CreateConservative();
            List<PlaybackTier> tiers = new List<PlaybackTier>();
            if (capabilities.CanHardwareDirectPlay(
                    directSource,
                    audioStreamIndex,
                    subtitleStreamIndex))
            {
                tiers.Add(PlaybackTier.HardwareDirect);
            }
            if (capabilities.CanLibVlcHardwareDirectPlay(
                    directSource,
                    audioStreamIndex,
                    subtitleStreamIndex))
            {
                tiers.Add(PlaybackTier.HardwareLibVlcDirect);
            }
            if (capabilities.CanSoftwareDirectPlay(
                    directSource,
                    audioStreamIndex,
                    subtitleStreamIndex))
            {
                tiers.Add(PlaybackTier.SoftwareDirect);
            }
            if (transcodeSource != null
                && !string.IsNullOrWhiteSpace(transcodeSource.TranscodingUrl))
            {
                tiers.Add(PlaybackTier.ServerTranscode);
            }
            return tiers;
        }

        public static int? ResolveAudioIndex(
            JellyfinMediaSource source,
            int? requestedIndex)
        {
            JellyfinMediaStream stream = PlaybackCapabilities.ResolveAudioStream(
                source,
                requestedIndex);
            return stream != null ? stream.Index : (int?)null;
        }

        public static int? ResolveSubtitleIndex(
            JellyfinMediaSource source,
            int? requestedIndex)
        {
            if (requestedIndex.HasValue && requestedIndex.Value < 0)
            {
                return -1;
            }
            JellyfinMediaStream stream = PlaybackCapabilities.ResolveSubtitleStream(
                source,
                requestedIndex);
            return stream != null ? stream.Index : -1;
        }

        public static int AudioTrackOrdinal(
            JellyfinMediaSource source,
            int? selectedStreamIndex)
        {
            List<JellyfinMediaStream> audioStreams =
                PlaybackCapabilities.StreamsOfType(source, "Audio");
            if (audioStreams.Count == 0 || !selectedStreamIndex.HasValue)
            {
                return 0;
            }
            int ordinal = audioStreams.FindIndex(
                stream => stream.Index == selectedStreamIndex.Value);
            return Math.Max(0, ordinal);
        }

        public static bool RequiresSubtitleBurnIn(
            JellyfinMediaSource source,
            int? subtitleStreamIndex)
        {
            JellyfinMediaStream stream = PlaybackCapabilities.ResolveSubtitleStream(
                source,
                subtitleStreamIndex);
            return stream != null
                && !PlaybackCapabilities.IsTextSubtitleCodec(stream.Codec);
        }

        public static JellyfinMediaSource SelectMediaSource(
            IEnumerable<JellyfinMediaSource> sources,
            string requestedId)
        {
            List<JellyfinMediaSource> available = (sources ?? Enumerable.Empty<JellyfinMediaSource>())
                .Where(source => source != null)
                .ToList();
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                JellyfinMediaSource requested = available.FirstOrDefault(source =>
                    string.Equals(source.Id, requestedId, StringComparison.OrdinalIgnoreCase));
                if (requested != null)
                {
                    return requested;
                }
            }
            return available.FirstOrDefault();
        }
    }
}
