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
        private readonly EmptyStateView _emptyState;
        private readonly Image _alphabetPanel;
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
        public event Action SearchModeRequested;
        public event Action FavoritesRequested;
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

            _alphabetPanel = UiFactory.CreateRoundedPanel(
                "Search Alphabet",
                rootImage.transform,
                UiTheme.SurfaceGlass);
            UiFactory.SetRect(
                _alphabetPanel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -198f),
                new Vector2(-96f, 64f));
            Outline alphabetOutline = _alphabetPanel.gameObject.AddComponent<Outline>();
            alphabetOutline.effectColor = UiTheme.Border;
            alphabetOutline.effectDistance = new Vector2(1f, -1f);
            HorizontalLayoutGroup alphabetLayout =
                _alphabetPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            alphabetLayout.padding = new RectOffset(14, 14, 7, 7);
            alphabetLayout.spacing = 7f;
            alphabetLayout.childAlignment = TextAnchor.MiddleCenter;
            alphabetLayout.childControlWidth = true;
            alphabetLayout.childControlHeight = true;
            alphabetLayout.childForceExpandWidth = false;
            alphabetLayout.childForceExpandHeight = false;
            CreateInitialButton(JellyfinTitleInitials.All, "全部", 70f, 0f);
            for (char initial = 'A'; initial <= 'Z'; initial++)
            {
                string value = initial.ToString();
                CreateInitialButton(value, value, 55f, (initial - 'A' + 1) * 0.008f);
            }
            CreateInitialButton(JellyfinTitleInitials.Other, "#", 55f, 0.22f);
            _alphabetPanel.gameObject.SetActive(false);

            _viewport = UiFactory.CreateRect("Browse Viewport", rootImage.transform);
            UiFactory.SetRect(
                _viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -96f),
                new Vector2(0f, -260f));
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
                new Vector2(0f, -45f),
                new Vector2(1080f, 330f));

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
            bool searchMode = _state.IsSearch;
            string searchInitial = JellyfinTitleInitials.NormalizeSelection(
                _state.SearchInitial);
            _state.SearchInitial = searchInitial;
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
                _pageLabel.text = total + " 的 " + start + "–" + end;
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
            _gridLayout.constraintCount = landscape ? 5 : 7;
            _gridLayout.cellSize = new Vector2(
                landscape ? PosterCardView.LandscapeWidth : PosterCardView.PosterWidth,
                landscape ? PosterCardView.LandscapeHeight : PosterCardView.PosterHeight);
            _gridLayout.spacing = new Vector2(landscape ? 22f : 27f, landscape ? 26f : 30f);
            _gridLayout.padding = landscape
                ? new RectOffset(44, 44, 18, 44)
                : new RectOffset(54, 54, 18, 44);
        }

        private void ConfigureViewport(bool searchMode)
        {
            _viewport.anchoredPosition = new Vector2(
                0f,
                searchMode ? -115f : -96f);
            _viewport.sizeDelta = new Vector2(
                0f,
                searchMode ? -330f : -260f);
        }

        private void CreateInitialButton(
            string selection,
            string label,
            float width,
            float revealDelay)
        {
            Button button = UiFactory.CreateButton(
                InitialButtonName(selection),
                _alphabetPanel.transform,
                label,
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                18);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = 50f;
            layout.preferredHeight = 50f;
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
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
