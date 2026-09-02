using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class EpisodeShelfView
    {
        public const float PreferredHeight = 438f;

        private readonly GameObject _root;
        private readonly Text _summary;
        private readonly RectTransform _seasonTabs;
        private readonly RectTransform _episodeRow;
        private readonly AxisRoutingScrollRect _seasonScroll;
        private readonly AxisRoutingScrollRect _episodeScroll;
        private readonly ScrollRect _parentScroll;
        private readonly List<Button> _seasonButtons = new List<Button>();

        public event Action<JellyfinItem> EpisodeSelected;

        public EpisodeShelfView(Transform parent, ScrollRect parentScroll)
        {
            _parentScroll = parentScroll;

            Image root = UiFactory.CreateRoundedPanel("Episode Shelf", parent, UiTheme.SurfaceGlass);
            root.raycastTarget = false;
            _root = root.gameObject;
            LayoutElement rootLayout = _root.AddComponent<LayoutElement>();
            rootLayout.minHeight = PreferredHeight;
            rootLayout.preferredHeight = PreferredHeight;
            rootLayout.flexibleHeight = 0f;
            Outline outline = _root.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            Text heading = UiFactory.CreateText(
                "Episode Heading",
                root.transform,
                "剧集",
                30,
                UiTheme.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                heading.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(30f, -22f),
                new Vector2(420f, 40f));

            _summary = UiFactory.CreateText(
                "Episode Summary",
                root.transform,
                string.Empty,
                19,
                UiTheme.TextSecondary,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _summary.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -64f),
                new Vector2(-60f, 28f));

            RectTransform tabsViewport = UiFactory.CreateRect("Episode Season Viewport", root.transform);
            AddTransparentDragSurface(tabsViewport);
            tabsViewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.SetRect(
                tabsViewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -96f),
                new Vector2(-60f, 50f));

            _seasonTabs = UiFactory.CreateRect("Episode Season Tabs", tabsViewport);
            _seasonTabs.anchorMin = new Vector2(0f, 0.5f);
            _seasonTabs.anchorMax = new Vector2(0f, 0.5f);
            _seasonTabs.pivot = new Vector2(0f, 0.5f);
            _seasonTabs.anchoredPosition = Vector2.zero;
            _seasonTabs.sizeDelta = new Vector2(0f, 48f);
            HorizontalLayoutGroup tabsLayout = _seasonTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 10f;
            tabsLayout.padding = new RectOffset(0, 18, 0, 0);
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childControlHeight = false;
            tabsLayout.childControlWidth = false;
            tabsLayout.childForceExpandHeight = false;
            tabsLayout.childForceExpandWidth = false;
            ContentSizeFitter tabsFitter = _seasonTabs.gameObject.AddComponent<ContentSizeFitter>();
            tabsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _seasonScroll = tabsViewport.gameObject.AddComponent<AxisRoutingScrollRect>();
            _seasonScroll.viewport = tabsViewport;
            _seasonScroll.content = _seasonTabs;
            _seasonScroll.horizontal = true;
            _seasonScroll.vertical = false;
            _seasonScroll.movementType = ScrollRect.MovementType.Elastic;
            _seasonScroll.scrollSensitivity = 42f;
            _seasonScroll.decelerationRate = 0.13f;
            _seasonScroll.ConfigureParent(parentScroll);

            RectTransform episodeViewport = UiFactory.CreateRect("Episode Viewport", root.transform);
            AddTransparentDragSurface(episodeViewport);
            episodeViewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.SetRect(
                episodeViewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -58f),
                new Vector2(-60f, -158f));

            _episodeRow = UiFactory.CreateRect("Episode Cards", episodeViewport);
            _episodeRow.anchorMin = new Vector2(0f, 0.5f);
            _episodeRow.anchorMax = new Vector2(0f, 0.5f);
            _episodeRow.pivot = new Vector2(0f, 0.5f);
            _episodeRow.anchoredPosition = Vector2.zero;
            _episodeRow.sizeDelta = new Vector2(0f, PosterCardView.LandscapeHeight + 16f);
            HorizontalLayoutGroup episodeLayout = _episodeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            episodeLayout.spacing = 24f;
            episodeLayout.padding = new RectOffset(2, 30, 8, 8);
            episodeLayout.childAlignment = TextAnchor.MiddleLeft;
            episodeLayout.childControlHeight = false;
            episodeLayout.childControlWidth = false;
            episodeLayout.childForceExpandHeight = false;
            episodeLayout.childForceExpandWidth = false;
            ContentSizeFitter episodeFitter = _episodeRow.gameObject.AddComponent<ContentSizeFitter>();
            episodeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _episodeScroll = episodeViewport.gameObject.AddComponent<AxisRoutingScrollRect>();
            _episodeScroll.viewport = episodeViewport;
            _episodeScroll.content = _episodeRow;
            _episodeScroll.horizontal = true;
            _episodeScroll.vertical = false;
            _episodeScroll.movementType = ScrollRect.MovementType.Elastic;
            _episodeScroll.elasticity = 0.085f;
            _episodeScroll.scrollSensitivity = 46f;
            _episodeScroll.decelerationRate = 0.13f;
            _episodeScroll.ConfigureParent(parentScroll);

            _root.SetActive(false);
        }

        public bool IsVisible
        {
            get { return _root.activeSelf; }
        }

        public void Bind(
            IList<JellyfinItem> episodes,
            JellyfinItem preferredEpisode,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            CancellationToken cancellationToken)
        {
            List<JellyfinItem> playable = EpisodePlaybackResolver.OrderedPlayableEpisodes(episodes);
            UiFactory.DestroyChildren(_seasonTabs);
            UiFactory.DestroyChildren(_episodeRow);
            _seasonButtons.Clear();
            _root.SetActive(playable.Count > 0);
            if (playable.Count == 0)
            {
                return;
            }
            UiFactory.AddScrollReveal(_root, _parentScroll, 0.04f);

            List<KeyValuePair<int, List<JellyfinItem>>> seasons = playable
                .GroupBy(episode => episode.ParentIndexNumber ?? int.MaxValue)
                .OrderBy(group => SeasonSortKey(group.Key))
                .Select(group => new KeyValuePair<int, List<JellyfinItem>>(group.Key, group.ToList()))
                .ToList();
            int preferredSeason = preferredEpisode != null
                ? preferredEpisode.ParentIndexNumber ?? seasons[0].Key
                : seasons[0].Key;
            _summary.text = BuildSummary(seasons.Count, playable.Count, preferredEpisode);

            foreach (KeyValuePair<int, List<JellyfinItem>> season in seasons)
            {
                int selectedSeason = season.Key;
                List<JellyfinItem> selectedEpisodes = season.Value;
                Button tab = UiFactory.CreateButton(
                    "Episode Season - " + SeasonTitle(selectedSeason, selectedEpisodes),
                    _seasonTabs,
                    SeasonTitle(selectedSeason, selectedEpisodes),
                    UiTheme.SurfaceRaised,
                    UiTheme.TextPrimary,
                    18);
                LayoutElement tabLayout = tab.gameObject.AddComponent<LayoutElement>();
                tabLayout.preferredWidth = 152f;
                tabLayout.preferredHeight = 46f;
                tab.GetComponent<RectTransform>().sizeDelta = new Vector2(152f, 46f);
                tab.onClick.AddListener(() =>
                    ShowSeason(selectedSeason, selectedEpisodes, api, imageCache, cancellationToken));
                _seasonButtons.Add(tab);
                UiFactory.AddItemReveal(
                    tab.gameObject,
                    Mathf.Min(0.14f, _seasonButtons.Count * 0.018f));
            }

            KeyValuePair<int, List<JellyfinItem>> initial = seasons
                .FirstOrDefault(season => season.Key == preferredSeason);
            if (initial.Value == null)
            {
                initial = seasons[0];
            }
            ShowSeason(initial.Key, initial.Value, api, imageCache, cancellationToken);
            Canvas.ForceUpdateCanvases();
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        private void ShowSeason(
            int seasonNumber,
            IList<JellyfinItem> episodes,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            CancellationToken cancellationToken)
        {
            UiFactory.DestroyChildren(_episodeRow);
            _episodeScroll.StopMovement();
            _episodeRow.anchoredPosition = Vector2.zero;
            int cardIndex = 0;
            foreach (JellyfinItem episode in episodes)
            {
                PosterCardView card = PosterCardView.Create(_episodeRow, true);
                card.ConfigureScrollRects(_episodeScroll, _parentScroll);
                card.Bind(
                    episode,
                    api,
                    imageCache,
                    selected => EpisodeSelected?.Invoke(selected),
                    cancellationToken,
                    640,
                    true);
                UiFactory.AddItemReveal(
                    card.gameObject,
                    Mathf.Min(0.20f, cardIndex * 0.028f));
                cardIndex++;
            }

            foreach (Button button in _seasonButtons)
            {
                bool selected = button.gameObject.name ==
                    "Episode Season - " + SeasonTitle(seasonNumber, episodes);
                button.targetGraphic.color = selected ? UiTheme.Accent : UiTheme.SurfaceRaised;
            }
            Canvas.ForceUpdateCanvases();
        }

        private static string BuildSummary(int seasonCount, int episodeCount, JellyfinItem preferredEpisode)
        {
            if (preferredEpisode != null)
            {
                bool resume = EpisodePlaybackResolver.ResumePosition(preferredEpisode)
                    > AppConstants.TicksPerSecond * 10L;
                return string.Format(
                    "{0} · {1} · {2}",
                    resume ? "继续观看" : "接下来",
                    EpisodePlaybackResolver.EpisodeCode(preferredEpisode),
                    JellyfinText.ToPlainText(preferredEpisode.Name));
            }
            return string.Format("{0} 季 · {1} 集", seasonCount, episodeCount);
        }

        private static string SeasonTitle(int seasonNumber, IList<JellyfinItem> episodes)
        {
            JellyfinItem first = episodes != null ? episodes.FirstOrDefault() : null;
            if (first != null && !string.IsNullOrWhiteSpace(first.SeasonName))
            {
                return first.SeasonName;
            }
            if (seasonNumber == 0)
            {
                return "特别篇";
            }
            return seasonNumber == int.MaxValue ? "剧集" : "第 " + seasonNumber + " 季";
        }

        private static int SeasonSortKey(int seasonNumber)
        {
            return seasonNumber == 0 ? int.MaxValue - 1 : seasonNumber;
        }

        private static void AddTransparentDragSurface(RectTransform viewport)
        {
            Image surface = viewport.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;
        }
    }
}
