using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class EpisodeBrowserView
    {
        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly Text _title;
        private readonly Text _summary;
        private readonly EmptyStateView _emptyState;
        private readonly ScrollRect _verticalScroll;
        private readonly RectTransform _seasonTabs;
        private readonly RectTransform _content;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;
        private readonly Dictionary<int, Button> _seasonButtons =
            new Dictionary<int, Button>();
        private readonly Dictionary<int, GameObject> _seasonIndicators =
            new Dictionary<int, GameObject>();

        public event Action<JellyfinItem> EpisodeSelected;
        public event Action CloseRequested;

        public Transform FocusRoot => _root.transform;

        public EpisodeBrowserView(Transform parent, JellyfinApiClient api, JellyfinImageCache imageCache)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));

            Image rootImage = UiFactory.CreatePanel("Episode Browser", parent, UiTheme.Background);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 22f, 0.99f);
            UiFactory.CreateAmbientBackdrop(rootImage.transform);

            Image headerGlow = UiFactory.CreateGradientPanel(
                "Header Ambient Glow",
                rootImage.transform,
                new Color(0.08f, 0.32f, 0.34f, 0.22f),
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0f),
                true);
            UiFactory.SetRect(
                headerGlow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 280f));

            Image headerShadow = UiFactory.CreateRoundedPanel(
                "Header Shadow",
                rootImage.transform,
                new Color(0f, 0f, 0f, 0.48f));
            headerShadow.raycastTarget = false;
            UiFactory.SetRect(
                headerShadow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -32f),
                new Vector2(-72f, 176f));

            Image header = UiFactory.CreateRoundedPanel("Header", rootImage.transform, Color.white);
            UiFactory.SetRect(
                header.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(-76f, 176f));
            UiGradient headerGradient = header.gameObject.AddComponent<UiGradient>();
            headerGradient.StartColor = new Color(0.068f, 0.078f, 0.105f, 0.96f);
            headerGradient.EndColor = new Color(0.026f, 0.032f, 0.050f, 0.94f);
            headerGradient.Horizontal = true;
            Outline headerOutline = header.gameObject.AddComponent<Outline>();
            headerOutline.effectColor = UiTheme.Border;
            headerOutline.effectDistance = new Vector2(1f, -1f);

            Image headerAccent = UiFactory.CreateRoundedPanel(
                "Header Accent",
                header.transform,
                UiTheme.AccentBright);
            headerAccent.raycastTarget = false;
            UiFactory.SetRect(
                headerAccent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(9f, -18f),
                new Vector2(5f, 72f));

            Text eyebrow = UiFactory.CreateText(
                "Series Eyebrow",
                header.transform,
                "JELLYFIN  ·  SERIES",
                14,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                eyebrow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(34f, -17f),
                new Vector2(560f, 24f));

            _title = UiFactory.CreateText(
                "Series Title",
                header.transform,
                string.Empty,
                42,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 27;
            _title.resizeTextMaxSize = 42;
            UiFactory.SetRect(
                _title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(34f, -42f),
                new Vector2(1180f, 52f));

            _summary = UiFactory.CreateText(
                "Series Summary",
                header.transform,
                string.Empty,
                19,
                UiTheme.TextSecondary,
                TextAnchor.MiddleLeft);
            UiFactory.SetRect(
                _summary.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(36f, -94f),
                new Vector2(1080f, 28f));

            Button close = UiFactory.CreateButton(
                "Close",
                header.transform,
                "返回详情",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                20);
            UiFactory.SetRect(
                close.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-26f, -25f),
                new Vector2(146f, 52f));
            close.onClick.AddListener(() => CloseRequested?.Invoke());

            RectTransform tabsViewport = UiFactory.CreateRect("Season Tabs Viewport", header.transform);
            tabsViewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.SetRect(
                tabsViewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-82f, 10f),
                new Vector2(-220f, 44f));

            _seasonTabs = UiFactory.CreateRect("Season Tabs", tabsViewport);
            _seasonTabs.anchorMin = new Vector2(0f, 0.5f);
            _seasonTabs.anchorMax = new Vector2(0f, 0.5f);
            _seasonTabs.pivot = new Vector2(0f, 0.5f);
            _seasonTabs.anchoredPosition = Vector2.zero;
            _seasonTabs.sizeDelta = new Vector2(0f, 42f);
            HorizontalLayoutGroup tabsLayout = _seasonTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 12f;
            tabsLayout.padding = new RectOffset(4, 24, 1, 1);
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childControlHeight = false;
            tabsLayout.childControlWidth = false;
            tabsLayout.childForceExpandHeight = false;
            tabsLayout.childForceExpandWidth = false;
            ContentSizeFitter tabsFitter = _seasonTabs.gameObject.AddComponent<ContentSizeFitter>();
            tabsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect tabsScroll = header.gameObject.AddComponent<ScrollRect>();
            tabsScroll.viewport = tabsViewport;
            tabsScroll.content = _seasonTabs;
            tabsScroll.horizontal = true;
            tabsScroll.vertical = false;
            tabsScroll.movementType = ScrollRect.MovementType.Elastic;
            tabsScroll.scrollSensitivity = 40f;
            tabsScroll.decelerationRate = 0.13f;

            RectTransform viewport = UiFactory.CreateRect("Seasons Viewport", rootImage.transform);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddTransparentDragSurface(viewport);
            UiFactory.Stretch(viewport, 42f, 42f, 204f, 24f);

            _content = UiFactory.CreateRect("Seasons", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup verticalLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(10, 10, 10, 30);
            verticalLayout.spacing = 22f;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlHeight = true;
            verticalLayout.childControlWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childForceExpandWidth = true;
            ContentSizeFitter contentFitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _verticalScroll = rootImage.gameObject.AddComponent<ScrollRect>();
            _verticalScroll.viewport = viewport;
            _verticalScroll.content = _content;
            _verticalScroll.horizontal = false;
            _verticalScroll.vertical = true;
            _verticalScroll.movementType = ScrollRect.MovementType.Elastic;
            _verticalScroll.scrollSensitivity = 42f;
            _verticalScroll.decelerationRate = 0.13f;

            _emptyState = new EmptyStateView(
                rootImage.transform,
                "Episode Empty State",
                new Vector2(0f, -42f),
                new Vector2(1080f, 330f));
            _emptyState.SetContent(
                "SERIES  ·  EPISODES",
                "这部剧集暂时没有可播放的分集",
                "等待 Jellyfin 完成剧集扫描，或返回详情页选择其他内容。",
                UiTheme.AccentSecondary);
            _motion.SetVisibleImmediately(false);
        }

        public bool IsVisible
        {
            get { return _motion.IsVisible; }
        }

        public void Show(JellyfinItem series, IList<JellyfinItem> episodes, CancellationToken cancellationToken)
        {
            _title.text = series != null && !string.IsNullOrWhiteSpace(series.Name) ? series.Name : "剧集";
            List<JellyfinItem> playableEpisodes = episodes == null
                ? new List<JellyfinItem>()
                : episodes.Where(item => item != null && item.IsPlayable).ToList();
            int seasonCount = playableEpisodes
                .Select(item => item.ParentIndexNumber ?? int.MaxValue)
                .Distinct()
                .Count();
            _summary.text = playableEpisodes.Count == 0
                ? "未找到可播放分集"
                : string.Format("{0} 季  ·  {1} 集  ·  选择分集后直接播放", seasonCount, playableEpisodes.Count);

            List<KeyValuePair<int, List<JellyfinItem>>> seasons = playableEpisodes
                .GroupBy(item => item.ParentIndexNumber ?? int.MaxValue)
                .OrderBy(group => SeasonSortKey(group.Key))
                .Select(group => new KeyValuePair<int, List<JellyfinItem>>(
                    group.Key,
                    group.OrderBy(item => item.IndexNumber ?? int.MaxValue)
                        .ThenBy(item => item.Name)
                        .ToList()))
                .ToList();

            UiFactory.DestroyChildren(_seasonTabs);
            UiFactory.DestroyChildren(_content);
            _seasonButtons.Clear();
            _seasonIndicators.Clear();
            _emptyState.SetVisible(playableEpisodes.Count == 0);
            foreach (KeyValuePair<int, List<JellyfinItem>> season in seasons)
            {
                KeyValuePair<int, List<JellyfinItem>> selectedSeason = season;
                string title = SeasonTitle(selectedSeason.Key, selectedSeason.Value);
                Button tab = UiFactory.CreateButton(
                    "Season Tab - " + title,
                    _seasonTabs,
                    title,
                    UiTheme.SurfaceSoft,
                    UiTheme.TextSecondary,
                    19);
                LayoutElement layout = tab.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 164f;
                layout.preferredHeight = 42f;
                RectTransform tabRect = tab.GetComponent<RectTransform>();
                tabRect.sizeDelta = new Vector2(164f, 42f);
                Image indicator = UiFactory.CreateRoundedPanel(
                    "Active Season",
                    tab.transform,
                    UiTheme.AccentBright);
                indicator.raycastTarget = false;
                UiFactory.SetRect(
                    indicator.rectTransform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 3f),
                    new Vector2(54f, 3f));
                indicator.gameObject.SetActive(false);
                _seasonButtons[selectedSeason.Key] = tab;
                _seasonIndicators[selectedSeason.Key] = indicator.gameObject;
                UiFactory.AddItemReveal(tab.gameObject, Mathf.Min(0.18f, _seasonButtons.Count * 0.025f));
                tab.onClick.AddListener(() => ShowSeason(selectedSeason.Key, selectedSeason.Value, cancellationToken));
            }
            if (seasons.Count > 0)
            {
                ShowSeason(seasons[0].Key, seasons[0].Value, cancellationToken);
            }

            _root.transform.SetAsLastSibling();
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _verticalScroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            _motion.Hide();
        }

        private void ShowSeason(int seasonNumber, IList<JellyfinItem> episodes, CancellationToken cancellationToken)
        {
            UpdateSeasonSelection(seasonNumber);
            UiFactory.DestroyChildren(_content);
            CreateSeasonShelf(seasonNumber, episodes, cancellationToken);
            Canvas.ForceUpdateCanvases();
            _verticalScroll.verticalNormalizedPosition = 1f;
        }

        private void CreateSeasonShelf(int seasonNumber, IList<JellyfinItem> items, CancellationToken cancellationToken)
        {
            RectTransform shelf = UiFactory.CreateRect("Season - " + SeasonTitle(seasonNumber, items), _content);
            LayoutElement shelfLayout = shelf.gameObject.AddComponent<LayoutElement>();
            shelfLayout.preferredHeight = 360f;
            shelfLayout.minHeight = 360f;
            shelfLayout.flexibleHeight = 0f;

            Image titleAccent = UiFactory.CreateRoundedPanel(
                "Season Accent",
                shelf,
                UiTheme.AccentBright);
            titleAccent.raycastTarget = false;
            UiFactory.SetRect(
                titleAccent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(2f, -11f),
                new Vector2(5f, 34f));

            Text title = UiFactory.CreateText(
                "Season Title",
                shelf,
                SeasonTitle(seasonNumber, items),
                31,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(20f, -4f),
                new Vector2(-220f, 52f));

            Text count = UiFactory.CreateText(
                "Season Episode Count",
                shelf,
                (items != null ? items.Count : 0) + " 集",
                18,
                UiTheme.TextMuted,
                TextAnchor.MiddleRight,
                FontStyle.Bold);
            UiFactory.SetRect(
                count.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-6f, -8f),
                new Vector2(160f, 40f));

            RectTransform viewport = UiFactory.CreateRect("Viewport", shelf);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddTransparentDragSurface(viewport);
            UiFactory.SetRect(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(0f, -66f));

            RectTransform row = UiFactory.CreateRect("Episodes", viewport);
            row.anchorMin = new Vector2(0f, 0.5f);
            row.anchorMax = new Vector2(0f, 0.5f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(0f, PosterCardView.LandscapeHeight + 20f);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.padding = new RectOffset(8, 32, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            AxisRoutingScrollRect horizontalScroll = shelf.gameObject.AddComponent<AxisRoutingScrollRect>();
            horizontalScroll.viewport = viewport;
            horizontalScroll.content = row;
            horizontalScroll.horizontal = true;
            horizontalScroll.vertical = false;
            horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
            horizontalScroll.scrollSensitivity = 46f;
            horizontalScroll.decelerationRate = 0.13f;
            horizontalScroll.ConfigureParent(_verticalScroll);

            int cardIndex = 0;
            foreach (JellyfinItem episode in items)
            {
                PosterCardView card = PosterCardView.Create(row, true);
                card.ConfigureScrollRects(horizontalScroll, _verticalScroll);
                card.Bind(
                    episode,
                    _api,
                    _imageCache,
                    selected => EpisodeSelected?.Invoke(selected),
                    cancellationToken,
                    640,
                    true);
                UiFactory.AddItemReveal(card.gameObject, Mathf.Min(0.20f, cardIndex * 0.025f));
                cardIndex++;
            }
        }

        private void UpdateSeasonSelection(int seasonNumber)
        {
            foreach (KeyValuePair<int, Button> pair in _seasonButtons)
            {
                bool selected = pair.Key == seasonNumber;
                Button button = pair.Value;
                if (button == null)
                {
                    continue;
                }

                Image background = button.targetGraphic as Image;
                if (background != null)
                {
                    background.color = selected
                        ? new Color(0.18f, 0.46f, 0.43f, 0.96f)
                        : UiTheme.SurfaceSoft;
                }

                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = selected ? UiTheme.TextPrimary : UiTheme.TextSecondary;
                }
            }

            foreach (KeyValuePair<int, GameObject> pair in _seasonIndicators)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(pair.Key == seasonNumber);
                }
            }
        }

        private static void AddTransparentDragSurface(RectTransform viewport)
        {
            Image dragSurface = viewport.gameObject.AddComponent<Image>();
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;
        }

        private static int SeasonSortKey(int seasonNumber)
        {
            if (seasonNumber == 0)
            {
                return int.MaxValue - 1;
            }
            return seasonNumber;
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
    }
}
