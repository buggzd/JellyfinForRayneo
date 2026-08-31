using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class PosterCardView : MonoBehaviour
    {
        private Image _poster;
        private Text _placeholder;
        private Text _title;
        private Text _subtitle;
        private Image _progressFill;
        private Button _button;
        private JellyfinItem _item;
        private Action<JellyfinItem> _onSelected;
        private int _bindingVersion;

        public static PosterCardView Create(Transform parent)
        {
            Image background = UiFactory.CreatePanel("Poster Card", parent, UiTheme.SurfaceRaised);
            RectTransform rootRect = background.rectTransform;
            rootRect.sizeDelta = new Vector2(190f, 300f);

            LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 190f;
            layout.preferredHeight = 300f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            FocusScale focus = background.gameObject.AddComponent<FocusScale>();
            focus.FocusedScale = 1.085f;
            focus.LocalDepthOffset = -25f;

            PosterCardView view = background.gameObject.AddComponent<PosterCardView>();
            view._button = button;

            Image poster = UiFactory.CreatePanel("Poster", background.transform, new Color(0.12f, 0.13f, 0.18f, 1f));
            poster.raycastTarget = false;
            poster.preserveAspect = true;
            UiFactory.SetRect(poster.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 240f));
            view._poster = poster;

            Text placeholder = UiFactory.CreateText("Poster Placeholder", poster.transform, "J", 78, new Color(1f, 1f, 1f, 0.22f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(placeholder.rectTransform);
            view._placeholder = placeholder;

            Text title = UiFactory.CreateText("Title", background.transform, string.Empty, 22, UiTheme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Bold);
            UiFactory.SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(-20f, 48f));
            view._title = title;

            Text subtitle = UiFactory.CreateText("Subtitle", background.transform, string.Empty, 17, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            UiFactory.SetRect(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 13f), new Vector2(-20f, 26f));
            view._subtitle = subtitle;

            Image progressTrack = UiFactory.CreatePanel("Progress Track", poster.transform, UiTheme.ProgressTrack);
            progressTrack.raycastTarget = false;
            UiFactory.SetRect(progressTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 7f));

            Image progressFill = UiFactory.CreatePanel("Progress Fill", progressTrack.transform, UiTheme.AccentBright);
            progressFill.raycastTarget = false;
            progressFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            progressFill.rectTransform.anchoredPosition = Vector2.zero;
            progressFill.rectTransform.sizeDelta = Vector2.zero;
            view._progressFill = progressFill;
            return view;
        }

        public void Bind(
            JellyfinItem item,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            Action<JellyfinItem> onSelected,
            CancellationToken cancellationToken,
            int posterMaxWidth = 360)
        {
            _bindingVersion++;
            _item = item;
            _onSelected = onSelected;
            _title.text = item != null ? item.Name : string.Empty;
            _subtitle.text = item != null ? item.Subtitle : string.Empty;
            _poster.sprite = null;
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

            string url = item != null ? api.BuildPrimaryImageUrl(item, posterMaxWidth) : null;
            if (!string.IsNullOrWhiteSpace(url))
            {
                LoadPosterAsync(url, imageCache, _bindingVersion, cancellationToken).Forget();
            }
        }

        private async Task LoadPosterAsync(
            string url,
            JellyfinImageCache imageCache,
            int bindingVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                Sprite sprite = await imageCache.LoadSpriteAsync(url, cancellationToken);
                if (this == null || bindingVersion != _bindingVersion || sprite == null)
                {
                    return;
                }

                _poster.sprite = sprite;
                _placeholder.gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
                // A shelf refresh or screen transition canceled the image request.
            }
            catch (Exception)
            {
                // Keep the branded placeholder when one image is unavailable.
            }
        }
    }
}
