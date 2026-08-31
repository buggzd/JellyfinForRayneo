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

        private Vector3 _restScale;
        private float _restDepth;
        private bool _focused;
        private Graphic _targetGraphic;
        private Color _restColor;

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
        }
    }
}
