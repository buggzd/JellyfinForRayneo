using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class HomeView
    {
        private const float HeroHeight = 812f;

        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly LucentSideNavigation _navigation;
        private readonly EmptyStateView _emptyState;
        private readonly ScrollRect _verticalScroll;
        private readonly RectTransform _content;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;

        private Image _heroBackdrop;
        private AspectRatioFitter _heroAspect;
        private Text _heroOriginal;
        private Text _heroTitle;
        private Text _heroMetadata;
        private Text _heroOverview;
        private GameObject _heroProgress;
        private Text _heroProgressLabel;
        private Text _heroProgressValue;
        private Image _heroProgressFill;
        private Button _heroAction;
        private Text _heroActionLabel;
        private JellyfinItem _heroItem;
        private int _heroBindingVersion;

        public event Action<JellyfinItem> ItemSelected;
        public event Action<JellyfinItem, long> PlayRequested;
        public event Action LibraryRequested;
        public event Action SearchRequested;
        public event Action FavoritesRequested;
        public event Action RefreshRequested;
        public event Action LogoutRequested;

        public Transform FocusRoot => _root.transform;

        public HomeView(Transform parent, JellyfinApiClient api, JellyfinImageCache imageCache)
        {
            _api = api;
            _imageCache = imageCache;

            RectTransform rootRect = UiFactory.CreateRect("Home Screen", parent);
            UiFactory.Stretch(rootRect);
            _root = rootRect.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 18f, 0.995f);
            UiFactory.CreateAmbientBackdrop(rootRect);

            RectTransform viewport = UiFactory.CreateRect("Home Viewport", rootRect);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddTransparentDragSurface(viewport);
            UiFactory.Stretch(viewport);

            _content = UiFactory.CreateRect("Home Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup verticalLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(0, 0, 0, 108);
            verticalLayout.spacing = 48f;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlHeight = true;
            verticalLayout.childControlWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childForceExpandWidth = true;

            ContentSizeFitter contentFitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _verticalScroll = rootRect.gameObject.AddComponent<ScrollRect>();
            _verticalScroll.viewport = viewport;
            _verticalScroll.content = _content;
            _verticalScroll.horizontal = false;
            _verticalScroll.vertical = true;
            _verticalScroll.movementType = ScrollRect.MovementType.Elastic;
            _verticalScroll.elasticity = 0.085f;
            _verticalScroll.scrollSensitivity = 54f;
            _verticalScroll.decelerationRate = 0.11f;

            _emptyState = new EmptyStateView(
                rootRect,
                "Home Empty State",
                new Vector2(0f, -34f),
                new Vector2(1080f, 330f));
            _emptyState.SetContent(
                "LUCENT  ·  JELLYFIN LIBRARY",
                "媒体库还没有可展示的内容",
                "检查媒体库权限或等待 Jellyfin 完成扫描，然后从左侧导航刷新媒体库。",
                UiTheme.AccentBright);

            _navigation = new LucentSideNavigation(
                rootRect,
                LucentSideNavigation.Section.Home);
            _navigation.LibraryRequested += () => LibraryRequested?.Invoke();
            _navigation.SearchRequested += () => SearchRequested?.Invoke();
            _navigation.FavoritesRequested += () => FavoritesRequested?.Invoke();
            _navigation.RefreshRequested += () => RefreshRequested?.Invoke();
            _navigation.LogoutRequested += () => LogoutRequested?.Invoke();
            _motion.SetVisibleImmediately(false);
        }

        public void Show(bool visible)
        {
            if (visible)
            {
                _root.transform.SetAsLastSibling();
                _motion.Show();
            }
            else
            {
                _motion.Hide();
            }
        }

        public void SetHeader(JellyfinSession session)
        {
            _navigation.SetIdentity(session);
        }

        public void SetSections(IList<JellyfinHomeSection> sections, CancellationToken cancellationToken)
        {
            _heroBindingVersion++;
            UiFactory.DestroyChildren(_content);

            List<JellyfinHomeSection> populatedSections = new List<JellyfinHomeSection>();
            if (sections != null)
            {
                foreach (JellyfinHomeSection section in sections)
                {
                    if (section != null && section.Items != null && section.Items.Count > 0)
                    {
                        populatedSections.Add(section);
                    }
                }
            }

            bool hasSections = populatedSections.Count > 0;
            _emptyState.SetVisible(!hasSections);
            if (!hasSections)
            {
                return;
            }

            JellyfinItem heroItem = SelectHeroItem(populatedSections);
            if (heroItem != null)
            {
                CreateHero(heroItem, cancellationToken);
            }

            int shelfIndex = 0;
            foreach (JellyfinHomeSection section in populatedSections)
            {
                CreateShelf(section, cancellationToken, 0.04f + shelfIndex * 0.035f);
                shelfIndex++;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _verticalScroll.StopMovement();
            _verticalScroll.verticalNormalizedPosition = 1f;
        }

        private void CreateHero(JellyfinItem item, CancellationToken cancellationToken)
        {
            RectTransform hero = UiFactory.CreateRect("Featured Hero", _content);
            LayoutElement heroLayout = hero.gameObject.AddComponent<LayoutElement>();
            heroLayout.minHeight = HeroHeight;
            heroLayout.preferredHeight = HeroHeight;
            heroLayout.flexibleHeight = 0f;

            hero.gameObject.AddComponent<RectMask2D>();
            Image heroFrame = UiFactory.CreatePanel(
                "Hero Frame",
                hero,
                new Color(0.003f, 0.014f, 0.026f, 0.98f));
            heroFrame.raycastTarget = false;
            UiFactory.Stretch(heroFrame.rectTransform);

            _heroBackdrop = UiFactory.CreatePanel("Hero Backdrop", heroFrame.transform, Color.clear);
            _heroBackdrop.raycastTarget = false;
            _heroBackdrop.preserveAspect = false;
            UiFactory.SetRect(
                _heroBackdrop.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1920f, 1080f));
            _heroAspect = _heroBackdrop.gameObject.AddComponent<AspectRatioFitter>();
            _heroAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _heroAspect.aspectRatio = 16f / 9f;
            UiHeroBreath heroBreath = _heroBackdrop.gameObject.AddComponent<UiHeroBreath>();
            heroBreath.ScaleAmplitude = 0.016f;
            heroBreath.CycleSeconds = 24f;

            Image horizontalShade = UiFactory.CreateGradientPanel(
                "Hero Horizontal Shade",
                heroFrame.transform,
                new Color(0.002f, 0.014f, 0.025f, 0.99f),
                new Color(0.002f, 0.014f, 0.025f, 0.03f),
                true);
            UiFactory.Stretch(horizontalShade.rectTransform);

            Image verticalShade = UiFactory.CreateGradientPanel(
                "Hero Vertical Shade",
                heroFrame.transform,
                new Color(0.002f, 0.014f, 0.025f, 0.96f),
                new Color(0.002f, 0.014f, 0.025f, 0.10f));
            UiFactory.Stretch(verticalShade.rectTransform);

            Image horizonGlow = UiFactory.CreateGlowPanel(
                "Hero Horizon Glow",
                heroFrame.transform,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.075f));
            UiFactory.SetRect(
                horizonGlow.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(430f, 28f),
                new Vector2(920f, 610f));

            UiFactory.CreateFilmGrain(heroFrame.transform, 0.026f);

            Text eyebrow = UiFactory.CreateText(
                "Hero Eyebrow",
                heroFrame.transform,
                "LUCENT  ·  本周推荐",
                16,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                eyebrow.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft, 676f),
                new Vector2(620f, 32f));

            _heroOriginal = UiFactory.CreateText(
                "Hero Original Title",
                heroFrame.transform,
                string.Empty,
                15,
                UiTheme.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _heroOriginal.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft, 626f),
                new Vector2(850f, 30f));

            _heroTitle = UiFactory.CreateText(
                "Hero Title",
                heroFrame.transform,
                string.Empty,
                92,
                UiTheme.TextPrimary,
                TextAnchor.LowerLeft,
                FontStyle.Normal);
            _heroTitle.resizeTextForBestFit = true;
            _heroTitle.resizeTextMinSize = 48;
            _heroTitle.resizeTextMaxSize = 92;
            UiFactory.SetRect(
                _heroTitle.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft - 4f, 504f),
                new Vector2(940f, 118f));

            _heroMetadata = UiFactory.CreateText(
                "Hero Metadata",
                heroFrame.transform,
                string.Empty,
                18,
                new Color(0.88f, 0.95f, 0.98f, 0.72f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _heroMetadata.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft, 455f),
                new Vector2(840f, 32f));

            _heroOverview = UiFactory.CreateText(
                "Hero Overview",
                heroFrame.transform,
                string.Empty,
                18,
                new Color(0.84f, 0.91f, 0.94f, 0.58f),
                TextAnchor.UpperLeft);
            _heroOverview.lineSpacing = 1.28f;
            UiFactory.SetRect(
                _heroOverview.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft, 358f),
                new Vector2(740f, 76f));

            RectTransform progress = UiFactory.CreateRect("Hero Progress", heroFrame.transform);
            UiFactory.SetRect(
                progress,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft, 276f),
                new Vector2(590f, 60f));
            _heroProgress = progress.gameObject;
            _heroProgressLabel = UiFactory.CreateText(
                "Hero Progress Label",
                progress,
                string.Empty,
                14,
                UiTheme.TextSecondary,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                _heroProgressLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0.82f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            _heroProgressValue = UiFactory.CreateText(
                "Hero Progress Value",
                progress,
                string.Empty,
                14,
                UiTheme.TextSecondary,
                TextAnchor.UpperRight,
                FontStyle.Normal);
            UiFactory.SetRect(
                _heroProgressValue.rectTransform,
                new Vector2(0.82f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            Image progressTrack = UiFactory.CreateRoundedPanel(
                "Hero Progress Track",
                progress,
                UiTheme.ProgressTrack);
            progressTrack.raycastTarget = false;
            UiFactory.SetRect(
                progressTrack.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 7f),
                new Vector2(0f, 4f));
            _heroProgressFill = UiFactory.CreateRoundedPanel(
                "Hero Progress Fill",
                progressTrack.transform,
                UiTheme.Focus);
            _heroProgressFill.raycastTarget = false;
            _heroProgressFill.rectTransform.anchorMin = Vector2.zero;
            _heroProgressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _heroProgressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _heroProgressFill.rectTransform.anchoredPosition = Vector2.zero;
            _heroProgressFill.rectTransform.sizeDelta = Vector2.zero;

            _heroAction = UiFactory.CreateButton(
                "Hero Action",
                heroFrame.transform,
                "立即观看",
                new Color(0.91f, 0.985f, 1f, 0.94f),
                new Color(0.015f, 0.055f, 0.074f, 1f),
                20);
            UiFactory.SetRect(
                _heroAction.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft, 186f),
                new Vector2(184f, 58f));
            _heroActionLabel = _heroAction.transform.Find("Label").GetComponent<Text>();
            _heroAction.onClick.AddListener(() => PlayRequested?.Invoke(
                _heroItem,
                _heroItem != null && _heroItem.UserData != null
                    ? Math.Max(0L, _heroItem.UserData.PlaybackPositionTicks)
                    : 0L));
            FocusScale heroFocus = _heroAction.GetComponent<FocusScale>();
            if (heroFocus != null)
            {
                heroFocus.FocusedScale = 1.06f;
                heroFocus.ConfigureScrollRects(null, _verticalScroll);
            }

            Button detailAction = UiFactory.CreateButton(
                "Hero Details",
                heroFrame.transform,
                "查看详情",
                new Color(0.30f, 0.53f, 0.63f, 0.17f),
                UiTheme.TextPrimary,
                19);
            UiFactory.SetRect(
                detailAction.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(UiTheme.ContentLeft + 202f, 186f),
                new Vector2(188f, 58f));
            detailAction.onClick.AddListener(() => ItemSelected?.Invoke(_heroItem));
            FocusScale detailFocus = detailAction.GetComponent<FocusScale>();
            if (detailFocus != null)
            {
                detailFocus.FocusedScale = 1.045f;
                detailFocus.ConfigureScrollRects(null, _verticalScroll);
            }

            Text edition = UiFactory.CreateText(
                "Hero Edition",
                heroFrame.transform,
                "ORIGINAL SERIES\n01  /  SEASON",
                25,
                new Color(0.84f, 0.94f, 0.97f, 0.22f),
                TextAnchor.LowerRight,
                FontStyle.Normal);
            edition.lineSpacing = 0.72f;
            UiFactory.SetRect(
                edition.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-UiTheme.ContentRight, 128f),
                new Vector2(310f, 90f));

            Image scrollLine = UiFactory.CreatePanel(
                "Hero Scroll Cue Line",
                heroFrame.transform,
                new Color(UiTheme.Focus.r, UiTheme.Focus.g, UiTheme.Focus.b, 0.20f));
            scrollLine.raycastTarget = false;
            UiFactory.SetRect(
                scrollLine.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-42f, 38f),
                new Vector2(2f, 34f));
            Text scrollCue = UiFactory.CreateText(
                "Hero Scroll Cue",
                heroFrame.transform,
                "向下探索",
                12,
                UiTheme.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                scrollCue.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0f),
                new Vector2(-30f, 32f),
                new Vector2(110f, 40f));

            Image bottomMerge = UiFactory.CreateGradientPanel(
                "Hero Bottom Merge",
                heroFrame.transform,
                UiTheme.Background,
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0f));
            bottomMerge.raycastTarget = false;
            UiFactory.SetRect(
                bottomMerge.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 74f));
            bottomMerge.transform.SetSiblingIndex(scrollLine.transform.GetSiblingIndex());

            UiFactory.AddItemReveal(hero.gameObject, 0f);
            BindHero(item, cancellationToken);
        }

        private void BindHero(JellyfinItem item, CancellationToken cancellationToken)
        {
            _heroBindingVersion++;
            _heroItem = item;
            _heroOriginal.text = item != null && !string.IsNullOrWhiteSpace(item.OriginalTitle)
                ? item.OriginalTitle.ToUpperInvariant()
                : "JELLYFIN  /  CURATED FOR RAYNEO";
            _heroTitle.text = item != null ? item.Name : string.Empty;
            _heroMetadata.text = BuildHeroMetadata(item);
            _heroOverview.text = item != null && !string.IsNullOrWhiteSpace(item.Overview)
                ? Condense(item.Overview, 112)
                : "从你的 Jellyfin 媒体库中精选，戴上眼镜即可进入沉浸观影。";

            double watched = item != null && item.UserData != null
                && item.UserData.PlayedPercentage.HasValue
                    ? item.UserData.PlayedPercentage.Value
                    : 0d;
            bool resumable = watched > 0.1d && watched < 99.9d;
            _heroProgress.SetActive(resumable);
            _heroProgressFill.rectTransform.anchorMax = new Vector2(
                resumable ? Mathf.Clamp01((float)watched / 100f) : 0f,
                1f);
            _heroProgressLabel.text = resumable
                ? "上次看到  ·  " + (item.Name ?? "继续观看")
                : string.Empty;
            _heroProgressValue.text = resumable ? watched.ToString("0") + "%" : string.Empty;
            _heroActionLabel.text = resumable ? "继续观看" : "立即观看";
            _heroBackdrop.sprite = null;
            _heroBackdrop.color = Color.clear;

            string backdropUrl = item != null ? _api.BuildBackdropImageUrl(item, 1800) : null;
            string primaryUrl = item != null ? _api.BuildPrimaryImageUrl(item, 900) : null;
            if (!string.IsNullOrWhiteSpace(backdropUrl))
            {
                LoadHeroArtworkAsync(
                    backdropUrl,
                    primaryUrl,
                    _heroBindingVersion,
                    cancellationToken).Forget();
            }
        }

        private async Task LoadHeroArtworkAsync(
            string imageUrl,
            string fallbackUrl,
            int bindingVersion,
            CancellationToken cancellationToken)
        {
            Sprite sprite = null;
            try
            {
                sprite = await _imageCache.LoadSpriteAsync(imageUrl, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                if (!string.IsNullOrWhiteSpace(fallbackUrl)
                    && !string.Equals(imageUrl, fallbackUrl, StringComparison.Ordinal))
                {
                    try
                    {
                        sprite = await _imageCache.LoadSpriteAsync(fallbackUrl, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }
            }

            if (bindingVersion != _heroBindingVersion || _heroBackdrop == null || sprite == null)
            {
                return;
            }

            _heroBackdrop.sprite = sprite;
            _heroBackdrop.color = Color.white;
            UiFactory.RevealGraphic(_heroBackdrop, 0.38f);
            if (sprite.rect.height > 0f)
            {
                _heroAspect.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        private void CreateShelf(
            JellyfinHomeSection section,
            CancellationToken cancellationToken,
            float revealDelay)
        {
            bool landscape = IsLandscapeSection(section.Key);
            bool libraryCards = string.Equals(
                section.Key,
                "my-media",
                StringComparison.OrdinalIgnoreCase);
            float shelfHeight = landscape ? 356f : 472f;

            RectTransform shelf = UiFactory.CreateRect("Shelf - " + section.Title, _content);
            LayoutElement shelfLayout = shelf.gameObject.AddComponent<LayoutElement>();
            shelfLayout.minHeight = shelfHeight;
            shelfLayout.preferredHeight = shelfHeight;
            shelfLayout.flexibleHeight = 0f;
            UiFactory.AddScrollReveal(shelf.gameObject, _verticalScroll, revealDelay);

            Text eyebrow = UiFactory.CreateText(
                "Shelf Eyebrow",
                shelf,
                ShelfEyebrow(section.Key),
                12,
                UiTheme.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                eyebrow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft, -2f),
                new Vector2(520f, 24f));

            Text title = UiFactory.CreateText(
                "Shelf Title",
                shelf,
                section.Title,
                34,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(UiTheme.ContentLeft, -27f),
                new Vector2(820f, 48f));

            Text shelfCount = UiFactory.CreateText(
                "Shelf Count",
                shelf,
                (section.Items != null ? section.Items.Count : 0) + " 项  ·  横向浏览",
                13,
                UiTheme.TextMuted,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            UiFactory.SetRect(
                shelfCount.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-UiTheme.ContentRight, -29f),
                new Vector2(320f, 42f));

            RectTransform viewport = UiFactory.CreateRect("Viewport", shelf);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddTransparentDragSurface(viewport);
            UiFactory.SetRect(
                viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -42f),
                new Vector2(0f, -84f));

            RectTransform row = UiFactory.CreateRect("Cards", viewport);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = landscape ? 30f : 28f;
            layout.padding = new RectOffset(
                Mathf.RoundToInt(UiTheme.ContentLeft),
                Mathf.RoundToInt(UiTheme.ContentRight),
                24,
                14);
            layout.childAlignment = TextAnchor.UpperLeft;
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
            horizontalScroll.elasticity = 0.085f;
            horizontalScroll.scrollSensitivity = 56f;
            horizontalScroll.decelerationRate = 0.11f;
            horizontalScroll.ConfigureParent(_verticalScroll);

            int cardIndex = 0;
            foreach (JellyfinItem item in section.Items)
            {
                if (item == null)
                {
                    continue;
                }

                PosterCardView card = PosterCardView.Create(row, landscape);
                card.ConfigureScrollRects(horizontalScroll, _verticalScroll);
                card.Bind(
                    item,
                    _api,
                    _imageCache,
                    selected => ItemSelected?.Invoke(selected),
                    cancellationToken,
                    landscape ? 760 : 480,
                    libraryCards,
                    libraryCards);
                UiFactory.AddItemReveal(
                    card.gameObject,
                    Mathf.Min(0.22f, cardIndex * 0.026f));
                cardIndex++;
            }
        }

        private static string ShelfEyebrow(string key)
        {
            if (string.Equals(key, "my-media", StringComparison.OrdinalIgnoreCase))
            {
                return "YOUR JELLYFIN LIBRARIES";
            }
            if (string.Equals(key, "resume", StringComparison.OrdinalIgnoreCase))
            {
                return "PICK UP WHERE YOU LEFT OFF";
            }
            if (string.Equals(key, "next-up", StringComparison.OrdinalIgnoreCase))
            {
                return "NEXT IN YOUR SERIES";
            }
            if (!string.IsNullOrWhiteSpace(key)
                && key.StartsWith("genre-", StringComparison.OrdinalIgnoreCase))
            {
                return "CURATED BY GENRE";
            }
            return "RECENTLY ADDED";
        }

        private static void AddTransparentDragSurface(RectTransform viewport)
        {
            Image dragSurface = viewport.gameObject.AddComponent<Image>();
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;
        }

        private static JellyfinItem SelectHeroItem(IList<JellyfinHomeSection> sections)
        {
            JellyfinItem fallback = null;
            foreach (JellyfinHomeSection section in sections)
            {
                if (section == null
                    || string.Equals(section.Key, "my-media", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (JellyfinItem item in section.Items)
                {
                    if (item == null || item.IsBrowsableContainer)
                    {
                        continue;
                    }

                    if (fallback == null)
                    {
                        fallback = item;
                    }

                    if (item.BackdropImageTags != null && item.BackdropImageTags.Count > 0)
                    {
                        return item;
                    }
                }
            }

            return fallback;
        }

        private static bool IsLandscapeSection(string key)
        {
            return string.Equals(key, "resume", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "next-up", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "my-media", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildHeroMetadata(JellyfinItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            List<string> values = new List<string>();
            if (item.ProductionYear.HasValue)
            {
                values.Add(item.ProductionYear.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(item.Type))
            {
                values.Add(FriendlyType(item.Type));
            }

            if (item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0L)
            {
                TimeSpan duration = TimeSpan.FromSeconds(item.RunTimeTicks.Value / (double)AppConstants.TicksPerSecond);
                values.Add(duration.TotalHours >= 1d
                    ? string.Format("{0}小时{1}分", (int)duration.TotalHours, duration.Minutes)
                    : string.Format("{0}分钟", Math.Max(1, duration.Minutes)));
            }

            if (item.CommunityRating.HasValue)
            {
                values.Add(item.CommunityRating.Value.ToString("0.0") + " 分");
            }

            if (item.UserData != null
                && item.UserData.PlayedPercentage.HasValue
                && item.UserData.PlayedPercentage.Value > 0.1d
                && item.UserData.PlayedPercentage.Value < 99.9d)
            {
                values.Add("已观看 " + item.UserData.PlayedPercentage.Value.ToString("0") + "%");
            }

            return string.Join("   •   ", values.ToArray());
        }

        private static string FriendlyType(string type)
        {
            if (string.Equals(type, "Movie", StringComparison.OrdinalIgnoreCase))
            {
                return "电影";
            }
            if (string.Equals(type, "Series", StringComparison.OrdinalIgnoreCase))
            {
                return "剧集";
            }
            if (string.Equals(type, "Episode", StringComparison.OrdinalIgnoreCase))
            {
                return "单集";
            }
            return type;
        }

        private static string Condense(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (compact.Contains("  "))
            {
                compact = compact.Replace("  ", " ");
            }

            if (compact.Length <= maximumLength)
            {
                return compact;
            }

            return compact.Substring(0, Math.Max(1, maximumLength - 1)).TrimEnd() + "…";
        }
    }
}
