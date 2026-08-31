using System;
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
        private readonly Image _backdrop;
        private readonly Image _poster;
        private readonly Text _posterPlaceholder;
        private readonly Text _title;
        private readonly Text _metadata;
        private readonly Text _overview;
        private readonly Button _continueButton;
        private readonly Text _continueLabel;
        private readonly Button _fromStartButton;
        private readonly Button _episodesButton;
        private JellyfinItem _item;
        private int _bindingVersion;

        public event Action<JellyfinItem, long> PlayRequested;
        public event Action<JellyfinItem> EpisodesRequested;
        public event Action CloseRequested;

        public DetailView(Transform parent)
        {
            Image rootImage = UiFactory.CreatePanel("Detail Screen", parent, UiTheme.Background);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;

            _backdrop = UiFactory.CreatePanel("Backdrop", rootImage.transform, new Color(0.06f, 0.07f, 0.10f, 1f));
            _backdrop.preserveAspect = false;
            _backdrop.raycastTarget = false;
            UiFactory.SetRect(_backdrop.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 560f));

            Image shade = UiFactory.CreatePanel("Backdrop Shade", _backdrop.transform, new Color(0.015f, 0.02f, 0.035f, 0.62f));
            shade.raycastTarget = false;
            UiFactory.Stretch(shade.rectTransform);

            _poster = UiFactory.CreatePanel("Poster", rootImage.transform, UiTheme.SurfaceRaised);
            _poster.preserveAspect = true;
            _poster.raycastTarget = false;
            UiFactory.SetRect(_poster.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, -40f), new Vector2(340f, 510f));
            _posterPlaceholder = UiFactory.CreateText("Poster Placeholder", _poster.transform, "J", 120, new Color(1f, 1f, 1f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(_posterPlaceholder.rectTransform);

            _title = UiFactory.CreateText("Title", rootImage.transform, string.Empty, 58, UiTheme.TextPrimary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiFactory.SetRect(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(500f, 232f), new Vector2(-620f, 116f));

            _metadata = UiFactory.CreateText("Metadata", rootImage.transform, string.Empty, 24, UiTheme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(_metadata.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(502f, 157f), new Vector2(-620f, 48f));

            _overview = UiFactory.CreateText("Overview", rootImage.transform, string.Empty, 26, UiTheme.TextPrimary, TextAnchor.UpperLeft);
            _overview.lineSpacing = 1.15f;
            UiFactory.SetRect(_overview.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(502f, 76f), new Vector2(-620f, 180f));

            _continueButton = UiFactory.CreateButton("Continue", rootImage.transform, "播放", UiTheme.Accent, UiTheme.TextPrimary, 28);
            UiFactory.SetRect(_continueButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(500f, -82f), new Vector2(250f, 68f));
            _continueLabel = _continueButton.GetComponentInChildren<Text>();
            _continueButton.onClick.AddListener(() =>
            {
                long position = _item != null && _item.UserData != null ? _item.UserData.PlaybackPositionTicks : 0L;
                PlayRequested?.Invoke(_item, Math.Max(0L, position));
            });

            _fromStartButton = UiFactory.CreateButton("From Start", rootImage.transform, "从头播放", UiTheme.SurfaceRaised, UiTheme.TextPrimary, 26);
            UiFactory.SetRect(_fromStartButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(780f, -82f), new Vector2(220f, 68f));
            _fromStartButton.onClick.AddListener(() => PlayRequested?.Invoke(_item, 0L));

            _episodesButton = UiFactory.CreateButton("Episodes", rootImage.transform, "浏览剧集", UiTheme.Accent, UiTheme.TextPrimary, 28);
            UiFactory.SetRect(_episodesButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(500f, -82f), new Vector2(250f, 68f));
            _episodesButton.onClick.AddListener(() => EpisodesRequested?.Invoke(_item));

            Button close = UiFactory.CreateButton("Close", rootImage.transform, "返回", new Color(0.08f, 0.09f, 0.13f, 0.9f), UiTheme.TextPrimary, 23);
            UiFactory.SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -48f), new Vector2(128f, 58f));
            close.onClick.AddListener(() => CloseRequested?.Invoke());

            _root.SetActive(false);
        }

        public bool IsVisible
        {
            get { return _root.activeSelf; }
        }

        public JellyfinItem CurrentItem
        {
            get { return _item; }
        }

        public void Show(
            JellyfinItem item,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            CancellationToken cancellationToken)
        {
            _bindingVersion++;
            _item = item;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _title.text = item != null ? item.Name : string.Empty;
            _metadata.text = BuildMetadata(item);
            _overview.text = item != null && !string.IsNullOrWhiteSpace(item.Overview)
                ? item.Overview
                : "暂无简介。";
            _poster.sprite = null;
            _backdrop.sprite = null;
            _posterPlaceholder.gameObject.SetActive(true);

            long resumePosition = item != null && item.UserData != null ? item.UserData.PlaybackPositionTicks : 0L;
            _continueLabel.text = resumePosition > AppConstants.TicksPerSecond * 10L ? "继续播放" : "播放";
            _fromStartButton.gameObject.SetActive(resumePosition > AppConstants.TicksPerSecond * 10L);
            bool playable = item != null && item.IsPlayable;
            _continueButton.gameObject.SetActive(playable);
            bool isSeries = item != null && string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase);
            _episodesButton.gameObject.SetActive(isSeries);
            if (!playable)
            {
                _fromStartButton.gameObject.SetActive(false);
            }

            int version = _bindingVersion;
            LoadImageAsync(api.BuildPrimaryImageUrl(item, 520), imageCache, _poster, _posterPlaceholder.gameObject, version, cancellationToken).Forget();
            LoadImageAsync(api.BuildBackdropImageUrl(item, 1600), imageCache, _backdrop, null, version, cancellationToken).Forget();
        }

        public void Hide()
        {
            _bindingVersion++;
            _root.SetActive(false);
        }

        private async Task LoadImageAsync(
            string url,
            JellyfinImageCache cache,
            Image target,
            GameObject placeholder,
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
                if (bindingVersion != _bindingVersion || sprite == null)
                {
                    return;
                }
                target.sprite = sprite;
                if (placeholder != null)
                {
                    placeholder.SetActive(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Metadata remains usable even when a backdrop or poster fails.
            }
        }

        private static string BuildMetadata(JellyfinItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string year = item.ProductionYear.HasValue ? item.ProductionYear.Value.ToString() : string.Empty;
            string duration = string.Empty;
            if (item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0)
            {
                TimeSpan span = TimeSpan.FromSeconds(item.RunTimeTicks.Value / (double)AppConstants.TicksPerSecond);
                duration = span.TotalHours >= 1d
                    ? string.Format("{0}小时{1}分", (int)span.TotalHours, span.Minutes)
                    : string.Format("{0}分钟", Math.Max(1, span.Minutes));
            }

            string genres = item.Genres != null ? string.Join(" / ", item.Genres.Take(3)) : string.Empty;
            string rating = item.CommunityRating.HasValue ? item.CommunityRating.Value.ToString("0.0") + " ★" : string.Empty;
            return string.Join("   ", new[] { year, duration, item.OfficialRating, genres, rating }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
