using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class DetailView
    {
        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly Image _backdrop;
        private readonly Image _poster;
        private readonly Text _posterPlaceholder;
        private readonly Text _posterPlaceholderCaption;
        private readonly UiArtworkPlaceholderMotion _posterPlaceholderMotion;
        private readonly GameObject _posterFrame;
        private readonly GameObject _heroInformation;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly Text _kindLabel;
        private readonly Text _title;
        private readonly Text _originalTitle;
        private readonly RectTransform _metadataChips;
        private readonly Text _tagline;
        private readonly Text _overview;
        private readonly GameObject _overviewToggleRow;
        private readonly Button _overviewToggle;
        private readonly Text _overviewToggleLabel;
        private readonly Image _expandedOverviewCard;
        private readonly Text _expandedOverview;
        private readonly Image _factsCard;
        private readonly RectTransform _factsContainer;
        private readonly Image _mediaCard;
        private readonly RectTransform _mediaContainer;
        private readonly GameObject _progressGroup;
        private readonly Text _progressLabel;
        private readonly Image _progressFill;
        private readonly Button _continueButton;
        private readonly Text _continueLabel;
        private readonly Button _fromStartButton;
        private readonly Button _favoriteButton;
        private readonly Text _favoriteLabel;
        private readonly Button _playedButton;
        private readonly Text _playedLabel;
        private readonly EpisodeShelfView _episodeShelf;
        private readonly ChapterShelfView _chapterShelf;
        private readonly DetailShelfView _seasonsShelf;
        private readonly DetailShelfView _similarShelf;
        private JellyfinItem _item;
        private JellyfinItem _playTarget;
        private bool _userActionBusy;
        private bool _overviewExpanded;
        private string _fullOverview;
        private int _bindingVersion;

        public event Action<JellyfinItem, long> PlayRequested;
        public event Action<JellyfinItem, bool> FavoriteStateChangeRequested;
        public event Action<JellyfinItem, bool> PlayedStateChangeRequested;
        public event Action<JellyfinItem> RelatedItemSelected;
        public event Action CloseRequested;

        public Transform FocusRoot => _root.transform;

        public DetailView(Transform parent)
        {
            Image rootImage = UiFactory.CreatePanel("Detail Screen", parent, UiTheme.Background);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 20f, 0.992f);
            UiFactory.CreateAmbientBackdrop(rootImage.transform);

            _backdrop = UiFactory.CreatePanel(
                "Backdrop",
                rootImage.transform,
                new Color(0.07f, 0.075f, 0.10f, 1f));
            _backdrop.preserveAspect = false;
            _backdrop.raycastTarget = false;
            UiHeroBreath heroBreath = _backdrop.gameObject.AddComponent<UiHeroBreath>();
            heroBreath.ScaleAmplitude = 0.011f;
            heroBreath.CycleSeconds = 22f;
            UiFactory.SetRect(
                _backdrop.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 720f));

            Image backdropShade = UiFactory.CreateGradientPanel(
                "Backdrop Content Shade",
                _backdrop.transform,
                new Color(0.003f, 0.006f, 0.014f, 0.88f),
                new Color(0.005f, 0.008f, 0.016f, 0.22f),
                true);
            UiFactory.Stretch(backdropShade.rectTransform);

            Image backdropFade = UiFactory.CreateGradientPanel(
                "Backdrop Fade",
                rootImage.transform,
                UiTheme.Background,
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0f));
            UiFactory.SetRect(
                backdropFade.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -256f),
                new Vector2(0f, 520f));

            Image scrollSurface = UiFactory.CreatePanel("Detail Scroll", rootImage.transform, Color.clear);
            UiFactory.Stretch(scrollSurface.rectTransform);
            _scroll = scrollSurface.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = UiFactory.CreateRect("Detail Viewport", scrollSurface.transform);
            UiFactory.Stretch(viewport);
            Image viewportHitSurface = viewport.gameObject.AddComponent<Image>();
            viewportHitSurface.color = Color.clear;
            viewportHitSurface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UiFactory.CreateRect("Detail Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(88, 88, 64, 110);
            contentLayout.spacing = 20f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.085f;
            _scroll.decelerationRate = 0.11f;
            _scroll.scrollSensitivity = 54f;

            RectTransform hero = UiFactory.CreateRect("Hero Section", _content);
            LayoutElement heroElement = hero.gameObject.AddComponent<LayoutElement>();
            heroElement.minHeight = 560f;
            heroElement.preferredHeight = 596f;
            heroElement.flexibleWidth = 1f;
            heroElement.flexibleHeight = 0f;

            HorizontalLayoutGroup heroLayout = hero.gameObject.AddComponent<HorizontalLayoutGroup>();
            heroLayout.spacing = 54f;
            heroLayout.childAlignment = TextAnchor.UpperLeft;
            heroLayout.childControlWidth = true;
            heroLayout.childControlHeight = true;
            heroLayout.childForceExpandWidth = false;
            heroLayout.childForceExpandHeight = false;

            Image posterFrame = UiFactory.CreateRoundedPanel("Poster Frame", hero, UiTheme.SurfaceRaised);
            _posterFrame = posterFrame.gameObject;
            posterFrame.raycastTarget = false;
            LayoutElement posterElement = posterFrame.gameObject.AddComponent<LayoutElement>();
            posterElement.minWidth = 330f;
            posterElement.preferredWidth = 330f;
            posterElement.minHeight = 495f;
            posterElement.preferredHeight = 495f;
            posterElement.flexibleWidth = 0f;
            Shadow posterShadow = posterFrame.gameObject.AddComponent<Shadow>();
            posterShadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
            posterShadow.effectDistance = new Vector2(0f, -13f);
            posterShadow.useGraphicAlpha = true;
            Outline posterOutline = posterFrame.gameObject.AddComponent<Outline>();
            posterOutline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            posterOutline.effectDistance = new Vector2(2f, -2f);
            posterOutline.useGraphicAlpha = true;
            Mask posterMask = posterFrame.gameObject.AddComponent<Mask>();
            posterMask.showMaskGraphic = true;

            _poster = UiFactory.CreatePanel("Poster Artwork", posterFrame.transform, UiTheme.SurfaceRaised);
            _poster.preserveAspect = false;
            _poster.raycastTarget = false;
            UiFactory.Stretch(_poster.rectTransform);

            UiGradient posterFrameGradient = posterFrame.gameObject.AddComponent<UiGradient>();
            posterFrameGradient.StartColor = new Color(0.46f, 0.86f, 0.82f, 0.58f);
            posterFrameGradient.EndColor = new Color(0.49f, 0.35f, 0.70f, 0.44f);
            posterFrameGradient.Horizontal = true;

            RectTransform posterPlaceholderLayer = UiFactory.CreateRect(
                "Poster Placeholder Layer",
                posterFrame.transform);
            UiFactory.Stretch(posterPlaceholderLayer);
            Image posterPlaceholderGlow = UiFactory.CreateGlowPanel(
                "Poster Placeholder Glow",
                posterPlaceholderLayer,
                new Color(0.34f, 0.96f, 0.88f, 0.11f));
            UiFactory.SetRect(
                posterPlaceholderGlow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 8f),
                new Vector2(280f, 360f));
            Image posterPlaceholderShimmer = UiFactory.CreatePanel(
                "Poster Placeholder Shimmer",
                posterPlaceholderLayer,
                new Color(0.74f, 1f, 0.97f, 0.075f));
            posterPlaceholderShimmer.raycastTarget = false;
            UiFactory.SetRect(
                posterPlaceholderShimmer.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(82f, 680f));
            posterPlaceholderShimmer.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -11f);
            Image posterPlaceholderMark = UiFactory.CreateRoundedPanel(
                "Poster Placeholder Mark",
                posterPlaceholderLayer,
                new Color(0.48f, 0.96f, 0.89f, 0.74f));
            posterPlaceholderMark.raycastTarget = false;
            UiFactory.SetRect(
                posterPlaceholderMark.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 50f),
                new Vector2(58f, 5f));
            _posterPlaceholder = UiFactory.CreateText(
                "Poster Placeholder",
                posterPlaceholderLayer,
                "JELLYFIN",
                29,
                new Color(0.90f, 1f, 0.98f, 0.78f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                _posterPlaceholder.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f),
                new Vector2(284f, 46f));
            _posterPlaceholderCaption = UiFactory.CreateText(
                "Poster Placeholder Caption",
                posterPlaceholderLayer,
                "正在载入画面",
                16,
                new Color(0.72f, 0.79f, 0.84f, 0.70f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                _posterPlaceholderCaption.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -29f),
                new Vector2(284f, 30f));
            _posterPlaceholderMotion =
                posterFrame.gameObject.AddComponent<UiArtworkPlaceholderMotion>();
            _posterPlaceholderMotion.Configure(
                posterPlaceholderLayer.gameObject,
                posterPlaceholderShimmer.rectTransform,
                posterPlaceholderGlow,
                _posterPlaceholder,
                0.31f);

            RectTransform heroInfo = UiFactory.CreateRect("Hero Information", hero);
            _heroInformation = heroInfo.gameObject;
            LayoutElement heroInfoElement = heroInfo.gameObject.AddComponent<LayoutElement>();
            heroInfoElement.minWidth = 700f;
            heroInfoElement.preferredHeight = 540f;
            heroInfoElement.flexibleWidth = 1f;
            VerticalLayoutGroup heroInfoLayout = heroInfo.gameObject.AddComponent<VerticalLayoutGroup>();
            heroInfoLayout.spacing = 10f;
            heroInfoLayout.childAlignment = TextAnchor.UpperLeft;
            heroInfoLayout.childControlWidth = true;
            heroInfoLayout.childControlHeight = true;
            heroInfoLayout.childForceExpandWidth = true;
            heroInfoLayout.childForceExpandHeight = false;

            Image heroGlass = UiFactory.CreateRoundedPanel(
                "Hero Information Glass",
                heroInfo,
                new Color(0.012f, 0.018f, 0.030f, 0.56f));
            heroGlass.raycastTarget = false;
            UiFactory.SetRect(
                heroGlass.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(48f, 470f));
            LayoutElement heroGlassLayout = heroGlass.gameObject.AddComponent<LayoutElement>();
            heroGlassLayout.ignoreLayout = true;
            UiGradient heroGlassGradient = heroGlass.gameObject.AddComponent<UiGradient>();
            heroGlassGradient.StartColor = new Color(1f, 1f, 1f, 0.92f);
            heroGlassGradient.EndColor = new Color(1f, 1f, 1f, 0.54f);
            heroGlassGradient.Horizontal = true;
            Outline heroGlassOutline = heroGlass.gameObject.AddComponent<Outline>();
            heroGlassOutline.effectColor = new Color(0.78f, 0.91f, 1f, 0.075f);
            heroGlassOutline.effectDistance = new Vector2(1f, -1f);
            heroGlassOutline.useGraphicAlpha = true;
            heroGlass.transform.SetAsFirstSibling();

            Image heroGlow = UiFactory.CreateGlowPanel(
                "Hero Information Glow",
                heroInfo,
                new Color(0.30f, 0.94f, 0.86f, 0.075f));
            UiFactory.SetRect(
                heroGlow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(210f, -116f),
                new Vector2(720f, 360f));
            LayoutElement heroGlowLayout = heroGlow.gameObject.AddComponent<LayoutElement>();
            heroGlowLayout.ignoreLayout = true;
            UiAmbientFloat heroGlowMotion = heroGlow.gameObject.AddComponent<UiAmbientFloat>();
            heroGlowMotion.Amplitude = new Vector2(12f, 6f);
            heroGlowMotion.Speed = 0.035f;
            heroGlowMotion.ScalePulse = 0.018f;

            Image heroAccent = UiFactory.CreateGradientPanel(
                "Hero Information Accent",
                heroInfo,
                new Color(UiTheme.AccentBright.r, UiTheme.AccentBright.g, UiTheme.AccentBright.b, 0f),
                new Color(UiTheme.AccentBright.r, UiTheme.AccentBright.g, UiTheme.AccentBright.b, 0.82f));
            UiFactory.SetRect(
                heroAccent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-17f, -18f),
                new Vector2(4f, 108f));
            LayoutElement heroAccentLayout = heroAccent.gameObject.AddComponent<LayoutElement>();
            heroAccentLayout.ignoreLayout = true;

            _kindLabel = CreateFlowText(
                "Kind",
                heroInfo,
                17,
                UiTheme.AccentBright,
                FontStyle.Bold,
                28f);
            _title = CreateFlowText(
                "Title",
                heroInfo,
                54,
                UiTheme.TextPrimary,
                FontStyle.Bold,
                66f);
            _title.lineSpacing = 0.95f;
            _originalTitle = CreateFlowText(
                "Original Title",
                heroInfo,
                22,
                UiTheme.TextSecondary,
                FontStyle.Normal,
                30f);

            _metadataChips = UiFactory.CreateRect("Metadata Chips", heroInfo);
            LayoutElement chipRowElement = _metadataChips.gameObject.AddComponent<LayoutElement>();
            chipRowElement.minHeight = 44f;
            chipRowElement.preferredHeight = 44f;
            chipRowElement.flexibleHeight = 0f;
            HorizontalLayoutGroup chipLayout = _metadataChips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 9f;
            chipLayout.childAlignment = TextAnchor.MiddleLeft;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;
            chipLayout.childForceExpandHeight = true;

            _tagline = CreateFlowText(
                "Tagline",
                heroInfo,
                23,
                new Color(0.86f, 0.87f, 0.92f, 1f),
                FontStyle.Italic,
                34f);
            _tagline.lineSpacing = 1.08f;

            RectTransform progress = UiFactory.CreateRect("Watch Progress", heroInfo);
            LayoutElement progressElement = progress.gameObject.AddComponent<LayoutElement>();
            progressElement.minHeight = 42f;
            progressElement.preferredHeight = 42f;
            progressElement.flexibleHeight = 0f;
            _progressGroup = progress.gameObject;
            _progressLabel = UiFactory.CreateText(
                "Progress Label",
                progress,
                string.Empty,
                18,
                UiTheme.TextSecondary,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _progressLabel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 24f));
            Image progressTrack = UiFactory.CreateRoundedPanel(
                "Progress Track",
                progress,
                UiTheme.ProgressTrack);
            progressTrack.raycastTarget = false;
            UiFactory.SetRect(
                progressTrack.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 2f),
                new Vector2(0f, 7f));
            _progressFill = UiFactory.CreateRoundedPanel(
                "Progress Fill",
                progressTrack.transform,
                UiTheme.AccentBright);
            _progressFill.raycastTarget = false;
            _progressFill.rectTransform.anchorMin = Vector2.zero;
            _progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _progressFill.rectTransform.offsetMin = Vector2.zero;
            _progressFill.rectTransform.offsetMax = Vector2.zero;

            RectTransform actions = UiFactory.CreateRect("Detail Actions", heroInfo);
            LayoutElement actionsElement = actions.gameObject.AddComponent<LayoutElement>();
            actionsElement.minHeight = 66f;
            actionsElement.preferredHeight = 66f;
            actionsElement.flexibleHeight = 0f;
            HorizontalLayoutGroup actionsLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 10f;
            actionsLayout.childAlignment = TextAnchor.MiddleLeft;
            actionsLayout.childControlWidth = true;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = false;
            actionsLayout.childForceExpandHeight = true;

            _continueButton = UiFactory.CreateButton(
                "Continue",
                actions,
                "播放",
                UiTheme.Focus,
                new Color(0.025f, 0.028f, 0.045f, 1f),
                23);
            ConfigureActionButton(_continueButton, 420f);
            _continueLabel = _continueButton.GetComponentInChildren<Text>();
            _continueLabel.resizeTextForBestFit = true;
            _continueLabel.resizeTextMinSize = 18;
            _continueLabel.resizeTextMaxSize = 23;
            _continueButton.onClick.AddListener(() => RequestPlayback(_playTarget, true));

            _fromStartButton = UiFactory.CreateButton(
                "From Start",
                actions,
                "从头播放",
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                21);
            ConfigureActionButton(_fromStartButton, 174f);
            _fromStartButton.onClick.AddListener(() => RequestPlayback(_playTarget, false));

            _favoriteButton = UiFactory.CreateButton(
                "Favorite",
                actions,
                "收藏",
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                21);
            ConfigureActionButton(_favoriteButton, 142f);
            _favoriteLabel = _favoriteButton.GetComponentInChildren<Text>();
            _favoriteButton.onClick.AddListener(() =>
            {
                if (_item == null || _userActionBusy)
                {
                    return;
                }
                bool current = _item.UserData != null && _item.UserData.IsFavorite;
                FavoriteStateChangeRequested?.Invoke(_item, !current);
            });

            _playedButton = UiFactory.CreateButton(
                "Played",
                actions,
                "标记已看",
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                21);
            ConfigureActionButton(_playedButton, 162f);
            _playedLabel = _playedButton.GetComponentInChildren<Text>();
            _playedButton.onClick.AddListener(() =>
            {
                if (_item == null || _userActionBusy)
                {
                    return;
                }
                bool current = _item.UserData != null && _item.UserData.Played;
                PlayedStateChangeRequested?.Invoke(_item, !current);
            });

            _overview = CreateFlowText(
                "Overview",
                heroInfo,
                21,
                new Color(0.88f, 0.89f, 0.93f, 1f),
                FontStyle.Normal,
                86f);
            _overview.verticalOverflow = VerticalWrapMode.Truncate;
            _overview.lineSpacing = 1.16f;
            LayoutElement overviewElement = _overview.GetComponent<LayoutElement>();
            overviewElement.preferredHeight = 86f;

            RectTransform overviewToggleRow = UiFactory.CreateRect("Overview Toggle Row", heroInfo);
            LayoutElement overviewToggleRowElement = overviewToggleRow.gameObject.AddComponent<LayoutElement>();
            overviewToggleRowElement.minHeight = 42f;
            overviewToggleRowElement.preferredHeight = 42f;
            overviewToggleRowElement.flexibleHeight = 0f;
            _overviewToggleRow = overviewToggleRow.gameObject;
            _overviewToggle = UiFactory.CreateButton(
                "Overview Toggle",
                overviewToggleRow,
                "展开简介",
                UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                18);
            ConfigureActionButton(_overviewToggle, 150f);
            LayoutElement overviewToggleElement = _overviewToggle.GetComponent<LayoutElement>();
            overviewToggleElement.minHeight = 42f;
            overviewToggleElement.preferredHeight = 42f;
            UiFactory.SetRect(
                _overviewToggle.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(150f, 42f));
            _overviewToggleLabel = _overviewToggle.GetComponentInChildren<Text>();
            _overviewToggle.onClick.AddListener(ToggleOverview);

            _expandedOverviewCard = CreateCard("Expanded Overview Card", _content);
            CreateSectionHeading(_expandedOverviewCard.transform, "剧情简介", "OVERVIEW");
            _expandedOverview = CreateFlowText(
                "Expanded Overview",
                _expandedOverviewCard.transform,
                22,
                new Color(0.89f, 0.90f, 0.94f, 1f),
                FontStyle.Normal,
                72f);
            _expandedOverview.lineSpacing = 1.18f;
            _expandedOverviewCard.gameObject.SetActive(false);

            _episodeShelf = new EpisodeShelfView(_content, _scroll);
            _episodeShelf.EpisodeSelected += episode => RequestPlayback(episode, true);

            _chapterShelf = new ChapterShelfView(_content, _scroll);
            _chapterShelf.ChapterSelected += startPosition =>
            {
                if (_item != null && _item.IsPlayable)
                {
                    PlayRequested?.Invoke(_item, Math.Max(0L, startPosition));
                }
            };

            _seasonsShelf = new DetailShelfView(_content, _scroll, "Seasons Shelf");
            _seasonsShelf.ItemSelected += item => RelatedItemSelected?.Invoke(item);
            _similarShelf = new DetailShelfView(_content, _scroll, "Similar Shelf");
            _similarShelf.ItemSelected += item => RelatedItemSelected?.Invoke(item);

            _factsCard = CreateCard("Details Card", _content);
            CreateSectionHeading(_factsCard.transform, "详细信息", "ABOUT");
            _factsContainer = CreateFactContainer("Detail Facts", _factsCard.transform);

            _mediaCard = CreateCard("Media Card", _content);
            CreateSectionHeading(_mediaCard.transform, "媒体规格", "TECHNICAL");
            _mediaContainer = CreateFactContainer("Media Facts", _mediaCard.transform);

            Button close = UiFactory.CreateButton(
                "Close",
                rootImage.transform,
                "返回",
                new Color(0.055f, 0.061f, 0.082f, 0.94f),
                UiTheme.TextPrimary,
                22);
            UiFactory.SetRect(
                close.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-46f, -38f),
                new Vector2(126f, 58f));
            close.onClick.AddListener(() => CloseRequested?.Invoke());

            _motion.SetVisibleImmediately(false);
        }

        public bool IsVisible
        {
            get { return _motion.IsVisible; }
        }

        public JellyfinItem CurrentItem
        {
            get { return _item; }
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _motion.SetInteractionAllowed(enabled);
        }

        public void Show(
            JellyfinItem item,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            CancellationToken cancellationToken,
            IList<JellyfinItem> episodes = null,
            IList<JellyfinItem> seasons = null,
            IList<JellyfinItem> similarItems = null)
        {
            _bindingVersion++;
            _item = item;
            bool isSeries = item != null
                && (string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Type, "Season", StringComparison.OrdinalIgnoreCase));
            _playTarget = isSeries
                ? EpisodePlaybackResolver.Select(episodes)
                : item != null && item.IsPlayable ? item : null;
            _userActionBusy = false;
            _root.transform.SetAsLastSibling();
            _motion.Show();
            _motion.SetInteractionAllowed(true);
            UiFactory.AddItemReveal(_posterFrame, 0.025f);
            UiFactory.AddItemReveal(_heroInformation, 0.075f);

            _kindLabel.text = BuildKindLabel(item);
            _title.text = item != null
                ? JellyfinText.ToPlainText(item.Name)
                : string.Empty;
            string originalTitle = item != null
                ? JellyfinText.ToPlainText(item.OriginalTitle)
                : string.Empty;
            bool showOriginalTitle = !string.IsNullOrWhiteSpace(originalTitle)
                && !string.Equals(originalTitle, _title.text, StringComparison.OrdinalIgnoreCase);
            _originalTitle.text = showOriginalTitle ? originalTitle : string.Empty;
            _originalTitle.gameObject.SetActive(showOriginalTitle);

            PopulateMetadataChips(item);
            string tagline = item != null && item.Taglines != null
                ? item.Taglines
                    .Select(JellyfinText.ToPlainText)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                : null;
            _tagline.text = tagline ?? string.Empty;
            _tagline.gameObject.SetActive(!string.IsNullOrWhiteSpace(tagline));

            _fullOverview = item != null ? JellyfinText.ToPlainText(item.Overview) : string.Empty;
            _overviewExpanded = false;
            UpdateOverview();
            PopulateFacts(item);
            PopulateMediaFacts(item);
            _episodeShelf.Bind(
                episodes,
                isSeries ? _playTarget : item,
                api,
                imageCache,
                cancellationToken);
            _chapterShelf.Bind(item != null ? item.Chapters : null);
            _seasonsShelf.Bind(
                "季",
                seasons,
                false,
                api,
                imageCache,
                cancellationToken);
            _similarShelf.Bind(
                "更多类似",
                similarItems,
                false,
                api,
                imageCache,
                cancellationToken);
            if (_factsCard.gameObject.activeSelf)
            {
                UiFactory.AddScrollReveal(_factsCard.gameObject, _scroll, 0.04f);
            }
            if (_mediaCard.gameObject.activeSelf)
            {
                UiFactory.AddScrollReveal(_mediaCard.gameObject, _scroll, 0.06f);
            }
            UpdatePlaybackState();
            UpdateUserActionState();

            _poster.sprite = null;
            _poster.color = UiTheme.SurfaceRaised;
            _poster.CrossFadeAlpha(1f, 0f, true);
            _backdrop.sprite = null;
            _backdrop.color = new Color(0.07f, 0.075f, 0.10f, 1f);

            string posterUrl = item != null && api != null
                ? api.BuildPrimaryImageUrl(item, 560)
                : null;
            string backdropUrl = item != null && api != null
                ? api.BuildBackdropImageUrl(item, 1920)
                : null;
            bool loadingPoster = imageCache != null && !string.IsNullOrWhiteSpace(posterUrl);
            _posterPlaceholder.text = BuildPosterPlaceholderLabel(item);
            _posterPlaceholderCaption.text = loadingPoster
                ? "正在载入画面"
                : "暂无画面";
            if (loadingPoster)
            {
                _posterPlaceholderMotion.ShowLoading();
            }
            else
            {
                _posterPlaceholderMotion.ShowUnavailable();
            }

            int version = _bindingVersion;
            if (imageCache != null)
            {
                if (loadingPoster)
                {
                    LoadImageAsync(
                        posterUrl,
                        imageCache,
                        _poster,
                        true,
                        version,
                        cancellationToken).Forget();
                }
                if (!string.IsNullOrWhiteSpace(backdropUrl))
                {
                    LoadImageAsync(
                        backdropUrl,
                        imageCache,
                        _backdrop,
                        false,
                        version,
                        cancellationToken).Forget();
                }
            }

            RebuildLayout();
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void ApplyUserData(JellyfinUserData userData)
        {
            if (_item == null)
            {
                return;
            }

            _item.UserData = userData ?? new JellyfinUserData();
            UpdatePlaybackState();
            UpdateUserActionState();
            RebuildLayout();
        }

        public void SetUserActionBusy(bool busy)
        {
            _userActionBusy = busy;
            UpdateUserActionState();
        }

        public void Hide()
        {
            _bindingVersion++;
            _episodeShelf.Hide();
            _chapterShelf.Hide();
            _seasonsShelf.Hide();
            _similarShelf.Hide();
            _overviewExpanded = false;
            _expandedOverviewCard.gameObject.SetActive(false);
            _motion.Hide();
        }

        private void ToggleOverview()
        {
            if (string.IsNullOrWhiteSpace(_fullOverview) || _fullOverview.Length <= 150)
            {
                return;
            }

            _overviewExpanded = !_overviewExpanded;
            UpdateOverview();
            RebuildLayout();
        }

        private void UpdateOverview()
        {
            bool hasOverview = !string.IsNullOrWhiteSpace(_fullOverview);
            bool canExpand = hasOverview && _fullOverview.Length > 150;
            _overview.text = hasOverview ? Condense(_fullOverview, 150) : "暂无简介。";
            _overviewToggleRow.SetActive(canExpand);
            _overviewToggleLabel.text = _overviewExpanded ? "收起简介" : "展开简介";
            _expandedOverview.text = _fullOverview ?? string.Empty;
            bool showExpanded = canExpand && _overviewExpanded;
            _expandedOverviewCard.gameObject.SetActive(showExpanded);
            if (showExpanded)
            {
                UiFactory.AddItemReveal(_expandedOverviewCard.gameObject, 0f);
            }
        }

        private async Task LoadImageAsync(
            string url,
            JellyfinImageCache cache,
            Image target,
            bool posterArtwork,
            int bindingVersion,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                Sprite sprite = await cache.LoadSpriteAsync(url, cancellationToken);
                if (bindingVersion != _bindingVersion)
                {
                    return;
                }
                if (sprite == null)
                {
                    SetPosterUnavailable(posterArtwork);
                    return;
                }
                target.sprite = sprite;
                target.color = Color.white;
                UiFactory.RevealGraphic(target, target == _backdrop ? 0.40f : 0.30f);
                if (posterArtwork)
                {
                    _posterPlaceholderMotion.Complete(0.30f);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Metadata remains usable even when a backdrop or poster fails.
                if (bindingVersion == _bindingVersion)
                {
                    SetPosterUnavailable(posterArtwork);
                }
            }
        }

        private void SetPosterUnavailable(bool posterArtwork)
        {
            if (!posterArtwork)
            {
                return;
            }

            _posterPlaceholderCaption.text = "暂无画面";
            _posterPlaceholderMotion.ShowUnavailable();
        }

        private void PopulateMetadataChips(JellyfinItem item)
        {
            UiFactory.DestroyChildren(_metadataChips);
            List<string> values = new List<string>();
            if (item != null && string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
            {
                if (item.ParentIndexNumber.HasValue && item.IndexNumber.HasValue)
                {
                    values.Add(string.Format(
                        "S{0} E{1}",
                        item.ParentIndexNumber.Value,
                        item.IndexNumber.Value));
                }
            }
            if (item != null && item.ProductionYear.HasValue)
            {
                values.Add(item.ProductionYear.Value.ToString());
            }
            AddDistinct(values, BuildRuntime(item));
            if (item != null)
            {
                AddDistinct(values, item.OfficialRating);
                if (item.CommunityRating.HasValue)
                {
                    AddDistinct(values, "★ " + item.CommunityRating.Value.ToString("0.0"));
                }
                if (item.CriticRating.HasValue)
                {
                    AddDistinct(values, "影评 " + item.CriticRating.Value.ToString("0"));
                }
                AddDistinct(values, BuildVideoBadge(item));
                if (item.Genres != null)
                {
                    foreach (string genre in item.Genres.Take(2))
                    {
                        AddDistinct(values, genre);
                    }
                }
            }

            foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Take(8))
            {
                AddChip(value);
            }
            _metadataChips.gameObject.SetActive(_metadataChips.childCount > 0);
            for (int index = 0; index < _metadataChips.childCount; index++)
            {
                UiFactory.AddItemReveal(
                    _metadataChips.GetChild(index).gameObject,
                    Mathf.Min(0.14f, index * 0.018f));
            }
        }

        private void AddChip(string value)
        {
            string clean = JellyfinText.ToPlainText(value);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return;
            }

            Image chip = UiFactory.CreateRoundedPanel(
                "Metadata - " + clean,
                _metadataChips,
                new Color(1f, 1f, 1f, 0.105f));
            chip.raycastTarget = false;
            LayoutElement element = chip.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 68f;
            element.preferredWidth = Mathf.Clamp(34f + clean.Length * 13f, 76f, 226f);
            element.preferredHeight = 38f;
            element.flexibleHeight = 0f;
            Text label = UiFactory.CreateText(
                "Label",
                chip.transform,
                clean,
                18,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(label.rectTransform, 13f, 13f, 4f, 4f);
        }

        private void PopulateFacts(JellyfinItem item)
        {
            UiFactory.DestroyChildren(_factsContainer);
            if (item == null)
            {
                _factsCard.gameObject.SetActive(false);
                return;
            }

            AddFact("类型", JoinValues(item.Genres, 8));
            AddFact("导演", JoinPeople(item.People, "Director", 4));
            AddFact("编剧", JoinPeople(item.People, "Writer", 6));
            AddFact("主演", JoinPeople(item.People, "Actor", 7));
            AddFact(
                "工作室",
                item.Studios != null
                    ? JoinValues(item.Studios.Select(studio => studio != null ? studio.Name : null), 6)
                    : null);
            AddFact("制作地区", JoinValues(item.ProductionLocations, 6));
            AddFact("首映日期", FormatDate(item.PremiereDate));
            AddFact("状态", LocalizeStatus(item.Status));
            AddFact("标签", JoinValues(item.Tags, 10));
            AddFact(
                "章节",
                item.Chapters != null && item.Chapters.Count > 0
                    ? item.Chapters.Count + " 章"
                    : null);
            AddFact("外部 ID", BuildProviderIds(item));
            _factsCard.gameObject.SetActive(_factsContainer.childCount > 0);
        }

        private void PopulateMediaFacts(JellyfinItem item)
        {
            UiFactory.DestroyChildren(_mediaContainer);
            JellyfinMediaSource source = item != null && item.MediaSources != null
                ? item.MediaSources.FirstOrDefault(candidate => candidate != null)
                : null;
            if (source == null)
            {
                _mediaCard.gameObject.SetActive(false);
                return;
            }

            List<JellyfinMediaStream> streams = source.MediaStreams ?? new List<JellyfinMediaStream>();
            List<JellyfinMediaStream> videos = streams
                .Where(stream =>
                    stream != null && string.Equals(stream.Type, "Video", StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<JellyfinMediaStream> audioTracks = streams
                .Where(stream =>
                    stream != null && string.Equals(stream.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<JellyfinMediaStream> subtitles = streams
                .Where(stream =>
                    stream != null && string.Equals(stream.Type, "Subtitle", StringComparison.OrdinalIgnoreCase))
                .ToList();

            AddMediaFact("文件", source.Name);
            AddMediaFact("封装", BuildContainerDescription(source));
            for (int index = 0; index < videos.Count; index++)
            {
                AddMediaFact(
                    videos.Count > 1 ? "视频 " + (index + 1) : "视频",
                    BuildVideoDescription(videos[index]));
            }
            for (int index = 0; index < audioTracks.Count; index++)
            {
                AddMediaFact(
                    audioTracks.Count > 1 ? "音频 " + (index + 1) : "音频",
                    BuildAudioDescription(audioTracks[index]));
            }
            for (int index = 0; index < subtitles.Count; index++)
            {
                AddMediaFact(
                    subtitles.Count > 1 ? "字幕 " + (index + 1) : "字幕",
                    BuildSubtitleStreamDescription(subtitles[index]));
            }
            if (subtitles.Count == 0)
            {
                AddMediaFact("字幕", "无");
            }
            AddMediaFact("播放能力", BuildPlaybackCapabilities(source));
            _mediaCard.gameObject.SetActive(_mediaContainer.childCount > 0);
        }

        private void AddFact(string label, string value)
        {
            AddFactRow(_factsContainer, label, value);
        }

        private void AddMediaFact(string label, string value)
        {
            AddFactRow(_mediaContainer, label, value);
        }

        private static void AddFactRow(Transform parent, string label, string value)
        {
            string clean = JellyfinText.ToPlainText(value);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return;
            }

            RectTransform row = UiFactory.CreateRect(label + " Row", parent);
            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.minHeight = 38f;
            rowElement.flexibleHeight = 0f;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Text key = CreateFlowText(
                "Label",
                row,
                20,
                UiTheme.TextMuted,
                FontStyle.Bold,
                32f);
            LayoutElement keyElement = key.GetComponent<LayoutElement>();
            keyElement.minWidth = 136f;
            keyElement.preferredWidth = 136f;
            keyElement.flexibleWidth = 0f;
            key.text = label;

            Text content = CreateFlowText(
                "Value",
                row,
                22,
                UiTheme.TextPrimary,
                FontStyle.Normal,
                32f);
            LayoutElement contentElement = content.GetComponent<LayoutElement>();
            contentElement.flexibleWidth = 1f;
            content.text = clean;
            content.lineSpacing = 1.12f;
        }

        private void UpdatePlaybackState()
        {
            bool playable = _playTarget != null && _playTarget.IsPlayable;
            bool isSeries = _item != null
                && (string.Equals(_item.Type, "Series", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(_item.Type, "Season", StringComparison.OrdinalIgnoreCase));
            long resumePosition = EpisodePlaybackResolver.ResumePosition(_playTarget);
            bool hasResumePosition = resumePosition > AppConstants.TicksPerSecond * 10L;

            _continueButton.gameObject.SetActive(playable);
            SetActionButtonWidth(_continueButton, isSeries ? 420f : 300f);
            string episodeCode = EpisodePlaybackResolver.EpisodeCode(_playTarget);
            _continueLabel.text = isSeries && !string.IsNullOrWhiteSpace(episodeCode)
                ? BuildSeriesPlaybackLabel(hasResumePosition, _playTarget)
                : hasResumePosition ? "继续播放" : "播放";
            _fromStartButton.gameObject.SetActive(playable && hasResumePosition);

            JellyfinUserData userData = isSeries && _playTarget != null
                ? _playTarget.UserData
                : _item != null ? _item.UserData : null;
            double percentage = userData != null && userData.PlayedPercentage.HasValue
                ? userData.PlayedPercentage.Value
                : 0d;
            if (userData != null && userData.Played)
            {
                percentage = 100d;
            }
            bool showProgress = percentage > 0.1d || hasResumePosition || (userData != null && userData.Played);
            _progressGroup.SetActive(showProgress);
            if (showProgress)
            {
                float normalized = Mathf.Clamp01((float)(percentage / 100d));
                _progressFill.rectTransform.anchorMax = new Vector2(normalized, 1f);
                _progressFill.rectTransform.offsetMax = Vector2.zero;
                string progressText = userData != null && userData.Played
                    ? "已看完"
                    : "已观看 " + Math.Max(1d, percentage).ToString("0") + "%";
                _progressLabel.text = isSeries && !string.IsNullOrWhiteSpace(episodeCode)
                    ? episodeCode + " · " + progressText
                    : progressText;
            }
        }

        private void RequestPlayback(JellyfinItem item, bool resume)
        {
            if (item == null || !item.IsPlayable)
            {
                return;
            }
            long position = resume ? EpisodePlaybackResolver.ResumePosition(item) : 0L;
            PlayRequested?.Invoke(item, position);
        }

        private void UpdateUserActionState()
        {
            bool hasItem = _item != null && !string.IsNullOrWhiteSpace(_item.Id);
            bool isFavorite = hasItem && _item.UserData != null && _item.UserData.IsFavorite;
            bool isPlayed = hasItem && _item.UserData != null && _item.UserData.Played;

            _favoriteButton.interactable = hasItem && !_userActionBusy;
            _playedButton.interactable = hasItem && !_userActionBusy;
            _favoriteLabel.text = isFavorite ? "已收藏" : "收藏";
            _playedLabel.text = isPlayed ? "已看完" : "标记已看";
            _favoriteButton.targetGraphic.color = isFavorite ? UiTheme.Accent : UiTheme.SurfaceSoft;
            _playedButton.targetGraphic.color = isPlayed
                ? new Color(0.15f, 0.52f, 0.43f, 0.94f)
                : UiTheme.SurfaceSoft;
        }

        private void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_factsContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_mediaContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
        }

        private static Image CreateCard(string name, Transform parent)
        {
            Image card = UiFactory.CreateRoundedPanel(name, parent, UiTheme.SurfaceGlass);
            card.raycastTarget = true;
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 26);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement element = card.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 110f;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
            return card;
        }

        private static void CreateSectionHeading(Transform parent, string title, string eyebrow)
        {
            Text eyebrowLabel = CreateFlowText(
                "Section Eyebrow",
                parent,
                15,
                UiTheme.AccentBright,
                FontStyle.Bold,
                22f);
            eyebrowLabel.text = eyebrow;
            Text titleLabel = CreateFlowText(
                "Section Title",
                parent,
                30,
                UiTheme.TextPrimary,
                FontStyle.Bold,
                42f);
            titleLabel.text = title;
        }

        private static RectTransform CreateFactContainer(string name, Transform parent)
        {
            RectTransform container = UiFactory.CreateRect(name, parent);
            VerticalLayoutGroup layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = container.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement element = container.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 1f;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
            return container;
        }

        private static Text CreateFlowText(
            string name,
            Transform parent,
            int fontSize,
            Color color,
            FontStyle fontStyle,
            float minHeight)
        {
            Text text = UiFactory.CreateText(
                name,
                parent,
                string.Empty,
                fontSize,
                color,
                TextAnchor.UpperLeft,
                fontStyle);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = minHeight;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
            return text;
        }

        private static void ConfigureActionButton(Button button, float width)
        {
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            SetActionButtonWidth(button, width);
            element.minHeight = 60f;
            element.preferredHeight = 60f;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }

        private static void SetActionButtonWidth(Button button, float width)
        {
            if (button == null)
            {
                return;
            }

            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = button.gameObject.AddComponent<LayoutElement>();
            }
            element.minWidth = width;
            element.preferredWidth = width;
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

        private static string BuildSeriesPlaybackLabel(bool resume, JellyfinItem episode)
        {
            string action = resume ? "继续" : "播放";
            string code = EpisodePlaybackResolver.EpisodeCode(episode);
            string title = Condense(
                JellyfinText.ToPlainText(episode != null ? episode.Name : null),
                14);
            return string.IsNullOrWhiteSpace(title)
                ? action + " " + code
                : string.Format("{0} {1} · {2}", action, code, title);
        }

        private static string BuildPosterPlaceholderLabel(JellyfinItem item)
        {
            if (item == null)
            {
                return "JELLYFIN";
            }

            string episodeCode = EpisodePlaybackResolver.EpisodeCode(item);
            if (!string.IsNullOrWhiteSpace(episodeCode))
            {
                return episodeCode;
            }

            switch ((item.Type ?? string.Empty).ToLowerInvariant())
            {
                case "movie":
                    return "MOVIE";
                case "series":
                    return "SERIES";
                case "season":
                    return item.IndexNumber.HasValue
                        ? "SEASON " + item.IndexNumber.Value
                        : "SEASON";
                case "boxset":
                    return "COLLECTION";
                case "video":
                    return "VIDEO";
                default:
                    return "JELLYFIN";
            }
        }

        private static string BuildKindLabel(JellyfinItem item)
        {
            if (item == null)
            {
                return "JELLYFIN · DETAILS";
            }

            string kind;
            switch ((item.Type ?? string.Empty).ToLowerInvariant())
            {
                case "movie":
                    kind = "电影";
                    break;
                case "series":
                    kind = "剧集";
                    break;
                case "episode":
                    kind = "单集";
                    break;
                case "season":
                    kind = "季";
                    break;
                case "video":
                    kind = "视频";
                    break;
                case "boxset":
                    kind = "合集";
                    break;
                default:
                    kind = item.Type;
                    break;
            }
            return "JELLYFIN  ·  " + (string.IsNullOrWhiteSpace(kind) ? "详情" : kind);
        }

        private static string BuildRuntime(JellyfinItem item)
        {
            if (item == null || !item.RunTimeTicks.HasValue || item.RunTimeTicks.Value <= 0L)
            {
                return null;
            }

            TimeSpan span = TimeSpan.FromSeconds(
                item.RunTimeTicks.Value / (double)AppConstants.TicksPerSecond);
            return span.TotalHours >= 1d
                ? string.Format("{0}小时{1}分", (int)span.TotalHours, span.Minutes)
                : string.Format("{0}分钟", Math.Max(1, span.Minutes));
        }

        private static string BuildVideoBadge(JellyfinItem item)
        {
            JellyfinMediaStream video = item != null && item.MediaSources != null
                ? item.MediaSources
                    .Where(source => source != null && source.MediaStreams != null)
                    .SelectMany(source => source.MediaStreams)
                    .FirstOrDefault(stream =>
                        stream != null && string.Equals(stream.Type, "Video", StringComparison.OrdinalIgnoreCase))
                : null;
            if (video == null)
            {
                return null;
            }
            if (video.Height.HasValue)
            {
                if (video.Height.Value >= 2100)
                {
                    return "4K";
                }
                if (video.Height.Value >= 1000)
                {
                    return "1080p";
                }
                if (video.Height.Value >= 700)
                {
                    return "720p";
                }
            }
            return null;
        }

        private static string BuildVideoDescription(JellyfinMediaStream video)
        {
            if (video == null)
            {
                return null;
            }

            List<string> values = new List<string>();
            if (video.Width.HasValue && video.Height.HasValue)
            {
                values.Add(video.Width.Value + "×" + video.Height.Value);
            }
            AddDistinct(values, !string.IsNullOrWhiteSpace(video.Codec)
                ? video.Codec.ToUpperInvariant()
                : null);
            AddDistinct(values, !string.IsNullOrWhiteSpace(video.VideoRangeType)
                ? video.VideoRangeType
                : video.VideoRange);
            if (video.BitDepth.HasValue)
            {
                values.Add(video.BitDepth.Value + "-bit");
            }
            float? frameRate = video.AverageFrameRate ?? video.RealFrameRate;
            if (frameRate.HasValue && frameRate.Value > 0f)
            {
                values.Add(frameRate.Value.ToString("0.##") + " fps");
            }
            if (video.BitRate.HasValue && video.BitRate.Value > 0)
            {
                values.Add((video.BitRate.Value / 1000000f).ToString("0.##") + " Mbps");
            }
            return string.Join(" · ", values);
        }

        private static string BuildAudioDescription(JellyfinMediaStream audio)
        {
            if (audio == null)
            {
                return null;
            }

            List<string> values = new List<string>();
            AddDistinct(values, audio.Language);
            AddDistinct(values, !string.IsNullOrWhiteSpace(audio.Codec)
                ? audio.Codec.ToUpperInvariant()
                : null);
            if (audio.Channels.HasValue)
            {
                values.Add(audio.Channels.Value + " 声道");
            }
            AddDistinct(values, audio.ChannelLayout);
            AddDistinct(values, audio.DisplayTitle);
            if (audio.SampleRate.HasValue && audio.SampleRate.Value > 0)
            {
                values.Add((audio.SampleRate.Value / 1000f).ToString("0.#") + " kHz");
            }
            if (audio.BitDepth.HasValue && audio.BitDepth.Value > 0)
            {
                values.Add(audio.BitDepth.Value + "-bit");
            }
            if (audio.BitRate.HasValue && audio.BitRate.Value > 0)
            {
                values.Add((audio.BitRate.Value / 1000f).ToString("0") + " kbps");
            }
            if (audio.IsDefault && !ContainsStateLabel(audio.DisplayTitle, "默认", "default"))
            {
                AddDistinct(values, "默认");
            }
            return string.Join(" · ", values);
        }

        private static string BuildContainerDescription(JellyfinMediaSource source)
        {
            if (source == null)
            {
                return null;
            }

            List<string> values = new List<string>();
            AddDistinct(values, !string.IsNullOrWhiteSpace(source.Container)
                ? source.Container.ToUpperInvariant()
                : null);
            if (source.Size.HasValue && source.Size.Value > 0L)
            {
                double gibibytes = source.Size.Value / 1073741824d;
                values.Add(gibibytes >= 0.1d
                    ? gibibytes.ToString("0.##") + " GiB"
                    : (source.Size.Value / 1048576d).ToString("0.#") + " MiB");
            }
            if (source.Bitrate.HasValue && source.Bitrate.Value > 0)
            {
                values.Add((source.Bitrate.Value / 1000000f).ToString("0.##") + " Mbps");
            }
            return string.Join(" · ", values);
        }

        private static string BuildSubtitleStreamDescription(JellyfinMediaStream subtitle)
        {
            if (subtitle == null)
            {
                return null;
            }

            List<string> values = new List<string>();
            AddDistinct(values, subtitle.Language);
            AddDistinct(values, subtitle.DisplayTitle);
            AddDistinct(values, !string.IsNullOrWhiteSpace(subtitle.Codec)
                ? subtitle.Codec.ToUpperInvariant()
                : null);
            values.Add(subtitle.IsExternal ? "外挂" : "内嵌");
            if (subtitle.IsDefault && !ContainsStateLabel(subtitle.DisplayTitle, "默认", "default"))
            {
                AddDistinct(values, "默认");
            }
            if (subtitle.IsForced && !ContainsStateLabel(subtitle.DisplayTitle, "强制", "forced"))
            {
                AddDistinct(values, "强制");
            }
            if (subtitle.IsHearingImpaired)
            {
                AddDistinct(values, "听障字幕");
            }
            return string.Join(" · ", values);
        }

        private static bool ContainsStateLabel(
            string value,
            string localizedLabel,
            string englishLabel)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.IndexOf(localizedLabel, StringComparison.OrdinalIgnoreCase) >= 0
                    || value.IndexOf(englishLabel, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildPlaybackCapabilities(JellyfinMediaSource source)
        {
            List<string> values = new List<string>();
            if (source.SupportsDirectPlay)
            {
                values.Add("直接播放");
            }
            if (source.SupportsDirectStream)
            {
                values.Add("直接串流");
            }
            if (source.SupportsTranscoding)
            {
                values.Add("转码");
            }
            return string.Join(" · ", values);
        }

        private static string JoinPeople(IEnumerable<JellyfinPerson> people, string type, int limit)
        {
            return people == null
                ? null
                : JoinValues(
                    people
                        .Where(person =>
                            person != null
                            && string.Equals(person.Type, type, StringComparison.OrdinalIgnoreCase))
                        .Select(person => person.Name),
                    limit);
        }

        private static string JoinValues(IEnumerable<string> values, int limit)
        {
            if (values == null)
            {
                return null;
            }

            List<string> clean = values
                .Select(JellyfinText.ToPlainText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, limit))
                .ToList();
            return clean.Count > 0 ? string.Join(" · ", clean) : null;
        }

        private static string BuildProviderIds(JellyfinItem item)
        {
            if (item == null || item.ProviderIds == null || item.ProviderIds.Count == 0)
            {
                return item != null && item.ExternalUrls != null
                    ? JoinValues(item.ExternalUrls.Select(value => value != null ? value.Name : null), 6)
                    : null;
            }

            return string.Join(
                " · ",
                item.ProviderIds
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .OrderBy(pair => pair.Key)
                    .Take(6)
                    .Select(pair => pair.Key + ": " + pair.Value));
        }

        private static string FormatDate(string value)
        {
            DateTimeOffset date;
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out date)
                ? date.ToString("yyyy-MM-dd")
                : value;
        }

        private static string LocalizeStatus(string value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "continuing":
                    return "连载中";
                case "ended":
                    return "已完结";
                case "released":
                    return "已发行";
                default:
                    return value;
            }
        }

        private static void AddDistinct(ICollection<string> values, string value)
        {
            string clean = JellyfinText.ToPlainText(value);
            if (string.IsNullOrWhiteSpace(clean)
                || values.Any(existing => string.Equals(existing, clean, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            values.Add(clean);
        }
    }
}
