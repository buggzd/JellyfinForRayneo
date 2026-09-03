using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class BrowseView
    {
        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly LucentSideNavigation _navigation;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;
        private readonly Text _breadcrumb;
        private readonly Text _eyebrow;
        private readonly Text _title;
        private readonly Text _summary;
        private readonly Text _countLabel;
        private readonly Text _pageLabel;
        private readonly EmptyStateView _emptyState;
        private readonly Image _alphabetPanel;
        private readonly RectTransform _alphabetGrid;
        private readonly Text _searchSelectionValue;
        private readonly RectTransform _toolbar;
        private readonly Dictionary<string, Button> _initialButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Button _sortButton;
        private readonly Button _filterButton;
        private readonly Button _previousButton;
        private readonly Button _nextButton;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _viewport;
        private readonly RectTransform _grid;
        private readonly GridLayoutGroup _gridLayout;
        private JellyfinBrowseState _state;

        public event Action BackRequested;
        public event Action HomeRequested;
        public event Action LibraryRequested;
        public event Action SearchModeRequested;
        public event Action FavoritesRequested;
        public event Action RefreshRequested;
        public event Action LogoutRequested;
        public event Action<string> SearchInitialSelected;
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
            _motion = UiFactory.AddViewMotion(_root, 18f, 0.995f);
            UiFactory.CreateAmbientBackdrop(rootImage.transform);
            UiFactory.CreateFilmGrain(rootImage.transform, 0.018f);

            Image glow = UiFactory.CreateGradientPanel(
                "Browse Ambient Glow",
                rootImage.transform,
                new Color(0.15f, 0.54f, 0.68f, 0.13f),
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0f),
                true);
            UiFactory.SetRect(
                glow.rectTransform,
                new Vector2(0f, 0.6f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            Button back = UiFactory.CreateButton(
                "Browse Back",
                rootImage.transform,
                "返回",
                new Color(0.24f, 0.46f, 0.56f, 0.16f),
                UiTheme.TextPrimary,
                17);
            UiFactory.SetRect(
                back.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft, -34f),
                new Vector2(92f, 50f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            Button home = UiFactory.CreateButton(
                "Browse Home",
                rootImage.transform,
                "首页",
                Color.clear,
                UiTheme.TextSecondary,
                16);
            UiFactory.SetRect(
                home.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft + 110f, -34f),
                new Vector2(74f, 50f));
            home.onClick.AddListener(() => HomeRequested?.Invoke());

            Text divider = UiFactory.CreateText(
                "Browse Breadcrumb Divider",
                rootImage.transform,
                "/",
                15,
                UiTheme.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            UiFactory.SetRect(
                divider.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft + 192f, -34f),
                new Vector2(24f, 50f));

            _breadcrumb = UiFactory.CreateText(
                "Browse Breadcrumb",
                rootImage.transform,
                "媒体库",
                15,
                UiTheme.TextSecondary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _breadcrumb.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft + 222f, -34f),
                new Vector2(520f, 50f));

            _eyebrow = UiFactory.CreateText(
                "Browse Eyebrow",
                rootImage.transform,
                "ALL JELLYFIN LIBRARIES",
                12,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _eyebrow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft, -94f),
                new Vector2(700f, 26f));

            _title = UiFactory.CreateText(
                "Browse Title",
                rootImage.transform,
                "媒体库",
                54,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 34;
            _title.resizeTextMaxSize = 54;
            UiFactory.SetRect(
                _title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft - 2f, -122f),
                new Vector2(980f, 66f));

            _summary = UiFactory.CreateText(
                "Browse Summary",
                rootImage.transform,
                string.Empty,
                16,
                UiTheme.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _summary.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft, -184f),
                new Vector2(980f, 30f));

            Image layoutIndicator = UiFactory.CreateRoundedPanel(
                "Browse Layout Indicator",
                rootImage.transform,
                new Color(0.30f, 0.54f, 0.64f, 0.12f));
            layoutIndicator.raycastTarget = false;
            UiFactory.SetRect(
                layoutIndicator.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-UiTheme.ContentRight, -132f),
                new Vector2(178f, 46f));
            Text layoutLabel = UiFactory.CreateText(
                "Browse Layout Label",
                layoutIndicator.transform,
                "▦  海报网格",
                14,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            UiFactory.Stretch(layoutLabel.rectTransform, 12f, 12f, 4f, 4f);

            Image toolbarImage = UiFactory.CreateGlassPanel(
                "Browse Toolbar",
                rootImage.transform,
                new Color(0.055f, 0.125f, 0.169f, 0.54f),
                new Vector2(0f, -8f));
            _toolbar = toolbarImage.rectTransform;
            UiFactory.SetRect(
                _toolbar,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2((UiTheme.ContentLeft - UiTheme.ContentRight) * 0.5f, -224f),
                new Vector2(-UiTheme.ContentLeft - UiTheme.ContentRight, 76f));

            _countLabel = UiFactory.CreateText(
                "Browse Count",
                _toolbar,
                string.Empty,
                16,
                UiTheme.TextSecondary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _countLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(24f, 0f),
                new Vector2(500f, 56f));

            _sortButton = CreateToolbarButton("Browse Sort", _toolbar, "排序：名称 A–Z", 212f);
            SetToolbarRightRect(_sortButton, 530f, 212f);
            _sortButton.onClick.AddListener(() => SortRequested?.Invoke());

            _filterButton = CreateToolbarButton("Browse Filter", _toolbar, "筛选：全部", 174f);
            SetToolbarRightRect(_filterButton, 344f, 174f);
            _filterButton.onClick.AddListener(() => FilterRequested?.Invoke());

            _previousButton = CreateToolbarButton("Previous Page", _toolbar, "‹", 54f);
            SetToolbarRightRect(_previousButton, 84f, 54f);
            _previousButton.onClick.AddListener(() => PreviousPageRequested?.Invoke());

            _nextButton = CreateToolbarButton("Next Page", _toolbar, "›", 54f);
            SetToolbarRightRect(_nextButton, 20f, 54f);
            _nextButton.onClick.AddListener(() => NextPageRequested?.Invoke());

            _pageLabel = UiFactory.CreateText(
                "Page Range",
                _toolbar,
                string.Empty,
                13,
                UiTheme.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            UiFactory.SetRect(
                _pageLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-148f, 0f),
                new Vector2(180f, 42f));

            _alphabetPanel = UiFactory.CreateGlassPanel(
                "Search Alphabet",
                rootImage.transform,
                new Color(0.055f, 0.125f, 0.169f, 0.62f),
                new Vector2(0f, -12f));
            UiFactory.SetRect(
                _alphabetPanel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2((UiTheme.ContentLeft - UiTheme.ContentRight) * 0.5f, -224f),
                new Vector2(-UiTheme.ContentLeft - UiTheme.ContentRight, 286f));

            Text searchMark = UiFactory.CreateText(
                "Search Console Mark",
                _alphabetPanel.transform,
                "⌕",
                28,
                UiTheme.Focus,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            UiFactory.SetRect(
                searchMark.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -18f),
                new Vector2(42f, 48f));

            Text searchEyebrow = UiFactory.CreateText(
                "Search Console Eyebrow",
                _alphabetPanel.transform,
                "SEARCH ALL JELLYFIN LIBRARIES",
                10,
                UiTheme.TextMuted,
                TextAnchor.LowerLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                searchEyebrow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(76f, -13f),
                new Vector2(520f, 22f));

            _searchSelectionValue = UiFactory.CreateText(
                "Search Console Value",
                _alphabetPanel.transform,
                "选择首字母",
                22,
                UiTheme.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _searchSelectionValue.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(76f, -36f),
                new Vector2(720f, 34f));

            _alphabetGrid = UiFactory.CreateRect("Search Keyboard", _alphabetPanel.transform);
            UiFactory.Stretch(_alphabetGrid, 24f, 24f, 80f, 16f);
            GridLayoutGroup alphabetLayout = _alphabetGrid.gameObject.AddComponent<GridLayoutGroup>();
            alphabetLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            alphabetLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            alphabetLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            alphabetLayout.constraintCount = 10;
            alphabetLayout.cellSize = new Vector2(142f, 54f);
            alphabetLayout.spacing = new Vector2(12f, 10f);
            alphabetLayout.childAlignment = TextAnchor.UpperCenter;
            CreateInitialButton(JellyfinTitleInitials.All, "全部", 0f);
            for (char initial = 'A'; initial <= 'Z'; initial++)
            {
                string value = initial.ToString();
                CreateInitialButton(value, value, (initial - 'A' + 1) * 0.008f);
            }
            CreateInitialButton(JellyfinTitleInitials.Other, "#", 0.22f);
            _alphabetPanel.gameObject.SetActive(false);

            _viewport = UiFactory.CreateRect("Browse Viewport", rootImage.transform);
            UiFactory.SetRect(
                _viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -154f),
                new Vector2(0f, -332f));
            Image dragSurface = _viewport.gameObject.AddComponent<Image>();
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;
            _viewport.gameObject.AddComponent<RectMask2D>();

            _grid = UiFactory.CreateRect("Browse Grid", _viewport);
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
            _scroll.viewport = _viewport;
            _scroll.content = _grid;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.085f;
            _scroll.decelerationRate = 0.11f;
            _scroll.scrollSensitivity = 58f;

            _emptyState = new EmptyStateView(
                rootImage.transform,
                "Browse Empty State",
                new Vector2(42f, -128f),
                new Vector2(1080f, 330f));

            _navigation = new LucentSideNavigation(
                rootImage.transform,
                LucentSideNavigation.Section.Library);
            _navigation.HomeRequested += () => HomeRequested?.Invoke();
            _navigation.LibraryRequested += () => LibraryRequested?.Invoke();
            _navigation.SearchRequested += () => SearchModeRequested?.Invoke();
            _navigation.FavoritesRequested += () => FavoritesRequested?.Invoke();
            _navigation.RefreshRequested += () => RefreshRequested?.Invoke();
            _navigation.LogoutRequested += () => LogoutRequested?.Invoke();

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

        public string PreferredSearchFocusName
        {
            get
            {
                string selection = _state != null
                    ? JellyfinTitleInitials.NormalizeSelection(_state.SearchInitial)
                    : null;
                return InitialButtonName(selection ?? "A");
            }
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
            _breadcrumb.text = _title.text;
            bool searchMode = _state.IsSearch;
            string searchInitial = JellyfinTitleInitials.NormalizeSelection(
                _state.SearchInitial);
            _state.SearchInitial = searchInitial;
            _eyebrow.text = searchMode
                ? "SEARCH EVERYWHERE"
                : _state.IsFavorites
                    ? "SAVED MOMENTS"
                    : string.IsNullOrWhiteSpace(_state.ParentId)
                        ? "ALL JELLYFIN LIBRARIES"
                        : "LIBRARY / FOLDER VIEW";
            _navigation.SetIdentity(_api.Session);
            _navigation.SetActive(
                searchMode
                    ? LucentSideNavigation.Section.Search
                    : _state.IsFavorites
                        ? LucentSideNavigation.Section.Favorites
                        : LucentSideNavigation.Section.Library);
            _alphabetPanel.gameObject.SetActive(searchMode);
            ConfigureViewport(searchMode);
            UpdateInitialSelection(searchInitial);

            int total = Math.Max(0, result.TotalRecordCount);
            int start = items.Count > 0 ? Math.Max(0, result.StartIndex) + 1 : 0;
            int end = items.Count > 0 ? start + items.Count - 1 : 0;
            if (searchMode && searchInitial == null)
            {
                _countLabel.text = "选择首字母  ·  中文拼音 / ENGLISH";
                _pageLabel.text = string.Empty;
                _searchSelectionValue.text = "选择首字母";
                _summary.text = "使用遥控器与首字母键盘搜索全部媒体库";
            }
            else
            {
                string initialLabel = searchInitial == JellyfinTitleInitials.All
                    ? "全部"
                    : searchInitial;
                _countLabel.text = searchMode
                    ? "首字母 " + initialLabel + "  ·  共 " + total + " 项"
                    : total > 0
                        ? "共 " + total + " 项"
                        : "此位置暂无内容";
                _pageLabel.text = start + "–" + end + "  /  " + total;
                _searchSelectionValue.text = searchMode
                    ? "当前索引  ·  " + initialLabel
                    : string.Empty;
                _summary.text = searchMode
                    ? "正在全部媒体库中查找首字母 “" + initialLabel + "”"
                    : total + " 个项目  ·  JELLYFIN / "
                        + (_api.Session != null && !string.IsNullOrWhiteSpace(_api.Session.ServerName)
                            ? _api.Session.ServerName
                            : "SERVER");
            }
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
            RectTransform emptyRect = _emptyState.Root.GetComponent<RectTransform>();
            emptyRect.anchoredPosition = searchMode
                ? new Vector2(42f, -316f)
                : new Vector2(42f, -128f);
            if (empty)
            {
                if (searchMode && searchInitial == null)
                {
                    _emptyState.SetContent(
                        "SEARCH  ·  INITIAL INDEX",
                        "选择一个首字母",
                        "用手机触控板选择 A–Z；中文按拼音首字母，英文按标题首字母。",
                        UiTheme.AccentSecondary);
                }
                else if (searchMode)
                {
                    string initialLabel = searchInitial == JellyfinTitleInitials.All
                        ? "全部"
                        : searchInitial;
                    _emptyState.SetContent(
                        "SEARCH  ·  NO RESULTS",
                        "没有找到首字母为 " + initialLabel + " 的内容",
                        "可选择其他字母，或调整当前排序与筛选条件。",
                        UiTheme.AccentSecondary);
                }
                else
                {
                    _emptyState.SetContent(
                        "JELLYFIN  ·  LIBRARY",
                        "这里暂时没有内容",
                        "调整筛选条件，或等待 Jellyfin 完成媒体库扫描。",
                        UiTheme.AccentBright);
                }
            }
            _emptyState.SetVisible(empty);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_grid);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
            Show();
        }

        private void ConfigureGrid(bool landscape)
        {
            _gridLayout.constraintCount = landscape ? 4 : 6;
            _gridLayout.cellSize = new Vector2(
                landscape ? PosterCardView.LandscapeWidth : PosterCardView.PosterWidth,
                landscape ? PosterCardView.LandscapeHeight : PosterCardView.PosterHeight);
            _gridLayout.spacing = new Vector2(landscape ? 26f : 28f, landscape ? 34f : 38f);
            _gridLayout.padding = landscape
                ? new RectOffset(
                    Mathf.RoundToInt(UiTheme.ContentLeft),
                    Mathf.RoundToInt(UiTheme.ContentRight),
                    28,
                    58)
                : new RectOffset(
                    Mathf.RoundToInt(UiTheme.ContentLeft),
                    Mathf.RoundToInt(UiTheme.ContentRight),
                    28,
                    58);
        }

        private void ConfigureViewport(bool searchMode)
        {
            Vector2 toolbarPosition = _toolbar.anchoredPosition;
            toolbarPosition.y = searchMode ? -530f : -224f;
            _toolbar.anchoredPosition = toolbarPosition;
            UiFactory.Stretch(
                _viewport,
                0f,
                0f,
                searchMode ? 626f : 318f,
                0f);
        }

        private void CreateInitialButton(
            string selection,
            string label,
            float revealDelay)
        {
            Button button = UiFactory.CreateButton(
                InitialButtonName(selection),
                _alphabetGrid,
                label,
                new Color(0.24f, 0.48f, 0.58f, 0.16f),
                UiTheme.TextPrimary,
                18);
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            FocusScale focus = button.GetComponent<FocusScale>();
            focus.FocusedScale = 1.045f;
            focus.LocalDepthOffset = -8f;
            button.onClick.AddListener(() => SearchInitialSelected?.Invoke(selection));
            UiFactory.AddItemReveal(button.gameObject, revealDelay);
            _initialButtons[selection] = button;
        }

        private void UpdateInitialSelection(string selection)
        {
            foreach (KeyValuePair<string, Button> entry in _initialButtons)
            {
                bool selected = string.Equals(
                    entry.Key,
                    selection,
                    StringComparison.Ordinal);
                Image image = entry.Value.GetComponent<Image>();
                Text label = entry.Value.GetComponentInChildren<Text>();
                Outline outline = entry.Value.GetComponent<Outline>();
                image.color = selected ? UiTheme.AccentBright : UiTheme.SurfaceSoft;
                label.color = selected
                    ? new Color(0.02f, 0.035f, 0.05f, 1f)
                    : UiTheme.TextPrimary;
                outline.effectColor = selected
                    ? new Color(
                        UiTheme.AccentBright.r,
                        UiTheme.AccentBright.g,
                        UiTheme.AccentBright.b,
                        0.72f)
                    : UiTheme.Border;
            }
        }

        private static string InitialButtonName(string selection)
        {
            if (selection == JellyfinTitleInitials.All)
            {
                return "Search Initial All";
            }
            if (selection == JellyfinTitleInitials.Other)
            {
                return "Search Initial Other";
            }
            return "Search Initial " + selection;
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
