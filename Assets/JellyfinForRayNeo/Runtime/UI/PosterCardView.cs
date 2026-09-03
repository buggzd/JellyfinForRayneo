using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class PosterCardView : MonoBehaviour
    {
        public const float PosterWidth = 242f;
        public const float PosterArtworkHeight = 363f;
        public const float PosterHeight = 435f;
        public const float LandscapeWidth = 390f;
        public const float LandscapeArtworkHeight = 219.375f;
        public const float LandscapeHeight = 296f;

        private Image _artwork;
        private AspectRatioFitter _artworkAspect;
        private Text _placeholder;
        private Text _placeholderCaption;
        private UiArtworkPlaceholderMotion _placeholderMotion;
        private Text _title;
        private Text _subtitle;
        private Text _centerLabel;
        private Text _artIndex;
        private GameObject _typeBadge;
        private Text _typeBadgeLabel;
        private GameObject _statusBadge;
        private Text _statusBadgeLabel;
        private Image _progressFill;
        private Button _button;
        private FocusScale _focus;
        private bool _landscape;
        private JellyfinItem _item;
        private Action<JellyfinItem> _onSelected;
        private int _bindingVersion;

        public static PosterCardView Create(Transform parent, bool landscape = false)
        {
            float width = landscape ? LandscapeWidth : PosterWidth;
            float artworkHeight = landscape ? LandscapeArtworkHeight : PosterArtworkHeight;
            float totalHeight = landscape ? LandscapeHeight : PosterHeight;

            RectTransform rootRect = UiFactory.CreateRect("Poster Card", parent);
            rootRect.sizeDelta = new Vector2(width, totalHeight);
            LayoutElement layout = rootRect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = totalHeight;
            layout.preferredHeight = totalHeight;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image shadow = UiFactory.CreateRoundedPanel(
                "Artwork Shadow",
                rootRect,
                new Color(0f, 0.01f, 0.02f, landscape ? 0.50f : 0.58f));
            shadow.raycastTarget = false;
            UiFactory.SetRect(
                shadow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -12f),
                new Vector2(18f, artworkHeight + 12f));

            Image focusRing = UiFactory.CreateRoundedPanel(
                "Focus Ring",
                rootRect,
                new Color(1f, 1f, 1f, 0f));
            focusRing.raycastTarget = false;
            UiFactory.SetRect(
                focusRing.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(12f, artworkHeight + 12f));

            Image artworkFrame = UiFactory.CreateRoundedPanel(
                "Artwork Frame",
                rootRect,
                new Color(0.018f, 0.055f, 0.078f, 1f));
            UiFactory.SetRect(
                artworkFrame.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, artworkHeight));
            Mask artworkMask = artworkFrame.gameObject.AddComponent<Mask>();
            artworkMask.showMaskGraphic = true;
            Outline artworkOutline = artworkFrame.gameObject.AddComponent<Outline>();
            artworkOutline.effectColor = new Color(0.79f, 0.94f, 0.98f, 0.13f);
            artworkOutline.effectDistance = new Vector2(1f, -1f);
            artworkOutline.useGraphicAlpha = true;

            Image artwork = UiFactory.CreatePanel("Artwork", artworkFrame.transform, Color.white);
            artwork.raycastTarget = false;
            artwork.preserveAspect = false;
            UiFactory.SetRect(
                artwork.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(width, artworkHeight));
            AspectRatioFitter artworkAspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            artworkAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            artworkAspect.aspectRatio = landscape ? 16f / 9f : 2f / 3f;

            UiGradient frameGradient = artworkFrame.gameObject.AddComponent<UiGradient>();
            frameGradient.StartColor = new Color(0.025f, 0.098f, 0.125f, 1f);
            frameGradient.EndColor = new Color(0.055f, 0.049f, 0.128f, 1f);
            frameGradient.Horizontal = true;

            RectTransform placeholderLayer = UiFactory.CreateRect(
                "Artwork Placeholder Layer",
                artworkFrame.transform);
            UiFactory.Stretch(placeholderLayer);

            Image placeholderGlow = UiFactory.CreateGlowPanel(
                "Artwork Placeholder Glow",
                placeholderLayer,
                new Color(0.34f, 0.88f, 1f, landscape ? 0.15f : 0.12f));
            UiFactory.SetRect(
                placeholderGlow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f),
                new Vector2(
                    landscape ? 250f : 180f,
                    landscape ? 180f : 240f));

            Image placeholderShimmer = UiFactory.CreatePanel(
                "Artwork Placeholder Shimmer",
                placeholderLayer,
                new Color(0.78f, 0.97f, 1f, 0.075f));
            placeholderShimmer.raycastTarget = false;
            UiFactory.SetRect(
                placeholderShimmer.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(
                    landscape ? 76f : 58f,
                    artworkHeight * 1.35f));
            placeholderShimmer.rectTransform.localEulerAngles = new Vector3(0f, 0f, -11f);

            Image placeholderMark = UiFactory.CreateRoundedPanel(
                "Artwork Placeholder Mark",
                placeholderLayer,
                new Color(0.62f, 0.93f, 1f, 0.72f));
            placeholderMark.raycastTarget = false;
            UiFactory.SetRect(
                placeholderMark.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, landscape ? 39f : 44f),
                new Vector2(48f, 4f));

            Text placeholder = UiFactory.CreateText(
                "Artwork Placeholder",
                placeholderLayer,
                string.Empty,
                landscape ? 27 : 24,
                new Color(0.90f, 0.98f, 1f, 0.76f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                placeholder.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 7f),
                new Vector2(width - 36f, 40f));

            Text placeholderCaption = UiFactory.CreateText(
                "Artwork Placeholder Caption",
                placeholderLayer,
                "正在载入",
                14,
                UiTheme.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                placeholderCaption.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -25f),
                new Vector2(width - 36f, 26f));

            UiArtworkPlaceholderMotion placeholderMotion =
                artworkFrame.gameObject.AddComponent<UiArtworkPlaceholderMotion>();
            placeholderMotion.Configure(
                placeholderLayer.gameObject,
                placeholderShimmer.rectTransform,
                placeholderGlow,
                placeholder,
                Mathf.Abs(rootRect.GetInstanceID() % 997) / 997f);

            Image artworkShade = UiFactory.CreateGradientPanel(
                "Artwork Shade",
                artworkFrame.transform,
                new Color(0.002f, 0.014f, 0.024f, landscape ? 0.74f : 0.36f),
                new Color(0.002f, 0.014f, 0.024f, 0.015f));
            UiFactory.Stretch(artworkShade.rectTransform);

            Text artIndex = UiFactory.CreateText(
                "Artwork Index",
                artworkFrame.transform,
                "L/00",
                11,
                UiTheme.TextMuted,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                artIndex.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -12f),
                new Vector2(74f, 24f));

            Text centerLabel = UiFactory.CreateText(
                "Library Title Overlay",
                artworkFrame.transform,
                string.Empty,
                landscape ? 30 : 24,
                Color.white,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            centerLabel.resizeTextForBestFit = true;
            centerLabel.resizeTextMinSize = 22;
            centerLabel.resizeTextMaxSize = landscape ? 36 : 28;
            Outline centerOutline = centerLabel.gameObject.AddComponent<Outline>();
            centerOutline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            centerOutline.effectDistance = new Vector2(2f, -2f);
            UiFactory.Stretch(centerLabel.rectTransform, 26f, 26f, 28f, 28f);
            centerLabel.gameObject.SetActive(false);

            Image typeBadge = UiFactory.CreateRoundedPanel(
                "Type Badge",
                artworkFrame.transform,
                new Color(0.005f, 0.027f, 0.043f, 0.68f));
            typeBadge.raycastTarget = false;
            UiFactory.SetRect(
                typeBadge.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(12f, -12f),
                new Vector2(92f, 34f));
            Text typeBadgeLabel = UiFactory.CreateText(
                "Type Badge Label",
                typeBadge.transform,
                string.Empty,
                15,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(typeBadgeLabel.rectTransform, 8f, 8f, 2f, 2f);
            typeBadge.gameObject.SetActive(false);

            Image statusBadge = UiFactory.CreateRoundedPanel(
                "Status Badge",
                artworkFrame.transform,
                new Color(0.005f, 0.027f, 0.043f, 0.72f));
            statusBadge.raycastTarget = false;
            UiFactory.SetRect(
                statusBadge.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -12f),
                new Vector2(104f, 34f));
            Text statusBadgeLabel = UiFactory.CreateText(
                "Status Badge Label",
                statusBadge.transform,
                string.Empty,
                15,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(statusBadgeLabel.rectTransform, 8f, 8f, 2f, 2f);
            statusBadge.gameObject.SetActive(false);

            Image progressTrack = UiFactory.CreateRoundedPanel(
                "Progress Track",
                artworkFrame.transform,
                UiTheme.ProgressTrack);
            progressTrack.raycastTarget = false;
            UiFactory.SetRect(
                progressTrack.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 12f),
                new Vector2(-28f, 6f));

            Image progressFill = UiFactory.CreateRoundedPanel(
                "Progress Fill",
                progressTrack.transform,
                UiTheme.AccentBright);
            progressFill.raycastTarget = false;
            progressFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            progressFill.rectTransform.anchoredPosition = Vector2.zero;
            progressFill.rectTransform.sizeDelta = Vector2.zero;

            Text title = UiFactory.CreateText(
                "Title",
                rootRect,
                string.Empty,
                landscape ? 22 : 21,
                UiTheme.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = landscape ? 17 : 16;
            title.resizeTextMaxSize = landscape ? 22 : 21;
            UiFactory.SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -artworkHeight - 12f),
                new Vector2(0f, 30f));

            Text subtitle = UiFactory.CreateText(
                "Subtitle",
                rootRect,
                string.Empty,
                15,
                UiTheme.TextMuted,
                TextAnchor.UpperLeft);
            UiFactory.SetRect(
                subtitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -artworkHeight - 42f),
                new Vector2(0f, 24f));

            Button button = rootRect.gameObject.AddComponent<Button>();
            button.targetGraphic = artworkFrame;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            FocusScale focus = rootRect.gameObject.AddComponent<FocusScale>();
            focus.FocusedScale = landscape ? 1.045f : 1.055f;
            focus.AnimationSpeed = 12f;
            focus.LocalDepthOffset = -20f;
            focus.ConfigureFocusGraphic(focusRing, UiTheme.Focus);
            focus.ConfigureShadowGraphic(
                shadow,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.42f));

            PosterCardView view = rootRect.gameObject.AddComponent<PosterCardView>();
            view._artwork = artwork;
            view._artworkAspect = artworkAspect;
            view._placeholder = placeholder;
            view._placeholderCaption = placeholderCaption;
            view._placeholderMotion = placeholderMotion;
            view._title = title;
            view._subtitle = subtitle;
            view._centerLabel = centerLabel;
            view._artIndex = artIndex;
            view._typeBadge = typeBadge.gameObject;
            view._typeBadgeLabel = typeBadgeLabel;
            view._statusBadge = statusBadge.gameObject;
            view._statusBadgeLabel = statusBadgeLabel;
            view._progressFill = progressFill;
            view._button = button;
            view._focus = focus;
            view._landscape = landscape;
            return view;
        }

        public void ConfigureScrollRects(ScrollRect horizontalScroll, ScrollRect verticalScroll)
        {
            if (_focus != null)
            {
                _focus.ConfigureScrollRects(horizontalScroll, verticalScroll);
            }
        }

        public void Bind(
            JellyfinItem item,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            Action<JellyfinItem> onSelected,
            CancellationToken cancellationToken,
            int posterMaxWidth = 480,
            bool preferPrimaryArtwork = false,
            bool libraryCard = false)
        {
            _bindingVersion++;
            _item = item;
            _onSelected = onSelected;
            _title.text = BuildTitle(item);
            _subtitle.text = BuildSubtitle(item);
            _centerLabel.text = item != null ? item.Name : string.Empty;
            _artIndex.text = ArtworkIndex(item);
            _centerLabel.gameObject.SetActive(libraryCard && item != null);
            BindBadges(item, libraryCard);
            _artwork.sprite = null;
            _artwork.color = Color.clear;
            _artwork.CrossFadeAlpha(1f, 0f, true);
            _placeholder.text = PlaceholderLabel(item);

            float progress = 0f;
            if (item != null && item.UserData != null && item.UserData.PlayedPercentage.HasValue)
            {
                progress = Mathf.Clamp01((float)item.UserData.PlayedPercentage.Value / 100f);
            }
            _progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
            _progressFill.transform.parent.gameObject.SetActive(progress > 0.001f && progress < 0.999f);

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onSelected?.Invoke(_item));

            string primaryUrl = item != null ? api.BuildPrimaryImageUrl(item, posterMaxWidth) : null;
            string backdropUrl = _landscape && item != null
                ? api.BuildBackdropImageUrl(item, Math.Max(720, posterMaxWidth))
                : null;
            string imageUrl = _landscape && !preferPrimaryArtwork ? backdropUrl : primaryUrl;
            string fallbackUrl = _landscape && preferPrimaryArtwork ? backdropUrl : primaryUrl;
            if (string.Equals(imageUrl, fallbackUrl, StringComparison.Ordinal))
            {
                fallbackUrl = null;
            }

            bool loadingArtwork = !string.IsNullOrWhiteSpace(imageUrl);
            _placeholderCaption.text = loadingArtwork ? "正在载入画面" : "暂无画面";
            if (loadingArtwork)
            {
                _placeholderMotion.ShowLoading();
            }
            else
            {
                _placeholderMotion.ShowUnavailable();
            }

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                LoadArtworkAsync(
                    imageUrl,
                    fallbackUrl,
                    imageCache,
                    _bindingVersion,
                    cancellationToken).Forget();
            }
        }

        private void BindBadges(JellyfinItem item, bool libraryCard)
        {
            string type = libraryCard ? "媒体库" : TypeBadge(item);
            _typeBadgeLabel.text = type ?? string.Empty;
            _typeBadge.SetActive(!string.IsNullOrWhiteSpace(type));

            string status = StatusBadge(item);
            _statusBadgeLabel.text = status ?? string.Empty;
            _statusBadge.SetActive(!string.IsNullOrWhiteSpace(status));
        }

        private static string BuildTitle(JellyfinItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase)
                && item.ParentIndexNumber.HasValue
                && item.IndexNumber.HasValue)
            {
                return string.Format(
                    "S{0}E{1} · {2}",
                    item.ParentIndexNumber.Value,
                    item.IndexNumber.Value,
                    item.Name);
            }

            return item.Name ?? string.Empty;
        }

        private static string BuildSubtitle(JellyfinItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.IsBrowsableContainer)
            {
                string count = item.VisibleChildCount.HasValue
                    ? " · " + item.VisibleChildCount.Value + " 项"
                    : string.Empty;
                return FriendlyType(item.Type) + count;
            }

            if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.SeriesName))
            {
                return item.SeriesName;
            }

            if (string.Equals(item.Type, "Video", StringComparison.OrdinalIgnoreCase))
            {
                string year = item.ProductionYear.HasValue
                    ? item.ProductionYear.Value.ToString()
                    : "视频";
                string runtime = RuntimeLabel(item.RunTimeTicks);
                return string.IsNullOrWhiteSpace(runtime) ? year : year + " · " + runtime;
            }

            return item.Subtitle;
        }

        private static string TypeBadge(JellyfinItem item)
        {
            if (item == null)
            {
                return null;
            }

            switch ((item.Type ?? string.Empty).ToLowerInvariant())
            {
                case "folder":
                case "collectionfolder":
                    return "文件夹";
                case "boxset":
                    return "合集";
                case "video":
                    return "视频";
                case "episode":
                    return "单集";
                default:
                    return null;
            }
        }

        private static string StatusBadge(JellyfinItem item)
        {
            JellyfinUserData userData = item != null ? item.UserData : null;
            if (userData == null)
            {
                return null;
            }

            if (userData.UnplayedItemCount.HasValue && userData.UnplayedItemCount.Value > 0)
            {
                return userData.UnplayedItemCount.Value + " 未看";
            }
            if (item != null && item.IsBrowsableContainer)
            {
                return userData.IsFavorite ? "★ 收藏" : null;
            }
            if (userData.Played)
            {
                return "✓ 已看";
            }
            if (userData.IsFavorite)
            {
                return "★ 收藏";
            }
            return null;
        }

        private static string RuntimeLabel(long? runTimeTicks)
        {
            if (!runTimeTicks.HasValue || runTimeTicks.Value <= 0L)
            {
                return null;
            }

            TimeSpan duration = TimeSpan.FromSeconds(
                runTimeTicks.Value / (double)AppConstants.TicksPerSecond);
            return duration.TotalHours >= 1d
                ? string.Format("{0}小时{1}分", (int)duration.TotalHours, duration.Minutes)
                : Math.Max(1, duration.Minutes) + "分钟";
        }

        private static string FriendlyType(string type)
        {
            switch ((type ?? string.Empty).ToLowerInvariant())
            {
                case "collectionfolder":
                    return "媒体库";
                case "folder":
                    return "文件夹";
                case "boxset":
                    return "合集";
                case "playlist":
                    return "播放列表";
                default:
                    return string.IsNullOrWhiteSpace(type) ? "内容" : type;
            }
        }

        private async Task LoadArtworkAsync(
            string imageUrl,
            string fallbackUrl,
            JellyfinImageCache imageCache,
            int bindingVersion,
            CancellationToken cancellationToken)
        {
            Sprite sprite = null;
            try
            {
                sprite = await imageCache.LoadSpriteAsync(imageUrl, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
            }

            if (sprite == null
                && !string.IsNullOrWhiteSpace(fallbackUrl)
                && !string.Equals(imageUrl, fallbackUrl, StringComparison.Ordinal))
            {
                try
                {
                    sprite = await imageCache.LoadSpriteAsync(fallbackUrl, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (this == null || bindingVersion != _bindingVersion)
            {
                return;
            }

            if (sprite == null)
            {
                _placeholderCaption.text = "暂无画面";
                _placeholderMotion.ShowUnavailable();
                return;
            }

            _artwork.sprite = sprite;
            _artwork.color = Color.white;
            UiFactory.RevealGraphic(_artwork, 0.30f);
            if (sprite.rect.height > 0f)
            {
                _artworkAspect.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            _placeholderMotion.Complete(0.30f);
        }

        private static string ArtworkIndex(JellyfinItem item)
        {
            string source = item != null
                ? item.Id ?? item.Name ?? item.Type
                : null;
            if (string.IsNullOrWhiteSpace(source))
            {
                return "L/00";
            }

            int checksum = 17;
            foreach (char value in source)
            {
                checksum = unchecked(checksum * 31 + value);
            }
            int index = (int)(Math.Abs((long)checksum) % 99L) + 1;
            return "L/" + index.ToString("00");
        }

        private static string PlaceholderLabel(JellyfinItem item)
        {
            if (item == null)
            {
                return "JELLYFIN";
            }

            if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
            {
                string code = EpisodePlaybackResolver.EpisodeCode(item);
                return string.IsNullOrWhiteSpace(code) ? "EPISODE" : code;
            }

            switch ((item.Type ?? string.Empty).ToLowerInvariant())
            {
                case "movie":
                    return "MOVIE";
                case "series":
                    return "SERIES";
                case "season":
                    return "SEASON";
                case "video":
                    return "VIDEO";
                case "folder":
                case "collectionfolder":
                    return "LIBRARY";
                case "boxset":
                    return "COLLECTION";
                default:
                    return "JELLYFIN";
            }
        }
    }
}
