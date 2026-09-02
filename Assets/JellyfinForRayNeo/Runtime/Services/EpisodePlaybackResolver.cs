using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyfinForRayNeo
{
    public static class EpisodePlaybackResolver
    {
        public static JellyfinItem Select(IList<JellyfinItem> episodes)
        {
            List<JellyfinItem> ordered = OrderedPlayableEpisodes(episodes);
            JellyfinItem resumable = ordered
                .Where(episode =>
                    episode.UserData != null
                    && !episode.UserData.Played
                    && episode.UserData.PlaybackPositionTicks > AppConstants.TicksPerSecond * 10L)
                .OrderByDescending(episode => episode.UserData.LastPlayedDate ?? string.Empty)
                .FirstOrDefault();
            if (resumable != null)
            {
                return resumable;
            }

            return ordered.FirstOrDefault(episode =>
                    episode.UserData == null || !episode.UserData.Played)
                ?? ordered.FirstOrDefault();
        }

        public static List<JellyfinItem> OrderedPlayableEpisodes(IList<JellyfinItem> episodes)
        {
            return episodes == null
                ? new List<JellyfinItem>()
                : episodes
                    .Where(episode => episode != null && episode.IsPlayable)
                    .OrderBy(episode => SeasonSortKey(episode.ParentIndexNumber))
                    .ThenBy(episode => episode.IndexNumber ?? int.MaxValue)
                    .ThenBy(episode => episode.Name)
                    .ToList();
        }

        public static long ResumePosition(JellyfinItem episode)
        {
            return episode != null && episode.UserData != null
                ? Math.Max(0L, episode.UserData.PlaybackPositionTicks)
                : 0L;
        }

        public static string EpisodeCode(JellyfinItem episode)
        {
            if (episode == null)
            {
                return string.Empty;
            }
            if (episode.ParentIndexNumber.HasValue && episode.IndexNumber.HasValue)
            {
                return string.Format(
                    "S{0}E{1}",
                    episode.ParentIndexNumber.Value,
                    episode.IndexNumber.Value);
            }
            return episode.IndexNumber.HasValue
                ? "E" + episode.IndexNumber.Value
                : "剧集";
        }

        private static int SeasonSortKey(int? seasonNumber)
        {
            if (!seasonNumber.HasValue)
            {
                return int.MaxValue;
            }
            return seasonNumber.Value == 0 ? int.MaxValue - 1 : seasonNumber.Value;
        }
    }
}
