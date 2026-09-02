using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    [RequireComponent(typeof(Selectable))]
    public sealed class FocusScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
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
        private ScrollRect _horizontalScroll;
        private ScrollRect _verticalScroll;

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

        public void ConfigureScrollRects(ScrollRect horizontalScroll, ScrollRect verticalScroll)
        {
            _horizontalScroll = horizontalScroll;
            _verticalScroll = verticalScroll;
        }

        private void Update()
        {
            Vector3 targetScale = _focused ? _restScale * FocusedScale : _restScale;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * AnimationSpeed);

            Vector3 position = transform.localPosition;
            float targetDepth = _focused ? _restDepth + LocalDepthOffset : _restDepth;
            position.z = Mathf.Lerp(position.z, targetDepth, Time.unscaledDeltaTime * AnimationSpeed);
            transform.localPosition = position;

            if (_targetGraphic != null)
            {
                Color targetColor = _focused
                    ? Color.Lerp(_restColor, Color.white, 0.16f)
                    : _restColor;
                _targetGraphic.color = Color.Lerp(_targetGraphic.color, targetColor, Time.unscaledDeltaTime * AnimationSpeed);
            }

            if (_focusGraphic != null)
            {
                Color targetFocusColor = _focused ? _focusColor : _restFocusColor;
                _focusGraphic.color = Color.Lerp(
                    _focusGraphic.color,
                    targetFocusColor,
                    Time.unscaledDeltaTime * AnimationSpeed);
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
            SetFocused(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocused(false);
        }

        private void SetFocused(bool focused)
        {
            _focused = focused;
            if (focused)
            {
                EnsureVisible(_horizontalScroll);
                EnsureVisible(_verticalScroll);
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
            scrollRect.content.anchoredPosition = position;
        }
    }

    public sealed class DirectionalFocusNavigator
    {
        private const float DirectionEpsilon = 0.5f;

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
                Selectable match = candidates.FirstOrDefault(candidate =>
                    candidate != null && candidate.gameObject.name == objectName);
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

            Vector2 origin = ScreenCenter(current);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Selectable best = null;
            float bestScore = float.PositiveInfinity;
            foreach (Selectable candidate in candidates)
            {
                if (candidate == current)
                {
                    continue;
                }

                Vector2 delta = ScreenCenter(candidate) - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= DirectionEpsilon)
                {
                    continue;
                }

                float lateral = Mathf.Abs(Vector2.Dot(delta, perpendicular));
                float score = forward
                    + lateral * 2.8f
                    + lateral * lateral / Mathf.Max(forward, 1f);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
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

        private static List<Selectable> GetCandidates()
        {
            Selectable[] all = Object.FindObjectsOfType<Selectable>(true);
            Transform modal = all
                .Select(candidate => candidate != null ? candidate.transform : null)
                .Select(transform => FindAncestor(transform, "Track Menu"))
                .FirstOrDefault(transform => transform != null && transform.gameObject.activeInHierarchy);

            return all.Where(candidate =>
                    candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.IsInteractable()
                    && candidate.navigation.mode != Navigation.Mode.None
                    && candidate.transform is RectTransform
                    && (modal == null || candidate.transform.IsChildOf(modal)))
                .ToList();
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
            return candidates
                .OrderByDescending(candidate => ScreenCenter(candidate).y)
                .ThenBy(candidate => ScreenCenter(candidate).x)
                .FirstOrDefault();
        }

        private static Vector2 ScreenCenter(Selectable selectable)
        {
            RectTransform rect = selectable.transform as RectTransform;
            Vector3 world = rect != null
                ? rect.TransformPoint(rect.rect.center)
                : selectable.transform.position;
            Canvas canvas = selectable.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.WorldToScreenPoint(camera, world);
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
