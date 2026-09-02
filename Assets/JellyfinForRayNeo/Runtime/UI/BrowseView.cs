using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class BrowseView
    {
        private const float HeaderSideMargin = 44f;

        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;
        private readonly Text _title;
        private readonly Text _countLabel;
        private readonly Text _pageLabel;
        private readonly Text _emptyLabel;
        private readonly InputField _searchInput;
        private readonly Button _searchSubmitButton;
        private readonly Button _sortButton;
        private readonly Button _filterButton;
        private readonly Button _previousButton;
        private readonly Button _nextButton;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _grid;
        private readonly GridLayoutGroup _gridLayout;
        private JellyfinBrowseState _state;
        private bool _updatingSearchText;
        private string _lastSubmittedSearch;
        private float _lastSearchSubmittedAt = -10f;

        public event Action BackRequested;
        public event Action HomeRequested;
        public event Action SearchModeRequested;
        public event Action FavoritesRequested;
        public event Action<string> SearchSubmitted;
        public event Action SortRequested;
        public event Action FilterRequested;
        public event Action PreviousPageRequested;
        public event Action NextPageRequested;
        public event Action<JellyfinItem> ItemSelected;

        public Transform FocusRoot => _root.transform;

        public BrowseView(
            Transform parent,
            JellyfinApiClient api,
            JellyfinImageCache imageCache)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));

            Image rootImage = UiFactory.CreatePanel("Browse Screen", parent, UiTheme.Background);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 24f, 0.99f);
            UiFactory.CreateAmbientBackdrop(rootImage.transform);

            Image glow = UiFactory.CreateGradientPanel(
                "Browse Ambient Glow",
                rootImage.transform,
                new Color(0.08f, 0.26f, 0.31f, 0.20f),
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0f),
                true);
            UiFactory.SetRect(
                glow.rectTransform,
                new Vector2(0f, 0.6f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            Image header = UiFactory.CreateRoundedPanel(
                "Browse Header",
                rootImage.transform,
                UiTheme.SurfaceGlass);
            UiFactory.SetRect(
                header.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -28f),
                new Vector2(-HeaderSideMargin * 2f, 84f));
            Outline headerOutline = header.gameObject.AddComponent<Outline>();
            headerOutline.effectColor = UiTheme.Border;
            headerOutline.effectDistance = new Vector2(1f, -1f);

            Button back = CreateHeaderButton("Browse Back", header.transform, "返回", 20);
            SetHeaderButtonRect(back, 18f, 96f);
            back.onClick.AddListener(() => BackRequested?.Invoke());

            Button home = CreateHeaderButton("Browse Home", header.transform, "首页", 20);
            SetHeaderButtonRect(home, 124f, 96f);
            home.onClick.AddListener(() => HomeRequested?.Invoke());

            Text eyebrow = UiFactory.CreateText(
                "Browse Eyebrow",
                header.transform,
                "JELLYFIN  ·  AIR BROWSE",
                14,
                UiTheme.AccentBright,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                eyebrow.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(244f, 15f),
                new Vector2(460f, 24f));

            _title = UiFactory.CreateText(
                "Browse Title",
                header.transform,
                "媒体库",
                29,
                UiTheme.TextPrimary,
                TextAnchor.LowerLeft,
                FontStyle.Bold);
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 20;
            _title.resizeTextMaxSize = 29;
            UiFactory.SetRect(
                _title.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-96f, -13f),
                new Vector2(-820f, 38f));

            Button favorites = CreateHeaderButton("Browse Favorites", header.transform, "收藏", 19);
            SetRightHeaderButtonRect(favorites, 138f, 104f);
            favorites.onClick.AddListener(() => FavoritesRequested?.Invoke());

            Button search = CreateHeaderButton("Browse Search", header.transform, "搜索", 19);
            SetRightHeaderButtonRect(search, 24f, 104f);
            search.onClick.AddListener(() => SearchModeRequested?.Invoke());

            RectTransform toolbar = UiFactory.CreateRect("Browse Toolbar", rootImage.transform);
            UiFactory.SetRect(
                toolbar,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -126f),
                new Vector2(-96f, 76f));

            _searchInput = UiFactory.CreateInputField(
                "Search Input",
                toolbar,
                "输入片名、剧集、演员或课程名称",
                InputField.ContentType.Standard);
            UiFactory.SetRect(
                _searchInput.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(690f, 60f));
            _searchInput.onEndEdit.AddListener(value =>
            {
                if (!_updatingSearchText)
                {
                    RequestSearch(value);
                }
            });

            _searchSubmitButton = UiFactory.CreateButton(
                "Submit Search",
                toolbar,
                "开始搜索",
                UiTheme.Focus,
                new Color(0.02f, 0.03f, 0.045f, 1f),
                19);
            UiFactory.SetRect(
                _searchSubmitButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(704f, 0f),
                new Vector2(142f, 60f));
            _searchSubmitButton.onClick.AddListener(() => RequestSearch(_searchInput.text));

            _countLabel = UiFactory.CreateText(
                "Browse Count",
                toolbar,
                string.Empty,
                20,
                UiTheme.TextSecondary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _countLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(620f, 60f));

            _sortButton = CreateToolbarButton("Browse Sort", toolbar, "排序：名称 A–Z", 232f);
            SetToolbarRightRect(_sortButton, 518f, 232f);
            _sortButton.onClick.AddListener(() => SortRequested?.Invoke());

            _filterButton = CreateToolbarButton("Browse Filter", toolbar, "筛选：全部", 188f);
            SetToolbarRightRect(_filterButton, 320f, 188f);
            _filterButton.onClick.AddListener(() => FilterRequested?.Invoke());

            _previousButton = CreateToolbarButton("Previous Page", toolbar, "上一页", 110f);
            SetToolbarRightRect(_previousButton, 200f, 110f);
            _previousButton.onClick.AddListener(() => PreviousPageRequested?.Invoke());

            _nextButton = CreateToolbarButton("Next Page", toolbar, "下一页", 110f);
            SetToolbarRightRect(_nextButton, 80f, 110f);
            _nextButton.onClick.AddListener(() => NextPageRequested?.Invoke());

            _pageLabel = UiFactory.CreateText(
                "Page Range",
                toolbar,
                string.Empty,
                17,
                UiTheme.TextMuted,
                TextAnchor.MiddleRight,
                FontStyle.Bold);
            UiFactory.SetRect(
                _pageLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-4f, -46f),
                new Vector2(320f, 28f));

            RectTransform viewport = UiFactory.CreateRect("Browse Viewport", rootImage.transform);
            UiFactory.SetRect(
                viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -96f),
                new Vector2(0f, -260f));
            Image dragSurface = viewport.gameObject.AddComponent<Image>();
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            _grid = UiFactory.CreateRect("Browse Grid", viewport);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.anchoredPosition = Vector2.zero;
            _grid.sizeDelta = Vector2.zero;

            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            _gridLayout.childAlignment = TextAnchor.UpperCenter;
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

            ContentSizeFitter gridFitter = _grid.gameObject.AddComponent<ContentSizeFitter>();
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = rootImage.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _grid;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.085f;
            _scroll.decelerationRate = 0.11f;
            _scroll.scrollSensitivity = 58f;

            _emptyLabel = UiFactory.CreateText(
                "Browse Empty",
                rootImage.transform,
                string.Empty,
                29,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _emptyLabel.lineSpacing = 1.25f;
            UiFactory.SetRect(
                _emptyLabel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f),
                new Vector2(1180f, 180f));

            _motion.SetVisibleImmediately(false);
        }

        public bool IsVisible
        {
            get { return _motion.IsVisible; }
        }

        public JellyfinBrowseState CurrentState
        {
            get { return _state != null ? _state.Clone() : null; }
        }

        public void Show()
        {
            _root.transform.SetAsLastSibling();
            _motion.Show();
        }

        public void Hide()
        {
            _motion.Hide();
        }

        public void SetPage(
            JellyfinBrowseState state,
            JellyfinQueryResult result,
            CancellationToken cancellationToken)
        {
            _state = state != null ? state.Clone() : new JellyfinBrowseState();
            result = result ?? new JellyfinQueryResult();
            List<JellyfinItem> items = result.Items ?? new List<JellyfinItem>();

            _title.text = string.IsNullOrWhiteSpace(_state.Title) ? "浏览" : _state.Title;
            bool searchMode = _state.IsSearch;
            _searchInput.gameObject.SetActive(searchMode);
            _searchSubmitButton.gameObject.SetActive(searchMode);
            _countLabel.gameObject.SetActive(!searchMode);
            _updatingSearchText = true;
            _searchInput.text = _state.SearchTerm ?? string.Empty;
            _updatingSearchText = false;

            int total = Math.Max(0, result.TotalRecordCount);
            int start = items.Count > 0 ? Math.Max(0, result.StartIndex) + 1 : 0;
            int end = items.Count > 0 ? start + items.Count - 1 : 0;
            _countLabel.text = total > 0
                ? "共 " + total + " 项"
                : "此位置暂无内容";
            _pageLabel.text = total + " 的 " + start + "–" + end;
            _previousButton.interactable = _state.StartIndex > 0;
            _nextButton.interactable = end > 0 && end < total;
            _sortButton.GetComponentInChildren<Text>().text =
                "排序：" + BrowseCatalogService.SortLabel(_state.Sort);
            _filterButton.GetComponentInChildren<Text>().text =
                "筛选：" + BrowseCatalogService.FilterLabel(_state.Filter);

            ConfigureGrid(_state.PreferLandscape);
            UiFactory.DestroyChildren(_grid);
            int cardIndex = 0;
            foreach (JellyfinItem item in items)
            {
                if (item == null)
                {
                    continue;
                }

                PosterCardView card = PosterCardView.Create(_grid, _state.PreferLandscape);
                card.ConfigureScrollRects(null, _scroll);
                card.Bind(
                    item,
                    _api,
                    _imageCache,
                    selected => ItemSelected?.Invoke(selected),
                    cancellationToken,
                    _state.PreferLandscape ? 760 : 480,
                    _state.PreferLandscape || item.IsBrowsableContainer);
                UiFactory.AddItemReveal(card.gameObject, Mathf.Min(0.22f, cardIndex * 0.018f));
                cardIndex++;
            }

            bool empty = items.Count == 0;
            _emptyLabel.gameObject.SetActive(empty);
            if (empty)
            {
                _emptyLabel.text = searchMode && string.IsNullOrWhiteSpace(_state.SearchTerm)
                    ? "搜索你的 Jellyfin\n在手机键盘输入关键词，然后选择“开始搜索”"
                    : searchMode
                        ? "没有找到匹配内容\n换一个关键词再试试"
                        : "这里暂时没有可显示的内容";
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_grid);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
            Show();
        }

        private void ConfigureGrid(bool landscape)
        {
            _gridLayout.constraintCount = landscape ? 5 : 7;
            _gridLayout.cellSize = new Vector2(
                landscape ? PosterCardView.LandscapeWidth : PosterCardView.PosterWidth,
                landscape ? PosterCardView.LandscapeHeight : PosterCardView.PosterHeight);
            _gridLayout.spacing = new Vector2(landscape ? 22f : 27f, landscape ? 26f : 30f);
            _gridLayout.padding = landscape
                ? new RectOffset(44, 44, 18, 44)
                : new RectOffset(54, 54, 18, 44);
        }

        private void RequestSearch(string value)
        {
            string query = value ?? string.Empty;
            if (string.Equals(query, _lastSubmittedSearch, StringComparison.Ordinal)
                && Time.unscaledTime - _lastSearchSubmittedAt < 0.25f)
            {
                return;
            }

            _lastSubmittedSearch = query;
            _lastSearchSubmittedAt = Time.unscaledTime;
            SearchSubmitted?.Invoke(query);
        }

        private static Button CreateHeaderButton(
            string name,
            Transform parent,
            string label,
            int fontSize)
        {
            return UiFactory.CreateButton(
                name,
                parent,
                label,
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                fontSize);
        }

        private static Button CreateToolbarButton(
            string name,
            Transform parent,
            string label,
            float width)
        {
            Button button = UiFactory.CreateButton(
                name,
                parent,
                label,
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                17);
            button.GetComponentInChildren<Text>().resizeTextForBestFit = true;
            button.GetComponentInChildren<Text>().resizeTextMinSize = 14;
            button.GetComponentInChildren<Text>().resizeTextMaxSize = 17;
            return button;
        }

        private static void SetHeaderButtonRect(Button button, float left, float width)
        {
            UiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(left, 0f),
                new Vector2(width, 52f));
        }

        private static void SetRightHeaderButtonRect(Button button, float right, float width)
        {
            UiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-right, 0f),
                new Vector2(width, 52f));
        }

        private static void SetToolbarRightRect(Button button, float right, float width)
        {
            UiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-right, 0f),
                new Vector2(width, 54f));
        }
    }
}
