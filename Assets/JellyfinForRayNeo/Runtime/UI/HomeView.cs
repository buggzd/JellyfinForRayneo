using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class HomeView
    {
        private readonly GameObject _root;
        private readonly Text _serverLabel;
        private readonly Text _emptyLabel;
        private readonly ScrollRect _verticalScroll;
        private readonly RectTransform _content;
        private readonly JellyfinApiClient _api;
        private readonly JellyfinImageCache _imageCache;

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

            Image header = UiFactory.CreatePanel("Header", rootRect, new Color(0.025f, 0.03f, 0.05f, 0.98f));
            UiFactory.SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 118f));

            Text brand = UiFactory.CreateText("Brand", header.transform, "JELLYFIN", 42, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(brand.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 12f), new Vector2(300f, 62f));
            Text rayneo = UiFactory.CreateText("RayNeo", header.transform, "for RayNeo Air", 21, UiTheme.AccentBright, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(rayneo.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(55f, -32f), new Vector2(320f, 32f));

            _serverLabel = UiFactory.CreateText("Server", header.transform, string.Empty, 22, UiTheme.TextSecondary, TextAnchor.MiddleRight);
            UiFactory.SetRect(_serverLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-390f, 0f), new Vector2(650f, 54f));

            Button refreshButton = UiFactory.CreateButton("Refresh", header.transform, "刷新", UiTheme.SurfaceRaised, UiTheme.TextPrimary, 22);
            UiFactory.SetRect(refreshButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-185f, 0f), new Vector2(118f, 58f));
            refreshButton.onClick.AddListener(() => RefreshRequested?.Invoke());

            Button logoutButton = UiFactory.CreateButton("Logout", header.transform, "退出", new Color(0.25f, 0.12f, 0.17f, 1f), UiTheme.TextPrimary, 22);
            UiFactory.SetRect(logoutButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-52f, 0f), new Vector2(110f, 58f));
            logoutButton.onClick.AddListener(() => LogoutRequested?.Invoke());

            RectTransform viewport = UiFactory.CreateRect("Shelves Viewport", rootRect);
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Stretch(viewport, 42f, 42f, 132f, 24f);

            _content = UiFactory.CreateRect("Shelves", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup verticalLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(10, 10, 10, 30);
            verticalLayout.spacing = 22f;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlHeight = false;
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
            _verticalScroll.scrollSensitivity = 42f;
            _verticalScroll.decelerationRate = 0.13f;

            _emptyLabel = UiFactory.CreateText(
                "Empty",
                rootRect,
                "媒体库中还没有可显示的电影或剧集",
                32,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(_emptyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1000f, 100f));
            _emptyLabel.gameObject.SetActive(false);
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
            UiFactory.DestroyChildren(_content);
            bool hasSections = sections != null && sections.Count > 0;
            _emptyLabel.gameObject.SetActive(!hasSections);
            if (!hasSections)
            {
                return;
            }

            foreach (JellyfinHomeSection section in sections)
            {
                if (section == null || section.Items == null || section.Items.Count == 0)
                {
                    continue;
                }
                CreateShelf(section, cancellationToken);
            }

            Canvas.ForceUpdateCanvases();
            _verticalScroll.verticalNormalizedPosition = 1f;
        }

        private void CreateShelf(JellyfinHomeSection section, CancellationToken cancellationToken)
        {
            RectTransform shelf = UiFactory.CreateRect("Shelf - " + section.Title, _content);
            LayoutElement shelfLayout = shelf.gameObject.AddComponent<LayoutElement>();
            shelfLayout.preferredHeight = 360f;
            shelfLayout.minHeight = 360f;

            Text title = UiFactory.CreateText("Shelf Title", shelf, section.Title, 32, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-10f, 52f));

            RectTransform viewport = UiFactory.CreateRect("Viewport", shelf);
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.SetRect(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(0f, -62f));

            RectTransform row = UiFactory.CreateRect("Posters", viewport);
            row.anchorMin = new Vector2(0f, 0.5f);
            row.anchorMax = new Vector2(0f, 0.5f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(0f, 300f);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.padding = new RectOffset(8, 32, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
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
            horizontalScroll.scrollSensitivity = 46f;
            horizontalScroll.decelerationRate = 0.13f;

            foreach (JellyfinItem item in section.Items)
            {
                PosterCardView card = PosterCardView.Create(row);
                card.Bind(item, _api, _imageCache, selected => ItemSelected?.Invoke(selected), cancellationToken);
            }
        }
    }
}

