using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    [RequireComponent(typeof(Selectable))]
    public sealed class FocusScale : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler,
        ISubmitHandler
    {
        public float FocusedScale = 1.07f;
        public float AnimationSpeed = 13f;
        public float LocalDepthOffset = -16f;
        public float ScrollMargin = 34f;

        private Vector3 _restScale;
        private float _restDepth;
        private bool _focused;
        private Graphic _targetGraphic;
        private Color _restColor;
        private Graphic _focusGraphic;
        private Color _focusColor;
        private Color _restFocusColor;
        private Graphic _shadowGraphic;
        private Color _restShadowColor;
        private Color _focusShadowColor;
        private ScrollRect _horizontalScroll;
        private ScrollRect _verticalScroll;
        private bool _scrollRectsResolved;
        private Vector3 _scaleVelocity;
        private float _depthVelocity;
        private float _pressAmount;
        private float _pressVelocity;
        private float _submitPulse;
        private bool _pressed;

        private void Awake()
        {
            _restScale = transform.localScale;
            _restDepth = transform.localPosition.z;
            Selectable selectable = GetComponent<Selectable>();
            _targetGraphic = selectable != null ? selectable.targetGraphic : null;
            if (_targetGraphic != null)
            {
                _restColor = _targetGraphic.color;
            }
        }

        public void ConfigureFocusGraphic(Graphic graphic, Color focusedColor)
        {
            _focusGraphic = graphic;
            _focusColor = focusedColor;
            if (_focusGraphic != null)
            {
                _restFocusColor = _focusGraphic.color;
            }
        }

        public void ConfigureShadowGraphic(Graphic graphic, Color focusedColor)
        {
            _shadowGraphic = graphic;
            _focusShadowColor = focusedColor;
            if (_shadowGraphic != null)
            {
                _restShadowColor = _shadowGraphic.color;
            }
        }

        public void ConfigureScrollRects(ScrollRect horizontalScroll, ScrollRect verticalScroll)
        {
            _horizontalScroll = horizontalScroll;
            _verticalScroll = verticalScroll;
            _scrollRectsResolved = true;
        }

        internal ScrollRect HorizontalScroll
        {
            get
            {
                ResolveScrollRects();
                return _horizontalScroll;
            }
        }

        internal ScrollRect VerticalScroll
        {
            get
            {
                ResolveScrollRects();
                return _verticalScroll;
            }
        }

        private void Update()
        {
            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _pressAmount = Mathf.SmoothDamp(
                _pressAmount,
                _pressed ? 1f : 0f,
                ref _pressVelocity,
                _pressed ? 0.045f : 0.085f,
                Mathf.Infinity,
                deltaTime);
            _submitPulse = Mathf.MoveTowards(_submitPulse, 0f, deltaTime * 5.5f);

            float focusScale = _focused ? FocusedScale : 1f;
            float pressedScale = Mathf.Lerp(1f, 0.965f, _pressAmount);
            pressedScale *= 1f - _submitPulse * 0.025f;
            Vector3 targetScale = _restScale * focusScale * pressedScale;
            transform.localScale = Vector3.SmoothDamp(
                transform.localScale,
                targetScale,
                ref _scaleVelocity,
                1f / Mathf.Max(1f, AnimationSpeed),
                Mathf.Infinity,
                deltaTime);

            Vector3 position = transform.localPosition;
            float targetDepth = _focused ? _restDepth + LocalDepthOffset : _restDepth;
            position.z = Mathf.SmoothDamp(
                position.z,
                targetDepth,
                ref _depthVelocity,
                1f / Mathf.Max(1f, AnimationSpeed),
                Mathf.Infinity,
                deltaTime);
            transform.localPosition = position;

            if (_targetGraphic != null)
            {
                Color targetColor = _focused
                    ? Color.Lerp(_restColor, Color.white, 0.16f)
                    : _restColor;
                _targetGraphic.color = Color.Lerp(
                    _targetGraphic.color,
                    targetColor,
                    1f - Mathf.Exp(-AnimationSpeed * deltaTime));
            }

            if (_focusGraphic != null)
            {
                Color targetFocusColor = _focused ? _focusColor : _restFocusColor;
                _focusGraphic.color = Color.Lerp(
                    _focusGraphic.color,
                    targetFocusColor,
                    1f - Mathf.Exp(-AnimationSpeed * deltaTime));
            }

            if (_shadowGraphic != null)
            {
                Color targetShadowColor = _focused ? _focusShadowColor : _restShadowColor;
                _shadowGraphic.color = Color.Lerp(
                    _shadowGraphic.color,
                    targetShadowColor,
                    1f - Mathf.Exp(-AnimationSpeed * deltaTime));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetFocused(true);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pressed = false;
            SetFocused(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _pressed = false;
            SetFocused(false);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            _submitPulse = 1f;
        }

        private void SetFocused(bool focused)
        {
            _focused = focused;
            if (focused)
            {
                ResolveScrollRects();
                EnsureVisible(_horizontalScroll);
                EnsureVisible(_verticalScroll);
            }
        }

        private void ResolveScrollRects()
        {
            if (_scrollRectsResolved)
            {
                return;
            }

            _scrollRectsResolved = true;
            foreach (ScrollRect scrollRect in GetComponentsInParent<ScrollRect>(true))
            {
                if (scrollRect == null
                    || scrollRect.content == null
                    || !transform.IsChildOf(scrollRect.content))
                {
                    continue;
                }

                if (_horizontalScroll == null && scrollRect.horizontal)
                {
                    _horizontalScroll = scrollRect;
                }
                if (_verticalScroll == null && scrollRect.vertical)
                {
                    _verticalScroll = scrollRect;
                }
            }
        }

        private void EnsureVisible(ScrollRect scrollRect)
        {
            RectTransform item = transform as RectTransform;
            if (scrollRect == null || item == null || scrollRect.viewport == null || scrollRect.content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scrollRect.viewport, item);
            Rect viewportRect = scrollRect.viewport.rect;
            Vector2 position = scrollRect.content.anchoredPosition;

            if (scrollRect.horizontal)
            {
                float left = viewportRect.xMin + ScrollMargin;
                float right = viewportRect.xMax - ScrollMargin;
                if (bounds.min.x < left)
                {
                    position.x += left - bounds.min.x;
                }
                else if (bounds.max.x > right)
                {
                    position.x -= bounds.max.x - right;
                }
            }

            if (scrollRect.vertical)
            {
                float bottom = viewportRect.yMin + ScrollMargin;
                float top = viewportRect.yMax - ScrollMargin;
                if (bounds.min.y < bottom)
                {
                    position.y += bottom - bounds.min.y;
                }
                else if (bounds.max.y > top)
                {
                    position.y -= bounds.max.y - top;
                }
            }

            scrollRect.StopMovement();
            SmoothScrollMotion motion = scrollRect.GetComponent<SmoothScrollMotion>();
            if (motion == null)
            {
                motion = scrollRect.gameObject.AddComponent<SmoothScrollMotion>();
            }
            motion.ScrollTo(position);
        }
    }

    public sealed class DirectionalFocusNavigator
    {
        private const float DirectionEpsilon = 0.5f;
        private const float SameRowOverlapRatio = 0.35f;
        private Transform _scope;

        public Transform Scope => _scope;

        public void SetScope(Transform scope)
        {
            _scope = scope;
            if (EventSystem.current == null)
            {
                return;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null
                && (_scope == null || !selected.transform.IsChildOf(_scope)))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public bool Handle(CompanionRemoteCommand command)
        {
            switch (command)
            {
                case CompanionRemoteCommand.Up:
                    return Move(Vector2.up);
                case CompanionRemoteCommand.Down:
                    return Move(Vector2.down);
                case CompanionRemoteCommand.Left:
                    return Move(Vector2.left);
                case CompanionRemoteCommand.Right:
                    return Move(Vector2.right);
                case CompanionRemoteCommand.Submit:
                    return Submit();
                default:
                    return false;
            }
        }

        public bool SelectPreferred(params string[] objectNames)
        {
            List<Selectable> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                ClearSelection();
                return false;
            }

            foreach (string objectName in objectNames ?? new string[0])
            {
                Selectable match = TopLeft(candidates.Where(candidate =>
                    candidate != null && candidate.gameObject.name == objectName));
                if (match != null)
                {
                    Select(match);
                    return true;
                }
            }

            Select(TopLeft(candidates));
            return true;
        }

        public void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private bool Move(Vector2 direction)
        {
            List<Selectable> candidates = GetCandidates();
            Selectable current = EnsureSelection(candidates);
            if (current == null)
            {
                return false;
            }

            Navigation navigation = current.navigation;
            if (navigation.mode == Navigation.Mode.Explicit)
            {
                Selectable explicitTarget = ExplicitTarget(navigation, direction);
                if (IsCandidate(explicitTarget, candidates))
                {
                    Select(explicitTarget);
                    return true;
                }
            }

            bool vertical = Mathf.Abs(direction.y) > Mathf.Abs(direction.x);
            List<Selectable> contextualCandidates = NavigationContextCandidates(
                current,
                candidates,
                vertical);
            Selectable best = vertical
                ? FindAdjacentVerticalRow(current, contextualCandidates, direction.y)
                : FindAdjacentInRow(current, contextualCandidates, direction.x);

            if (best == null && contextualCandidates.Count != candidates.Count)
            {
                best = vertical
                    ? FindAdjacentVerticalRow(current, candidates, direction.y)
                    : FindAdjacentInRow(current, candidates, direction.x);
            }

            if (best == null)
            {
                return false;
            }

            Select(best);
            return true;
        }

        private bool Submit()
        {
            List<Selectable> candidates = GetCandidates();
            Selectable current = EnsureSelection(candidates);
            if (current == null || EventSystem.current == null)
            {
                return false;
            }

            BaseEventData eventData = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(current.gameObject, eventData, ExecuteEvents.submitHandler);
            EnsureSelection(GetCandidates());
            return true;
        }

        private static Selectable EnsureSelection(List<Selectable> candidates)
        {
            if (candidates == null || candidates.Count == 0 || EventSystem.current == null)
            {
                return null;
            }

            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
            Selectable selected = selectedObject != null
                ? selectedObject.GetComponent<Selectable>()
                : null;
            if (IsCandidate(selected, candidates))
            {
                return selected;
            }

            selected = TopLeft(candidates);
            Select(selected);
            return selected;
        }

        private List<Selectable> GetCandidates()
        {
            Selectable[] all = Object.FindObjectsOfType<Selectable>(true);
            Transform modal = all
                .Select(candidate => candidate != null ? candidate.transform : null)
                .Select(transform => FindAncestor(transform, "Track Menu"))
                .FirstOrDefault(transform =>
                    transform != null
                    && transform.gameObject.activeInHierarchy
                    && (_scope == null || transform.IsChildOf(_scope))
                    && AllowsInteraction(transform, _scope));

            return all.Where(candidate =>
                    candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.IsInteractable()
                    && candidate.navigation.mode != Navigation.Mode.None
                    && candidate.transform is RectTransform
                    && (_scope == null || candidate.transform.IsChildOf(_scope))
                    && AllowsInteraction(candidate.transform, _scope)
                    && (modal == null || candidate.transform.IsChildOf(modal)))
                .ToList();
        }

        private static bool AllowsInteraction(Transform transform, Transform scope)
        {
            Transform current = transform;
            while (current != null)
            {
                CanvasGroup[] groups = current.GetComponents<CanvasGroup>();
                foreach (CanvasGroup group in groups)
                {
                    if (group != null && !group.interactable)
                    {
                        return false;
                    }
                }

                if (current == scope)
                {
                    break;
                }
                current = current.parent;
            }
            return true;
        }

        private static Transform FindAncestor(Transform transform, string name)
        {
            while (transform != null)
            {
                if (transform.name == name)
                {
                    return transform;
                }
                transform = transform.parent;
            }
            return null;
        }

        private static Selectable TopLeft(IEnumerable<Selectable> candidates)
        {
            List<Selectable> populated = candidates
                .Where(candidate => candidate != null)
                .ToList();
            List<Selectable> visible = populated
                .Where(IsCurrentlyVisible)
                .ToList();
            IEnumerable<Selectable> pool = visible.Count > 0 ? visible : populated;
            return pool
                .OrderByDescending(candidate => ScreenRect(candidate).center.y)
                .ThenBy(candidate => ScreenRect(candidate).center.x)
                .FirstOrDefault();
        }

        private static List<Selectable> NavigationContextCandidates(
            Selectable current,
            List<Selectable> candidates,
            bool vertical)
        {
            FocusScale currentFocus = current != null
                ? current.GetComponent<FocusScale>()
                : null;
            if (currentFocus == null)
            {
                return candidates;
            }

            ScrollRect context = vertical
                ? currentFocus.VerticalScroll
                : currentFocus.HorizontalScroll ?? currentFocus.VerticalScroll;
            if (context == null)
            {
                return candidates;
            }

            List<Selectable> contextual = candidates.Where(candidate =>
                {
                    if (candidate == null)
                    {
                        return false;
                    }

                    FocusScale focus = candidate.GetComponent<FocusScale>();
                    if (focus == null)
                    {
                        return false;
                    }

                    return vertical
                        ? focus.VerticalScroll == context
                        : (currentFocus.HorizontalScroll != null
                            ? focus.HorizontalScroll == context
                            : focus.VerticalScroll == context);
                })
                .ToList();
            return contextual.Contains(current) && contextual.Count > 1
                ? contextual
                : candidates;
        }

        private static Selectable FindAdjacentVerticalRow(
            Selectable current,
            IEnumerable<Selectable> candidates,
            float direction)
        {
            Rect currentRect = ScreenRect(current);
            float verticalDirection = Mathf.Sign(direction);
            List<DirectionalCandidate> rows = candidates
                .Where(candidate => candidate != null && candidate != current)
                .Select(candidate => new DirectionalCandidate(
                    candidate,
                    ScreenRect(candidate),
                    (ScreenRect(candidate).center.y - currentRect.center.y)
                        * verticalDirection))
                .Where(candidate =>
                    candidate.Forward > DirectionEpsilon
                    && !SharesVisualRow(currentRect, candidate.Rect))
                .ToList();
            if (rows.Count == 0)
            {
                return null;
            }

            float nearestForward = rows.Min(candidate => candidate.Forward);
            float rowBand = Mathf.Max(
                10f,
                Mathf.Min(currentRect.height, rows
                    .OrderBy(candidate => candidate.Forward)
                    .First().Rect.height) * 0.35f);
            return rows
                .Where(candidate => candidate.Forward <= nearestForward + rowBand)
                .OrderBy(candidate => HorizontalGap(currentRect, candidate.Rect))
                .ThenBy(candidate =>
                    Mathf.Abs(candidate.Rect.center.x - currentRect.center.x))
                .ThenBy(candidate => candidate.Forward)
                .Select(candidate => candidate.Selectable)
                .FirstOrDefault();
        }

        private static Selectable FindAdjacentInRow(
            Selectable current,
            IEnumerable<Selectable> candidates,
            float direction)
        {
            Rect currentRect = ScreenRect(current);
            float horizontalDirection = Mathf.Sign(direction);
            return candidates
                .Where(candidate => candidate != null && candidate != current)
                .Select(candidate => new DirectionalCandidate(
                    candidate,
                    ScreenRect(candidate),
                    (ScreenRect(candidate).center.x - currentRect.center.x)
                        * horizontalDirection))
                .Where(candidate =>
                    candidate.Forward > DirectionEpsilon
                    && SharesVisualRow(currentRect, candidate.Rect))
                .OrderBy(candidate => HorizontalGap(currentRect, candidate.Rect))
                .ThenBy(candidate => candidate.Forward)
                .ThenBy(candidate =>
                    Mathf.Abs(candidate.Rect.center.y - currentRect.center.y))
                .Select(candidate => candidate.Selectable)
                .FirstOrDefault();
        }

        private static bool SharesVisualRow(Rect first, Rect second)
        {
            float overlap = Mathf.Min(first.yMax, second.yMax)
                - Mathf.Max(first.yMin, second.yMin);
            float minimumHeight = Mathf.Max(1f, Mathf.Min(first.height, second.height));
            return overlap >= minimumHeight * SameRowOverlapRatio;
        }

        private static float HorizontalGap(Rect first, Rect second)
        {
            if (first.xMax < second.xMin)
            {
                return second.xMin - first.xMax;
            }
            if (second.xMax < first.xMin)
            {
                return first.xMin - second.xMax;
            }
            return 0f;
        }

        private static bool IsCurrentlyVisible(Selectable selectable)
        {
            Rect screenRect = ScreenRect(selectable);
            Canvas canvas = selectable.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Rect displayRect = rootCanvas != null && rootCanvas.pixelRect.width > 1f
                && rootCanvas.pixelRect.height > 1f
                    ? rootCanvas.pixelRect
                    : new Rect(
                        0f,
                        0f,
                        Mathf.Max(1f, Screen.width),
                        Mathf.Max(1f, Screen.height));
            if (!screenRect.Overlaps(displayRect, true))
            {
                return false;
            }

            RectMask2D[] masks = selectable.GetComponentsInParent<RectMask2D>(true);
            foreach (RectMask2D mask in masks)
            {
                if (mask != null
                    && mask.isActiveAndEnabled
                    && !screenRect.Overlaps(ScreenRect(mask.rectTransform), true))
                {
                    return false;
                }
            }
            return true;
        }

        private static Vector2 ScreenCenter(Selectable selectable)
        {
            return ScreenRect(selectable).center;
        }

        private static Rect ScreenRect(Selectable selectable)
        {
            return selectable != null
                ? ScreenRect(selectable.transform as RectTransform)
                : default(Rect);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            if (rect == null)
            {
                return default(Rect);
            }

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float xMin = first.x;
            float xMax = first.x;
            float yMin = first.y;
            float yMax = first.y;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private readonly struct DirectionalCandidate
        {
            public readonly Selectable Selectable;
            public readonly Rect Rect;
            public readonly float Forward;

            public DirectionalCandidate(Selectable selectable, Rect rect, float forward)
            {
                Selectable = selectable;
                Rect = rect;
                Forward = forward;
            }
        }

        private static Selectable ExplicitTarget(Navigation navigation, Vector2 direction)
        {
            if (direction == Vector2.up)
            {
                return navigation.selectOnUp;
            }
            if (direction == Vector2.down)
            {
                return navigation.selectOnDown;
            }
            if (direction == Vector2.left)
            {
                return navigation.selectOnLeft;
            }
            return navigation.selectOnRight;
        }

        private static bool IsCandidate(Selectable selectable, ICollection<Selectable> candidates)
        {
            return selectable != null && candidates != null && candidates.Contains(selectable);
        }

        private static void Select(Selectable selectable)
        {
            if (selectable != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }
    }
}
