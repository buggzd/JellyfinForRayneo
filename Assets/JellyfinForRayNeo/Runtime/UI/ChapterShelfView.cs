using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class ChapterShelfView
    {
        private readonly RectTransform _root;
        private readonly RectTransform _row;
        private readonly AxisRoutingScrollRect _horizontalScroll;
        private readonly ScrollRect _parentScroll;

        public event Action<long> ChapterSelected;

        public ChapterShelfView(Transform parent, ScrollRect parentScroll)
        {
            _parentScroll = parentScroll;
            _root = UiFactory.CreateRect("Chapters Shelf", parent);
            LayoutElement rootLayout = _root.gameObject.AddComponent<LayoutElement>();
            rootLayout.minHeight = 176f;
            rootLayout.preferredHeight = 176f;
            rootLayout.flexibleHeight = 0f;

            Text title = UiFactory.CreateText(
                "Shelf Title",
                _root,
                "场景与章节",
                30,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 46f));

            RectTransform viewport = UiFactory.CreateRect("Chapter Viewport", _root);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitSurface = viewport.gameObject.AddComponent<Image>();
            hitSurface.color = Color.clear;
            hitSurface.raycastTarget = true;
            UiFactory.SetRect(
                viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -26f),
                new Vector2(0f, -52f));

            _row = UiFactory.CreateRect("Chapter Items", viewport);
            _row.anchorMin = new Vector2(0f, 0f);
            _row.anchorMax = new Vector2(0f, 1f);
            _row.pivot = new Vector2(0f, 0.5f);
            _row.anchoredPosition = Vector2.zero;
            _row.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = _row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(10, 40, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _horizontalScroll = _root.gameObject.AddComponent<AxisRoutingScrollRect>();
            _horizontalScroll.viewport = viewport;
            _horizontalScroll.content = _row;
            _horizontalScroll.horizontal = true;
            _horizontalScroll.vertical = false;
            _horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
            _horizontalScroll.elasticity = 0.085f;
            _horizontalScroll.decelerationRate = 0.11f;
            _horizontalScroll.scrollSensitivity = 56f;
            _horizontalScroll.ConfigureParent(parentScroll);
            _root.gameObject.SetActive(false);
        }

        public void Bind(IList<JellyfinChapter> chapters)
        {
            UiFactory.DestroyChildren(_row);
            List<JellyfinChapter> populated = new List<JellyfinChapter>();
            if (chapters != null)
            {
                foreach (JellyfinChapter chapter in chapters)
                {
                    if (chapter != null && chapter.StartPositionTicks >= 0L)
                    {
                        populated.Add(chapter);
                    }
                }
            }

            if (populated.Count == 0)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            _root.gameObject.SetActive(true);
            UiFactory.AddScrollReveal(_root.gameObject, _parentScroll, 0.06f);
            for (int index = 0; index < populated.Count; index++)
            {
                JellyfinChapter chapter = populated[index];
                string chapterName = string.IsNullOrWhiteSpace(chapter.Name)
                    ? "章节 " + (index + 1)
                    : chapter.Name;
                string label = FormatTime(chapter.StartPositionTicks) + "  ·  " + chapterName;
                Button button = UiFactory.CreateButton(
                    "Chapter - " + (index + 1),
                    _row,
                    label,
                    UiTheme.SurfaceSoft,
                    UiTheme.TextPrimary,
                    18);
                LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 300f;
                element.preferredWidth = 300f;
                element.minHeight = 74f;
                element.preferredHeight = 74f;
                element.flexibleWidth = 0f;
                element.flexibleHeight = 0f;
                Text buttonLabel = button.GetComponentInChildren<Text>();
                buttonLabel.alignment = TextAnchor.MiddleLeft;
                buttonLabel.resizeTextForBestFit = true;
                buttonLabel.resizeTextMinSize = 14;
                buttonLabel.resizeTextMaxSize = 18;
                long startPosition = chapter.StartPositionTicks;
                button.onClick.AddListener(() => ChapterSelected?.Invoke(startPosition));
                FocusScale focus = button.GetComponent<FocusScale>();
                focus?.ConfigureScrollRects(_horizontalScroll, _parentScroll);
                UiFactory.AddItemReveal(
                    button.gameObject,
                    Mathf.Min(0.20f, index * 0.024f));
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_row);
            _horizontalScroll.StopMovement();
            _horizontalScroll.horizontalNormalizedPosition = 0f;
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        private static string FormatTime(long ticks)
        {
            TimeSpan span = TimeSpan.FromSeconds(
                Math.Max(0L, ticks) / (double)AppConstants.TicksPerSecond);
            return span.TotalHours >= 1d
                ? string.Format("{0}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds)
                : string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);
        }
    }
}
