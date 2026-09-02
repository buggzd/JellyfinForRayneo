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
        private readonly Text _emptyLabel;
        private readonly ScrollRect _verticalScroll;
        private readonly RectTransform _seasonTabs;
        private readonly RectTransform _content;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;

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

            Image header = UiFactory.CreatePanel("Header", rootImage.transform, new Color(0.025f, 0.03f, 0.05f, 0.98f));
            UiFactory.SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 190f));

            _title = UiFactory.CreateText("Series Title", header.transform, string.Empty, 40, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 43f), new Vector2(-260f, 54f));

            _summary = UiFactory.CreateText("Series Summary", header.transform, string.Empty, 20, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            UiFactory.SetRect(_summary.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(-260f, 32f));

            Button close = UiFactory.CreateButton("Close", header.transform, "返回", UiTheme.SurfaceRaised, UiTheme.TextPrimary, 23);
            UiFactory.SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-52f, 43f), new Vector2(128f, 60f));
            close.onClick.AddListener(() => CloseRequested?.Invoke());

            RectTransform tabsViewport = UiFactory.CreateRect("Season Tabs Viewport", header.transform);
            tabsViewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.SetRect(tabsViewport, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(-104f, 56f));

            _seasonTabs = UiFactory.CreateRect("Season Tabs", tabsViewport);
            _seasonTabs.anchorMin = new Vector2(0f, 0.5f);
            _seasonTabs.anchorMax = new Vector2(0f, 0.5f);
            _seasonTabs.pivot = new Vector2(0f, 0.5f);
            _seasonTabs.anchoredPosition = Vector2.zero;
            _seasonTabs.sizeDelta = new Vector2(0f, 54f);
            HorizontalLayoutGroup tabsLayout = _seasonTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 14f;
            tabsLayout.padding = new RectOffset(4, 24, 2, 2);
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

            _emptyLabel = UiFactory.CreateText("Empty", rootImage.transform, "这部剧集暂时没有可播放的分集", 32, UiTheme.TextSecondary, TextAnchor.MiddleCenter);
            UiFactory.SetRect(_emptyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1000f, 100f));
            _emptyLabel.gameObject.SetActive(false);
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
                : string.Format("{0} 季  ·  {1} 集", seasonCount, playableEpisodes.Count);

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
            _emptyLabel.gameObject.SetActive(playableEpisodes.Count == 0);
            foreach (KeyValuePair<int, List<JellyfinItem>> season in seasons)
            {
                KeyValuePair<int, List<JellyfinItem>> selectedSeason = season;
                string title = SeasonTitle(selectedSeason.Key, selectedSeason.Value);
                Button tab = UiFactory.CreateButton("Season Tab - " + title, _seasonTabs, title, UiTheme.SurfaceRaised, UiTheme.TextPrimary, 21);
                LayoutElement layout = tab.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 178f;
                layout.preferredHeight = 50f;
                RectTransform tabRect = tab.GetComponent<RectTransform>();
                tabRect.sizeDelta = new Vector2(178f, 50f);
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

            Text title = UiFactory.CreateText("Season Title", shelf, SeasonTitle(seasonNumber, items), 32, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-10f, 52f));

            RectTransform viewport = UiFactory.CreateRect("Viewport", shelf);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddTransparentDragSurface(viewport);
            UiFactory.SetRect(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(0f, -62f));

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
