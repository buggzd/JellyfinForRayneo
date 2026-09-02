using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class PosterCardView : MonoBehaviour
    {
        public const float PosterWidth = 208f;
        public const float PosterArtworkHeight = 312f;
        public const float PosterHeight = 380f;
        public const float LandscapeWidth = 342f;
        public const float LandscapeArtworkHeight = 192.375f;
        public const float LandscapeHeight = 260f;

        private Image _artwork;
        private AspectRatioFitter _artworkAspect;
        private Text _placeholder;
        private Text _title;
        private Text _subtitle;
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
                new Color(0f, 0f, 0f, landscape ? 0.42f : 0.50f));
            shadow.raycastTarget = false;
            UiFactory.SetRect(
                shadow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -10f),
                new Vector2(10f, artworkHeight + 6f));

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
                new Vector2(10f, artworkHeight + 10f));

            Image artworkFrame = UiFactory.CreateRoundedPanel(
                "Artwork Frame",
                rootRect,
                new Color(0.09f, 0.095f, 0.12f, 1f));
            UiFactory.SetRect(
                artworkFrame.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, artworkHeight));
            Mask artworkMask = artworkFrame.gameObject.AddComponent<Mask>();
            artworkMask.showMaskGraphic = true;

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

            Text placeholder = UiFactory.CreateText(
                "Artwork Placeholder",
                artworkFrame.transform,
                string.Empty,
                landscape ? 68 : 76,
                new Color(1f, 1f, 1f, 0.15f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(placeholder.rectTransform);

            Image artworkShade = UiFactory.CreateGradientPanel(
                "Artwork Shade",
                artworkFrame.transform,
                new Color(0.012f, 0.014f, 0.022f, landscape ? 0.54f : 0.20f),
                new Color(0.012f, 0.014f, 0.022f, 0f));
            UiFactory.Stretch(artworkShade.rectTransform);

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
                landscape ? 23 : 22,
                UiTheme.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = landscape ? 18 : 17;
            title.resizeTextMaxSize = landscape ? 23 : 22;
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
                17,
                UiTheme.TextSecondary,
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
            focus.FocusedScale = landscape ? 1.055f : 1.07f;
            focus.AnimationSpeed = 15f;
            focus.LocalDepthOffset = -22f;
            focus.ConfigureFocusGraphic(focusRing, UiTheme.Focus);

            PosterCardView view = rootRect.gameObject.AddComponent<PosterCardView>();
            view._artwork = artwork;
            view._artworkAspect = artworkAspect;
            view._placeholder = placeholder;
            view._title = title;
            view._subtitle = subtitle;
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
            bool preferPrimaryArtwork = false)
        {
            _bindingVersion++;
            _item = item;
            _onSelected = onSelected;
            _title.text = item != null ? item.Name : string.Empty;
            _subtitle.text = item != null ? item.Subtitle : string.Empty;
            _artwork.sprite = null;
            _artwork.color = Color.clear;
            _placeholder.text = Initial(item != null ? item.Name : null);
            _placeholder.gameObject.SetActive(true);

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
                if (!string.IsNullOrWhiteSpace(fallbackUrl)
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
                        return;
                    }
                }
            }

            if (this == null || bindingVersion != _bindingVersion || sprite == null)
            {
                return;
            }

            _artwork.sprite = sprite;
            _artwork.color = Color.white;
            if (sprite.rect.height > 0f)
            {
                _artworkAspect.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            _placeholder.gameObject.SetActive(false);
        }

        private static string Initial(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "J";
            }

            return value.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }
}
