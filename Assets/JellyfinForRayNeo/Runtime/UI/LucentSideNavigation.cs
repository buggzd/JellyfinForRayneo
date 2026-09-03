using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    /// <summary>
    /// Shared LUCENT navigation rail for every glasses-owned browsing surface.
    /// It stays icon-only until remote or pointer focus enters the rail.
    /// </summary>
    public sealed class LucentSideNavigation
    {
        public enum Section
        {
            None,
            Home,
            Search,
            Library,
            Favorites
        }

        private sealed class NavigationEntry
        {
            public Image Background;
            public Image Indicator;
            public Text Icon;
            public Text Label;
        }

        private readonly RectTransform _root;
        private readonly Dictionary<Section, NavigationEntry> _entries =
            new Dictionary<Section, NavigationEntry>();
        private readonly List<CanvasGroup> _expandingLabels = new List<CanvasGroup>();
        private readonly Text _profileName;
        private readonly Text _serverName;

        public event Action HomeRequested;
        public event Action SearchRequested;
        public event Action LibraryRequested;
        public event Action FavoritesRequested;
        public event Action RefreshRequested;
        public event Action LogoutRequested;

        public Transform FocusRoot => _root;

        public LucentSideNavigation(Transform parent, Section activeSection)
        {
            _root = UiFactory.CreateRect("Lucent Side Navigation", parent);
            UiFactory.SetRect(
                _root,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(UiTheme.SideRailWidth, 0f));

            Image pointerSurface = _root.gameObject.AddComponent<Image>();
            pointerSurface.color = Color.clear;
            pointerSurface.raycastTarget = true;

            Image backdrop = UiFactory.CreateGradientPanel(
                "Navigation Backdrop",
                _root,
                new Color(0.002f, 0.012f, 0.022f, 0.99f),
                new Color(0.002f, 0.012f, 0.022f, 0f),
                true);
            UiFactory.SetRect(
                backdrop.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(510f, 0f));
            CanvasGroup backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();

            Image spectrum = UiFactory.CreateGlowPanel(
                "Navigation Spectrum",
                backdrop.transform,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.08f));
            spectrum.raycastTarget = false;
            UiFactory.SetRect(
                spectrum.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(62f, 0f),
                new Vector2(360f, 700f));

            CreateBrandButton();
            _profileName = CreateProfile();

            CreateNavigationButton(
                Section.Home,
                "Navigation Home",
                "⌂",
                "首页",
                236f,
                () => HomeRequested?.Invoke());
            CreateNavigationButton(
                Section.Search,
                "Navigation Search",
                "⌕",
                "搜索",
                300f,
                () => SearchRequested?.Invoke());
            CreateNavigationButton(
                Section.Library,
                "Navigation Library",
                "▦",
                "媒体库",
                364f,
                () => LibraryRequested?.Invoke());
            CreateNavigationButton(
                Section.Favorites,
                "Navigation Favorites",
                "♡",
                "我的收藏",
                428f,
                () => FavoritesRequested?.Invoke());

            _serverName = CreateServerIdentity();
            CreateUtilityButton(
                "Navigation Refresh",
                "↻",
                "刷新媒体库",
                104f,
                () => RefreshRequested?.Invoke());
            CreateUtilityButton(
                "Navigation Logout",
                "↗",
                "退出服务器",
                40f,
                () => LogoutRequested?.Invoke());

            UiSideRailMotion motion = _root.gameObject.AddComponent<UiSideRailMotion>();
            motion.Configure(backdrop.rectTransform, backdropGroup, _expandingLabels.ToArray());
            SetActive(activeSection);
            SetIdentity(null);
            _root.SetAsLastSibling();
        }

        public void SetActive(Section section)
        {
            foreach (KeyValuePair<Section, NavigationEntry> pair in _entries)
            {
                bool active = pair.Key == section;
                NavigationEntry entry = pair.Value;
                entry.Background.color = active
                    ? new Color(0.45f, 0.80f, 0.91f, 0.095f)
                    : Color.clear;
                entry.Indicator.color = active ? UiTheme.AccentBright : Color.clear;
                entry.Icon.color = active ? UiTheme.Focus : UiTheme.TextSecondary;
                entry.Label.color = active ? UiTheme.TextPrimary : UiTheme.TextSecondary;
            }
        }

        public void SetIdentity(JellyfinSession session)
        {
            if (session == null)
            {
                _profileName.text = "RAYNEO USER";
                _serverName.text = "JELLYFIN";
                return;
            }

            _profileName.text = string.IsNullOrWhiteSpace(session.UserName)
                ? "RAYNEO USER"
                : session.UserName.Trim();
            _serverName.text = string.IsNullOrWhiteSpace(session.ServerName)
                ? "JELLYFIN"
                : session.ServerName.Trim();
        }

        private void CreateBrandButton()
        {
            Button button = CreateRailButton(
                "Lucent Brand",
                "L",
                "LUCENT",
                true);
            RectTransform rect = button.GetComponent<RectTransform>();
            SetTopRect(rect, 36f, 58f);
            button.onClick.AddListener(() => HomeRequested?.Invoke());

            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.fontSize = 20;
            label.fontStyle = FontStyle.Normal;

            Text icon = button.transform.Find("Navigation Icon").GetComponent<Text>();
            icon.fontSize = 13;
            Image ring = UiFactory.CreateRoundedPanel(
                "Brand Ring",
                button.transform,
                new Color(0.02f, 0.07f, 0.10f, 0.66f));
            ring.raycastTarget = false;
            UiFactory.SetRect(
                ring.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(32f, 0f),
                new Vector2(32f, 32f));
            Outline outline = ring.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.83f, 0.97f, 1f, 0.56f);
            outline.effectDistance = new Vector2(1f, -1f);
            ring.transform.SetSiblingIndex(icon.transform.GetSiblingIndex());
            icon.transform.SetAsLastSibling();
        }

        private Text CreateProfile()
        {
            RectTransform profile = UiFactory.CreateRect("Navigation Profile", _root);
            SetTopRect(profile, 114f, 58f);

            Image avatar = UiFactory.CreateRoundedPanel(
                "Profile Avatar",
                profile,
                new Color(0.45f, 0.75f, 0.86f, 0.12f));
            avatar.raycastTarget = false;
            UiFactory.SetRect(
                avatar.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(32f, 0f),
                new Vector2(30f, 30f));
            Outline avatarOutline = avatar.gameObject.AddComponent<Outline>();
            avatarOutline.effectColor = UiTheme.Border;
            avatarOutline.effectDistance = new Vector2(1f, -1f);

            Text avatarMark = UiFactory.CreateText(
                "Profile Mark",
                avatar.transform,
                "·",
                30,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter);
            UiFactory.Stretch(avatarMark.rectTransform);

            RectTransform copy = CreateIdentityCopy(
                "Profile Copy",
                profile,
                "欢迎回来",
                out Text value);
            SetIdentityCopyRect(copy);
            return value;
        }

        private Text CreateServerIdentity()
        {
            RectTransform server = UiFactory.CreateRect("Navigation Server", _root);
            SetBottomRect(server, 174f, 54f);

            Image pulse = UiFactory.CreateRoundedPanel(
                "Server Pulse",
                server,
                UiTheme.Success);
            pulse.raycastTarget = false;
            UiFactory.SetRect(
                pulse.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(32f, 0f),
                new Vector2(8f, 8f));
            Shadow glow = pulse.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(
                UiTheme.Success.r,
                UiTheme.Success.g,
                UiTheme.Success.b,
                0.52f);
            glow.effectDistance = Vector2.zero;

            RectTransform copy = CreateIdentityCopy(
                "Server Copy",
                server,
                "JELLYFIN SERVER",
                out Text value);
            SetIdentityCopyRect(copy);
            return value;
        }

        private RectTransform CreateIdentityCopy(
            string name,
            Transform parent,
            string eyebrow,
            out Text value)
        {
            RectTransform copy = UiFactory.CreateRect(name, parent);
            CanvasGroup group = copy.gameObject.AddComponent<CanvasGroup>();
            _expandingLabels.Add(group);

            Text small = UiFactory.CreateText(
                name + " Eyebrow",
                copy,
                eyebrow,
                10,
                UiTheme.TextMuted,
                TextAnchor.LowerLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                small.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            value = UiFactory.CreateText(
                name + " Value",
                copy,
                string.Empty,
                15,
                UiTheme.TextPrimary,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            UiFactory.SetRect(
                value.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 11;
            value.resizeTextMaxSize = 15;
            return copy;
        }

        private void CreateNavigationButton(
            Section section,
            string name,
            string iconValue,
            string labelValue,
            float top,
            Action action)
        {
            Button button = CreateRailButton(name, iconValue, labelValue, false);
            SetTopRect(button.GetComponent<RectTransform>(), top, 56f);
            button.onClick.AddListener(() => action?.Invoke());

            Image indicator = UiFactory.CreateRoundedPanel(
                "Active Indicator",
                button.transform,
                Color.clear);
            indicator.raycastTarget = false;
            UiFactory.SetRect(
                indicator.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-4f, 0f),
                new Vector2(3f, 30f));

            _entries[section] = new NavigationEntry
            {
                Background = button.GetComponent<Image>(),
                Indicator = indicator,
                Icon = button.transform.Find("Navigation Icon").GetComponent<Text>(),
                Label = button.transform.Find("Label").GetComponent<Text>()
            };
        }

        private void CreateUtilityButton(
            string name,
            string iconValue,
            string labelValue,
            float bottom,
            Action action)
        {
            Button button = CreateRailButton(name, iconValue, labelValue, false);
            SetBottomRect(button.GetComponent<RectTransform>(), bottom, 56f);
            button.onClick.AddListener(() => action?.Invoke());
        }

        private Button CreateRailButton(
            string name,
            string iconValue,
            string labelValue,
            bool brand)
        {
            Button button = UiFactory.CreateButton(
                name,
                _root,
                labelValue,
                Color.clear,
                UiTheme.TextSecondary,
                brand ? 20 : 17);
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.pivot = new Vector2(0f, 0.5f);

            FocusScale focus = button.GetComponent<FocusScale>();
            focus.FocusedScale = 1.025f;
            focus.AnimationSpeed = 11f;
            focus.LocalDepthOffset = -8f;

            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Normal;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(
                label.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(76f, 0f),
                new Vector2(-82f, 0f));
            CanvasGroup labelGroup = label.gameObject.AddComponent<CanvasGroup>();
            _expandingLabels.Add(labelGroup);

            Text icon = UiFactory.CreateText(
                "Navigation Icon",
                button.transform,
                iconValue,
                brand ? 13 : 27,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            UiFactory.SetRect(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(32f, 0f),
                new Vector2(40f, 40f));
            icon.transform.SetAsLastSibling();
            return button;
        }

        private static void SetTopRect(RectTransform rect, float top, float height)
        {
            UiFactory.SetRect(
                rect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(10f, -top),
                new Vector2(-20f, height));
        }

        private static void SetBottomRect(RectTransform rect, float bottom, float height)
        {
            UiFactory.SetRect(
                rect,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(10f, bottom),
                new Vector2(-20f, height));
        }

        private static void SetIdentityCopyRect(RectTransform rect)
        {
            UiFactory.SetRect(
                rect,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(76f, 0f),
                new Vector2(-86f, -4f));
        }
    }
}
