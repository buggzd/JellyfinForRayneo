using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class DetailShelfView
    {
        private readonly RectTransform _root;
        private readonly Text _title;
        private readonly RectTransform _row;
        private readonly AxisRoutingScrollRect _horizontalScroll;
        private readonly ScrollRect _parentScroll;

        public event Action<JellyfinItem> ItemSelected;

        public DetailShelfView(
            Transform parent,
            ScrollRect parentScroll,
            string objectName)
        {
            _parentScroll = parentScroll;
            _root = UiFactory.CreateRect(objectName, parent);
            LayoutElement rootLayout = _root.gameObject.AddComponent<LayoutElement>();
            rootLayout.flexibleHeight = 0f;

            _title = UiFactory.CreateText(
                "Shelf Title",
                _root,
                string.Empty,
                30,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 48f));

            RectTransform viewport = UiFactory.CreateRect("Shelf Viewport", _root);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitSurface = viewport.gameObject.AddComponent<Image>();
            hitSurface.color = Color.clear;
            hitSurface.raycastTarget = true;
            UiFactory.SetRect(
                viewport,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -28f),
                new Vector2(0f, -56f));

            _row = UiFactory.CreateRect("Shelf Items", viewport);
            _row.anchorMin = new Vector2(0f, 0f);
            _row.anchorMax = new Vector2(0f, 1f);
            _row.pivot = new Vector2(0f, 0.5f);
            _row.anchoredPosition = Vector2.zero;
            _row.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = _row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.padding = new RectOffset(12, 42, 10, 10);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
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

        public void Bind(
            string title,
            IList<JellyfinItem> items,
            bool landscape,
            JellyfinApiClient api,
            JellyfinImageCache imageCache,
            CancellationToken cancellationToken)
        {
            UiFactory.DestroyChildren(_row);
            List<JellyfinItem> populated = new List<JellyfinItem>();
            if (items != null)
            {
                foreach (JellyfinItem item in items)
                {
                    if (item != null)
                    {
                        populated.Add(item);
                    }
                }
            }

            if (populated.Count == 0)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            _title.text = title ?? string.Empty;
            float height = landscape ? 334f : 454f;
            LayoutElement layout = _root.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            _root.gameObject.SetActive(true);
            UiFactory.AddScrollReveal(_root.gameObject, _parentScroll, 0.06f);

            for (int index = 0; index < populated.Count; index++)
            {
                JellyfinItem item = populated[index];
                PosterCardView card = PosterCardView.Create(_row, landscape);
                card.ConfigureScrollRects(_horizontalScroll, _parentScroll);
                card.Bind(
                    item,
                    api,
                    imageCache,
                    selected => ItemSelected?.Invoke(selected),
                    cancellationToken,
                    landscape ? 760 : 480,
                    landscape || item.IsBrowsableContainer);
                UiFactory.AddItemReveal(
                    card.gameObject,
                    Mathf.Min(0.20f, index * 0.025f));
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
    }
}
