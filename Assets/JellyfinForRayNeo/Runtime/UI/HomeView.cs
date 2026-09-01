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
        private const float HeroHeight = 610f;
        private const float ContentSideMargin = 48f;

        private readonly GameObject _root;
        private readonly Text _serverLabel;
        private readonly Text _emptyLabel;
        private readonly ScrollRect _verticalScroll;
        private readonly RectTransform _content;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;

        private Image _heroBackdrop;
        private AspectRatioFitter _heroAspect;
        private Text _heroTitle;
        private Text _heroMetadata;
        private Text _heroOverview;
        private Button _heroAction;
        private JellyfinItem _heroItem;
        private int _heroBindingVersion;

        public event Action<JellyfinItem> ItemSelected;
        public event Action RefreshRequested;
        public event Action LogoutRequested;

        public HomeView(Transform parent, JellyfinApiClient api, JellyfinImageCache imageCache)
        {
            _api = api;
            _imageCache = imageCache;

            RectTransform rootRect = UiFactory.CreateRect("Home Screen", parent);
            UiFactory.Stretch(rootRect);
            _root = rootRect.gameObject;

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
            verticalLayout.padding = new RectOffset(0, 0, 0, 84);
            verticalLayout.spacing = 26f;
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

            _emptyLabel = UiFactory.CreateText(
                "Empty",
                rootRect,
                "媒体库中还没有可显示的电影或剧集",
                30,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                _emptyLabel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f),
                new Vector2(1000f, 100f));
            _emptyLabel.gameObject.SetActive(false);

            Image topScrim = UiFactory.CreateGradientPanel(
                "Top Scrim",
                rootRect,
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0f),
                UiTheme.Background);
            UiFactory.SetRect(
                topScrim.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 154f));

            Image headerShadow = UiFactory.CreateRoundedPanel(
                "Header Shadow",
                rootRect,
                new Color(0f, 0f, 0f, 0.42f));
            headerShadow.raycastTarget = false;
            UiFactory.SetRect(
                headerShadow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                new Vector2(-88f, 82f));

            Image header = UiFactory.CreateRoundedPanel("Header", rootRect, UiTheme.SurfaceGlass);
            UiFactory.SetRect(
                header.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -28f),
                new Vector2(-96f, 78f));
            Outline headerOutline = header.gameObject.AddComponent<Outline>();
            headerOutline.effectColor = UiTheme.Border;
            headerOutline.effectDistance = new Vector2(1f, -1f);
            headerOutline.useGraphicAlpha = true;

            Text brand = UiFactory.CreateText(
                "Brand",
                header.transform,
                "JELLYFIN",
                28,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                brand.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(24f, 1f),
                new Vector2(180f, 48f));

            Text rayneo = UiFactory.CreateText(
                "RayNeo",
                header.transform,
                "RAYNEO AIR",
                15,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                rayneo.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(184f, 0f),
                new Vector2(150f, 40f));

            Image activeNavigation = UiFactory.CreateRoundedPanel(
                "Active Navigation",
                header.transform,
                new Color(1f, 1f, 1f, 0.12f));
            activeNavigation.raycastTarget = false;
            UiFactory.SetRect(
                activeNavigation.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(112f, 46f));
            Text activeNavigationLabel = UiFactory.CreateText(
                "Active Navigation Label",
                activeNavigation.transform,
                "首页",
                20,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(activeNavigationLabel.rectTransform, 12f, 12f, 4f, 4f);

            _serverLabel = UiFactory.CreateText(
                "Server",
                header.transform,
                string.Empty,
                18,
                UiTheme.TextSecondary,
                TextAnchor.MiddleRight);
            _serverLabel.resizeTextForBestFit = true;
            _serverLabel.resizeTextMinSize = 14;
            _serverLabel.resizeTextMaxSize = 18;
            UiFactory.SetRect(
                _serverLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-220f, 0f),
                new Vector2(380f, 48f));

            Button refreshButton = UiFactory.CreateButton(
                "Refresh",
                header.transform,
                "刷新",
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                18);
            UiFactory.SetRect(
                refreshButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-108f, 0f),
                new Vector2(84f, 46f));
            refreshButton.onClick.AddListener(() => RefreshRequested?.Invoke());

            Button logoutButton = UiFactory.CreateButton(
                "Logout",
                header.transform,
                "退出",
                new Color(0.26f, 0.11f, 0.16f, 0.86f),
                UiTheme.TextPrimary,
                18);
            UiFactory.SetRect(
                logoutButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f),
                new Vector2(80f, 46f));
            logoutButton.onClick.AddListener(() => LogoutRequested?.Invoke());
        }

        public void Show(bool visible)
        {
            _root.SetActive(visible);
        }

        public void SetHeader(JellyfinSession session)
        {
            if (session == null)
            {
                _serverLabel.text = string.Empty;
                return;
            }

            string server = string.IsNullOrWhiteSpace(session.ServerName) ? "Jellyfin" : session.ServerName;
            string user = string.IsNullOrWhiteSpace(session.UserName) ? string.Empty : "  ·  " + session.UserName;
            _serverLabel.text = server + user;
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
            _emptyLabel.gameObject.SetActive(!hasSections);
            if (!hasSections)
            {
                return;
            }

            JellyfinItem heroItem = SelectHeroItem(populatedSections);
            if (heroItem != null)
            {
                CreateHero(heroItem, cancellationToken);
            }

            foreach (JellyfinHomeSection section in populatedSections)
            {
                CreateShelf(section, cancellationToken);
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

            Image heroShadow = UiFactory.CreateRoundedPanel(
                "Hero Shadow",
                hero,
                new Color(0f, 0f, 0f, 0.58f));
            heroShadow.raycastTarget = false;
            UiFactory.Stretch(heroShadow.rectTransform, ContentSideMargin - 5f, ContentSideMargin - 5f, 6f, -8f);

            Image heroFrame = UiFactory.CreateRoundedPanel(
                "Hero Frame",
                hero,
                new Color(0.055f, 0.06f, 0.08f, 1f));
            UiFactory.Stretch(heroFrame.rectTransform, ContentSideMargin, ContentSideMargin, 0f, 0f);
            Mask heroMask = heroFrame.gameObject.AddComponent<Mask>();
            heroMask.showMaskGraphic = true;

            _heroBackdrop = UiFactory.CreatePanel("Hero Backdrop", heroFrame.transform, Color.clear);
            _heroBackdrop.raycastTarget = false;
            _heroBackdrop.preserveAspect = false;
            UiFactory.SetRect(
                _heroBackdrop.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1824f, HeroHeight));
            _heroAspect = _heroBackdrop.gameObject.AddComponent<AspectRatioFitter>();
            _heroAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _heroAspect.aspectRatio = 16f / 9f;

            Image horizontalShade = UiFactory.CreateGradientPanel(
                "Hero Horizontal Shade",
                heroFrame.transform,
                new Color(0.012f, 0.014f, 0.024f, 0.93f),
                new Color(0.012f, 0.014f, 0.024f, 0.06f),
                true);
            UiFactory.Stretch(horizontalShade.rectTransform);

            Image verticalShade = UiFactory.CreateGradientPanel(
                "Hero Vertical Shade",
                heroFrame.transform,
                new Color(0.012f, 0.014f, 0.024f, 0.92f),
                new Color(0.012f, 0.014f, 0.024f, 0.02f));
            UiFactory.Stretch(verticalShade.rectTransform);

            Image badge = UiFactory.CreateRoundedPanel(
                "Featured Badge",
                heroFrame.transform,
                new Color(1f, 1f, 1f, 0.13f));
            badge.raycastTarget = false;
            UiFactory.SetRect(
                badge.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(52f, 304f),
                new Vector2(126f, 38f));
            Text badgeLabel = UiFactory.CreateText(
                "Featured Badge Label",
                badge.transform,
                "为你推荐",
                17,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(badgeLabel.rectTransform, 10f, 10f, 2f, 2f);

            _heroTitle = UiFactory.CreateText(
                "Hero Title",
                heroFrame.transform,
                string.Empty,
                54,
                UiTheme.TextPrimary,
                TextAnchor.LowerLeft,
                FontStyle.Bold);
            _heroTitle.resizeTextForBestFit = true;
            _heroTitle.resizeTextMinSize = 36;
            _heroTitle.resizeTextMaxSize = 54;
            UiFactory.SetRect(
                _heroTitle.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(52f, 218f),
                new Vector2(780f, 82f));

            _heroMetadata = UiFactory.CreateText(
                "Hero Metadata",
                heroFrame.transform,
                string.Empty,
                20,
                new Color(0.92f, 0.93f, 0.97f, 0.92f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _heroMetadata.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(54f, 176f),
                new Vector2(820f, 34f));

            _heroOverview = UiFactory.CreateText(
                "Hero Overview",
                heroFrame.transform,
                string.Empty,
                20,
                new Color(0.90f, 0.91f, 0.95f, 0.90f),
                TextAnchor.UpperLeft);
            _heroOverview.lineSpacing = 1.12f;
            UiFactory.SetRect(
                _heroOverview.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(54f, 98f),
                new Vector2(760f, 70f));

            _heroAction = UiFactory.CreateButton(
                "Hero Action",
                heroFrame.transform,
                "查看详情",
                new Color(0.98f, 0.985f, 1f, 0.96f),
                new Color(0.055f, 0.06f, 0.085f, 1f),
                21);
            UiFactory.SetRect(
                _heroAction.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(52f, 30f),
                new Vector2(178f, 58f));
            _heroAction.onClick.AddListener(() => ItemSelected?.Invoke(_heroItem));
            FocusScale heroFocus = _heroAction.GetComponent<FocusScale>();
            if (heroFocus != null)
            {
                heroFocus.FocusedScale = 1.06f;
                heroFocus.ConfigureScrollRects(null, _verticalScroll);
            }

            BindHero(item, cancellationToken);
        }

        private void BindHero(JellyfinItem item, CancellationToken cancellationToken)
        {
            _heroBindingVersion++;
            _heroItem = item;
            _heroTitle.text = item != null ? item.Name : string.Empty;
            _heroMetadata.text = BuildHeroMetadata(item);
            _heroOverview.text = item != null && !string.IsNullOrWhiteSpace(item.Overview)
                ? Condense(item.Overview, 112)
                : "从你的 Jellyfin 媒体库中精选，戴上眼镜即可进入沉浸观影。";
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
            if (sprite.rect.height > 0f)
            {
                _heroAspect.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        private void CreateShelf(JellyfinHomeSection section, CancellationToken cancellationToken)
        {
            bool landscape = IsLandscapeSection(section.Key);
            float shelfHeight = landscape ? 334f : 454f;

            RectTransform shelf = UiFactory.CreateRect("Shelf - " + section.Title, _content);
            LayoutElement shelfLayout = shelf.gameObject.AddComponent<LayoutElement>();
            shelfLayout.minHeight = shelfHeight;
            shelfLayout.preferredHeight = shelfHeight;
            shelfLayout.flexibleHeight = 0f;

            Text title = UiFactory.CreateText(
                "Shelf Title",
                shelf,
                section.Title,
                29,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -2f),
                new Vector2(-ContentSideMargin * 2f, 48f));

            RectTransform viewport = UiFactory.CreateRect("Viewport", shelf);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddTransparentDragSurface(viewport);
            UiFactory.SetRect(
                viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -27f),
                new Vector2(0f, -54f));

            RectTransform row = UiFactory.CreateRect("Cards", viewport);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = landscape ? 24f : 25f;
            layout.padding = new RectOffset(58, 80, 10, 10);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect horizontalScroll = shelf.gameObject.AddComponent<ScrollRect>();
            horizontalScroll.viewport = viewport;
            horizontalScroll.content = row;
            horizontalScroll.horizontal = true;
            horizontalScroll.vertical = false;
            horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
            horizontalScroll.elasticity = 0.085f;
            horizontalScroll.scrollSensitivity = 56f;
            horizontalScroll.decelerationRate = 0.11f;

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
                    landscape ? 760 : 480);
            }
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
                foreach (JellyfinItem item in section.Items)
                {
                    if (item == null)
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
                || string.Equals(key, "next-up", StringComparison.OrdinalIgnoreCase);
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
