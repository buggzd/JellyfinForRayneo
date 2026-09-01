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
}
