using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    internal static class UiFactory
    {
        private static Sprite _roundedSprite;
        private static Sprite _radialGlowSprite;
        private static Sprite _grainSprite;

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static Image CreateRoundedPanel(string name, Transform parent, Color color)
        {
            Image image = CreatePanel(name, parent, color);
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Image CreateGradientPanel(
            string name,
            Transform parent,
            Color startColor,
            Color endColor,
            bool horizontal = false)
        {
            Image image = CreatePanel(name, parent, Color.white);
            image.raycastTarget = false;
            UiGradient gradient = image.gameObject.AddComponent<UiGradient>();
            gradient.StartColor = startColor;
            gradient.EndColor = endColor;
            gradient.Horizontal = horizontal;
            return image;
        }

        public static Image CreateGlowPanel(string name, Transform parent, Color color)
        {
            Image image = CreatePanel(name, parent, color);
            image.sprite = RadialGlowSprite;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateGlassPanel(
            string name,
            Transform parent,
            Color color,
            Vector2 shadowDistance)
        {
            Image panel = CreateRoundedPanel(name, parent, color);
            UiGradient gradient = panel.gameObject.AddComponent<UiGradient>();
            gradient.StartColor = new Color(1f, 1f, 1f, 1f);
            gradient.EndColor = new Color(0.64f, 0.84f, 0.94f, 0.70f);
            gradient.Horizontal = true;

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            Shadow shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.02f, 0.04f, 0.42f);
            shadow.effectDistance = shadowDistance;
            shadow.useGraphicAlpha = true;
            return panel;
        }

        public static Image CreateFilmGrain(Transform parent, float opacity = 0.035f)
        {
            Image grain = CreatePanel(
                "Film Grain",
                parent,
                new Color(1f, 1f, 1f, Mathf.Clamp01(opacity)));
            grain.sprite = GrainSprite;
            grain.type = Image.Type.Tiled;
            grain.raycastTarget = false;
            Stretch(grain.rectTransform);
            return grain;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle fontStyle = FontStyle.Normal)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = UiTheme.Font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.text = value ?? string.Empty;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color background,
            Color foreground,
            int fontSize = 28)
        {
            Image image = CreateRoundedPanel(name, parent, background);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            colors.selectedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            colors.pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f);
            colors.disabledColor = new Color(0.48f, 0.48f, 0.52f, 0.55f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            Image focusWash = CreateRoundedPanel(
                "Focus Wash",
                image.transform,
                Color.clear);
            focusWash.raycastTarget = false;
            Stretch(focusWash.rectTransform, -2f, -2f, -2f, -2f);

            Text text = CreateText("Label", image.transform, label, fontSize, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 12f, 12f, 8f, 8f);
            FocusScale focus = image.gameObject.AddComponent<FocusScale>();
            focus.ConfigureFocusGraphic(
                focusWash,
                new Color(
                    UiTheme.AccentBright.r,
                    UiTheme.AccentBright.g,
                    UiTheme.AccentBright.b,
                    0.14f));
            return button;
        }

        public static UiViewMotion AddViewMotion(
            GameObject target,
            float enterOffset = 28f,
            float enterScale = 0.985f)
        {
            UiViewMotion motion = target.GetComponent<UiViewMotion>();
            if (motion == null)
            {
                motion = target.AddComponent<UiViewMotion>();
            }
            motion.EnterOffset = enterOffset;
            motion.EnterScale = enterScale;
            return motion;
        }

        public static UiItemReveal AddItemReveal(GameObject target, float delay)
        {
            UiItemReveal reveal = target.GetComponent<UiItemReveal>();
            if (reveal == null)
            {
                reveal = target.AddComponent<UiItemReveal>();
            }
            reveal.Configure(delay);
            return reveal;
        }

        public static UiScrollReveal AddScrollReveal(
            GameObject target,
            ScrollRect scrollRect,
            float delay = 0f)
        {
            UiScrollReveal reveal = target.GetComponent<UiScrollReveal>();
            if (reveal == null)
            {
                reveal = target.AddComponent<UiScrollReveal>();
            }
            reveal.Configure(scrollRect, delay);
            return reveal;
        }

        public static void RevealGraphic(Graphic graphic, float duration = 0.28f)
        {
            if (graphic == null)
            {
                return;
            }

            graphic.canvasRenderer.SetAlpha(0f);
            graphic.CrossFadeAlpha(1f, Mathf.Max(0.01f, duration), true);
        }

        public static RectTransform CreateAmbientBackdrop(Transform parent)
        {
            RectTransform ambient = CreateRect("Ambient Backdrop", parent);
            Stretch(ambient);
            ambient.SetAsFirstSibling();

            Image teal = CreatePanel("Teal Glow", ambient, UiTheme.GlowTeal);
            teal.sprite = RadialGlowSprite;
            teal.raycastTarget = false;
            SetRect(
                teal.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(240f, -160f),
                new Vector2(1160f, 760f));
            UiAmbientFloat tealMotion = teal.gameObject.AddComponent<UiAmbientFloat>();
            tealMotion.Amplitude = new Vector2(46f, 22f);
            tealMotion.Speed = 0.055f;
            tealMotion.Phase = 0.4f;

            Image violet = CreatePanel("Violet Glow", ambient, UiTheme.GlowViolet);
            violet.sprite = RadialGlowSprite;
            violet.raycastTarget = false;
            SetRect(
                violet.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-170f, 100f),
                new Vector2(980f, 700f));
            UiAmbientFloat violetMotion = violet.gameObject.AddComponent<UiAmbientFloat>();
            violetMotion.Amplitude = new Vector2(34f, 28f);
            violetMotion.Speed = 0.045f;
            violetMotion.Phase = 2.1f;

            Image blue = CreatePanel("Blue Spectrum", ambient, UiTheme.GlowTeal);
            blue.sprite = RadialGlowSprite;
            blue.raycastTarget = false;
            SetRect(
                blue.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-190f, -130f),
                new Vector2(1280f, 780f));
            UiAmbientFloat blueMotion = blue.gameObject.AddComponent<UiAmbientFloat>();
            blueMotion.Amplitude = new Vector2(54f, 26f);
            blueMotion.Speed = 0.038f;
            blueMotion.Phase = 4.2f;
            blueMotion.ScalePulse = 0.025f;

            Image leftVeil = CreateGradientPanel(
                "Ambient Left Veil",
                ambient,
                new Color(0.004f, 0.018f, 0.034f, 0.96f),
                new Color(0.004f, 0.018f, 0.034f, 0.08f),
                true);
            Stretch(leftVeil.rectTransform);

            Image edgeFade = CreateGradientPanel(
                "Ambient Edge Fade",
                ambient,
                new Color(0.004f, 0.018f, 0.034f, 0.94f),
                new Color(0.004f, 0.018f, 0.034f, 0.05f));
            Stretch(edgeFade.rectTransform);

            return ambient;
        }

        public static InputField CreateInputField(
            string name,
            Transform parent,
            string placeholderValue,
            InputField.ContentType contentType)
        {
            Image background = CreateRoundedPanel(name, parent, UiTheme.SurfaceRaised);
            InputField input = background.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.contentType = contentType;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 256;

            Text text = CreateText("Text", background.transform, string.Empty, 27, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 24f, 24f, 8f, 8f);
            input.textComponent = text;

            Text placeholder = CreateText("Placeholder", background.transform, placeholderValue, 27, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            placeholder.fontStyle = FontStyle.Italic;
            Stretch(placeholder.rectTransform, 24f, 24f, 8f, 8f);
            input.placeholder = placeholder;
            background.gameObject.AddComponent<FocusScale>().FocusedScale = 1.015f;
            return input;
        }

        public static Canvas CreateWorldSpaceCanvas(Camera camera)
        {
            GameObject canvasObject = new GameObject(
                "Jellyfin Spatial Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            AddSpatialRaycaster(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 20;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920f, 1080f);
            Air3SDisplayController display = camera != null
                ? camera.GetComponent<Air3SDisplayController>()
                : null;
            float screenDistance = display != null
                ? display.ScreenDistance
                : Air3SDisplayController.DefaultScreenDistance;
            float worldScale = display != null
                ? display.CanvasWorldScale
                : Air3SDisplayController.CalculateCanvasWorldScale(
                    screenDistance,
                    27f);
            rect.localScale = Vector3.one * worldScale;
            if (camera != null)
            {
                rect.position = camera.transform.position
                    + camera.transform.forward * screenDistance;
                rect.rotation = camera.transform.rotation;
            }
            else
            {
                rect.position = new Vector3(0f, 0f, screenDistance);
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 1.5f;
            return canvas;
        }

        private static void AddSpatialRaycaster(GameObject canvasObject)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        public static Camera EnsureMainCamera()
        {
            Air3SDisplayController displayController =
                Object.FindObjectOfType<Air3SDisplayController>(true);
            if (displayController != null)
            {
                return displayController.MonoCamera;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                if (camera.GetComponent<Air3SDisplayController>() == null)
                {
                    camera.gameObject.AddComponent<Air3SDisplayController>();
                }
                return camera;
            }

            GameObject cameraObject = new GameObject(
                "RayNeo Air 3S Display Camera",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 27f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            cameraObject.AddComponent<Air3SDisplayController>();
            return camera;
        }

        public static void EnsureEventSystem()
        {
            EventSystem current = EventSystem.current;
            if (current == null)
            {
                GameObject eventSystemObject = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(null);
                current = eventSystemObject.GetComponent<EventSystem>();
            }

            // Navigation is routed through DirectionalFocusNavigator so every
            // command is constrained to the visible page. Leaving Unity's
            // built-in axis navigation enabled can process the same gesture a
            // second time and move focus into stale or underlying controls.
            current.sendNavigationEvents = false;

            StandaloneInputModule standardModule = current.GetComponent<StandaloneInputModule>();
            if (standardModule == null)
            {
                standardModule = current.gameObject.AddComponent<StandaloneInputModule>();
            }
            standardModule.enabled = true;

            foreach (BaseInputModule module in current.GetComponents<BaseInputModule>())
            {
                if (module != standardModule)
                {
                    module.enabled = false;
                }
            }
        }

        public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        public static void DestroyChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite != null)
                {
                    return _roundedSprite;
                }

                const int size = 64;
                const float radius = 18f;
                float half = size * 0.5f;
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = Mathf.Abs(x + 0.5f - half) - half + radius;
                        float py = Mathf.Abs(y + 0.5f - half) - half + radius;
                        float outside = Mathf.Sqrt(
                            Mathf.Max(px, 0f) * Mathf.Max(px, 0f)
                            + Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
                        float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                        float signedDistance = inside + outside - radius;
                        float alpha = Mathf.Clamp01(0.5f - signedDistance);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "Runtime Rounded Rectangle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels(pixels);
                texture.Apply(false, true);

                float border = radius + 2f;
                _roundedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(border, border, border, border));
                _roundedSprite.name = "Runtime Rounded Rectangle";
                _roundedSprite.hideFlags = HideFlags.HideAndDontSave;
                return _roundedSprite;
            }
        }

        private static Sprite RadialGlowSprite
        {
            get
            {
                if (_radialGlowSprite != null)
                {
                    return _radialGlowSprite;
                }

                const int size = 128;
                float half = size * 0.5f;
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float normalized = Vector2.Distance(
                            new Vector2(x + 0.5f, y + 0.5f),
                            new Vector2(half, half)) / half;
                        float alpha = Mathf.Clamp01(1f - normalized);
                        alpha = alpha * alpha * (3f - 2f * alpha);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "Runtime Radial Glow",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels(pixels);
                texture.Apply(false, true);

                _radialGlowSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _radialGlowSprite.name = "Runtime Radial Glow";
                _radialGlowSprite.hideFlags = HideFlags.HideAndDontSave;
                return _radialGlowSprite;
            }
        }

        private static Sprite GrainSprite
        {
            get
            {
                if (_grainSprite != null)
                {
                    return _grainSprite;
                }

                const int size = 96;
                Color[] pixels = new Color[size * size];
                uint state = 0x7f4a7c15u;
                for (int index = 0; index < pixels.Length; index++)
                {
                    state = state * 1664525u + 1013904223u;
                    float value = ((state >> 24) & 0xff) / 255f;
                    float alpha = Mathf.Lerp(0.05f, 0.46f, value);
                    pixels[index] = new Color(1f, 1f, 1f, alpha);
                }

                Texture2D texture = new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "Runtime Film Grain",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels(pixels);
                texture.Apply(false, true);

                _grainSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    24f);
                _grainSprite.name = "Runtime Film Grain";
                _grainSprite.hideFlags = HideFlags.HideAndDontSave;
                return _grainSprite;
            }
        }
    }
}
