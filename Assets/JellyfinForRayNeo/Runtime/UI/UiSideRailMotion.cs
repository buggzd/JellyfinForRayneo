using UnityEngine;
using UnityEngine.EventSystems;

namespace JellyfinForRayNeo
{
    /// <summary>
    /// Expands the LUCENT navigation rail only while pointer or remote focus is inside it.
    /// The long ease and restrained opacity mirror the television prototype without
    /// introducing a tweening dependency.
    /// </summary>
    public sealed class UiSideRailMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private const float ClosedWidth = UiTheme.SideRailWidth;
        private const float OpenWidth = UiTheme.SideRailExpandedWidth;

        private RectTransform _rail;
        private RectTransform _backdrop;
        private CanvasGroup _backdropGroup;
        private CanvasGroup[] _labels;
        private bool _pointerInside;
        private float _widthVelocity;

        public bool IsExpanded { get; private set; }

        public void Configure(
            RectTransform backdrop,
            CanvasGroup backdropGroup,
            CanvasGroup[] labels)
        {
            _rail = transform as RectTransform;
            _backdrop = backdrop;
            _backdropGroup = backdropGroup;
            _labels = labels ?? new CanvasGroup[0];
            ApplyImmediate(false);
        }

        private void Awake()
        {
            _rail = transform as RectTransform;
        }

        private void OnEnable()
        {
            ApplyImmediate(false);
        }

        private void Update()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            bool focusInside = selected != null
                && selected.activeInHierarchy
                && selected.transform.IsChildOf(transform);
            bool expand = _pointerInside || focusInside;
            IsExpanded = expand;

            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            if (_rail != null)
            {
                Vector2 size = _rail.sizeDelta;
                size.x = Mathf.SmoothDamp(
                    size.x,
                    expand ? OpenWidth : ClosedWidth,
                    ref _widthVelocity,
                    expand ? 0.14f : 0.19f,
                    Mathf.Infinity,
                    deltaTime);
                _rail.sizeDelta = size;
            }

            if (_backdrop != null)
            {
                Vector2 size = _backdrop.sizeDelta;
                size.x = Mathf.Lerp(
                    size.x,
                    expand ? 650f : 510f,
                    1f - Mathf.Exp(-5.2f * deltaTime));
                _backdrop.sizeDelta = size;
            }

            if (_backdropGroup != null)
            {
                _backdropGroup.alpha = Mathf.MoveTowards(
                    _backdropGroup.alpha,
                    expand ? 1f : 0.42f,
                    deltaTime * 3.1f);
            }

            float labelTarget = expand ? 1f : 0f;
            foreach (CanvasGroup label in _labels ?? new CanvasGroup[0])
            {
                if (label == null)
                {
                    continue;
                }

                label.alpha = Mathf.MoveTowards(
                    label.alpha,
                    labelTarget,
                    deltaTime * (expand ? 5.8f : 7.6f));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
        }

        private void ApplyImmediate(bool expanded)
        {
            IsExpanded = expanded;
            _widthVelocity = 0f;
            if (_rail != null)
            {
                Vector2 size = _rail.sizeDelta;
                size.x = expanded ? OpenWidth : ClosedWidth;
                _rail.sizeDelta = size;
            }
            if (_backdrop != null)
            {
                Vector2 size = _backdrop.sizeDelta;
                size.x = expanded ? 650f : 510f;
                _backdrop.sizeDelta = size;
            }
            if (_backdropGroup != null)
            {
                _backdropGroup.alpha = expanded ? 1f : 0.42f;
            }
            foreach (CanvasGroup label in _labels ?? new CanvasGroup[0])
            {
                if (label != null)
                {
                    label.alpha = expanded ? 1f : 0f;
                }
            }
        }
    }
}
