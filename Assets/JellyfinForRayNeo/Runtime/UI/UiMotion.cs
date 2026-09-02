using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    /// <summary>
    /// Lightweight, dependency-free motion primitives tuned for the fixed Air 3S display.
    /// Motion uses unscaled time so navigation remains responsive while playback is paused.
    /// </summary>
    public sealed class UiViewMotion : MonoBehaviour
    {
        public float EnterDuration = 0.30f;
        public float ExitDuration = 0.18f;
        public float EnterOffset = 28f;
        public float EnterScale = 0.985f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private Vector3 _restScale;
        private Coroutine _routine;
        private bool _targetVisible = true;

        public bool IsVisible => _targetVisible;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }

            _restPosition = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _restScale = transform.localScale;
        }

        public void RefreshRestState()
        {
            _restPosition = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _restScale = transform.localScale;
        }

        public void SetVisibleImmediately(bool visible)
        {
            StopMotion();
            _targetVisible = visible;
            if (visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            RestoreTransform();
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
            if (!visible && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void Show(float delay = 0f)
        {
            StopMotion();
            _targetVisible = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _group.alpha = 0f;
            _group.interactable = true;
            _group.blocksRaycasts = false;
            if (_rect != null)
            {
                _rect.anchoredPosition = _restPosition + Vector2.down * EnterOffset;
            }
            transform.localScale = _restScale * EnterScale;

            if (gameObject.activeInHierarchy)
            {
                _routine = StartCoroutine(AnimateIn(Mathf.Max(0f, delay)));
            }
            else
            {
                SetVisibleImmediately(true);
            }
        }

        public void Hide()
        {
            if (!_targetVisible && _routine != null)
            {
                return;
            }

            _targetVisible = false;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            StopMotion();
            if (!gameObject.activeInHierarchy || ExitDuration <= 0f)
            {
                SetVisibleImmediately(false);
                return;
            }

            _routine = StartCoroutine(AnimateOut());
        }

        private IEnumerator AnimateIn(float delay)
        {
            if (delay > 0f)
            {
                yield return Wait(delay);
            }

            float elapsed = 0f;
            Vector2 startPosition = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            Vector3 startScale = transform.localScale;
            while (elapsed < EnterDuration)
            {
                if (!_targetVisible)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float amount = EaseOutCubic(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, EnterDuration)));
                _group.alpha = amount;
                if (_rect != null)
                {
                    _rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, _restPosition, amount);
                }
                transform.localScale = Vector3.LerpUnclamped(startScale, _restScale, amount);
                yield return null;
            }

            RestoreTransform();
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            _routine = null;
        }

        private IEnumerator AnimateOut()
        {
            float elapsed = 0f;
            float startAlpha = _group.alpha;
            Vector2 startPosition = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            Vector2 endPosition = _restPosition + Vector2.up * (EnterOffset * 0.35f);
            Vector3 startScale = transform.localScale;
            Vector3 endScale = _restScale * 0.992f;
            while (elapsed < ExitDuration)
            {
                if (_targetVisible)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float amount = EaseInCubic(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, ExitDuration)));
                _group.alpha = Mathf.Lerp(startAlpha, 0f, amount);
                if (_rect != null)
                {
                    _rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, amount);
                }
                transform.localScale = Vector3.LerpUnclamped(startScale, endScale, amount);
                yield return null;
            }

            _routine = null;
            SetVisibleImmediately(false);
        }

        private static IEnumerator Wait(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void StopMotion()
        {
            if (_routine == null)
            {
                return;
            }

            StopCoroutine(_routine);
            _routine = null;
        }

        private void RestoreTransform()
        {
            if (_rect != null)
            {
                _rect.anchoredPosition = _restPosition;
            }
            transform.localScale = _restScale;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float value)
        {
            return value * value * value;
        }
    }

    public sealed class UiItemReveal : MonoBehaviour
    {
        private CanvasGroup _group;
        private Vector3 _restScale;
        private Coroutine _routine;
        private float _delay;
        private bool _configured;

        private void Awake()
        {
            _restScale = transform.localScale;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            if (_configured)
            {
                Restart();
            }
        }

        private void OnDisable()
        {
            StopMotion();
            if (_group != null)
            {
                _group.alpha = 1f;
            }
            transform.localScale = _restScale;
        }

        public void Configure(float delay)
        {
            _delay = Mathf.Clamp(delay, 0f, 0.28f);
            _configured = true;
            if (gameObject.activeInHierarchy)
            {
                Restart();
            }
        }

        private void Restart()
        {
            StopMotion();
            _group.alpha = 0f;
            transform.localScale = _restScale * 0.955f;
            _routine = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            float waited = 0f;
            while (waited < _delay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            const float duration = 0.30f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float amount = 1f - Mathf.Pow(1f - normalized, 3f);
                _group.alpha = amount;
                transform.localScale = Vector3.LerpUnclamped(startScale, _restScale, amount);
                yield return null;
            }

            _group.alpha = 1f;
            transform.localScale = _restScale;
            _routine = null;
        }

        private void StopMotion()
        {
            if (_routine == null)
            {
                return;
            }

            StopCoroutine(_routine);
            _routine = null;
        }
    }

    public sealed class UiAmbientFloat : MonoBehaviour
    {
        public Vector2 Amplitude = new Vector2(22f, 12f);
        public float Speed = 0.16f;
        public float Phase;
        public float ScalePulse = 0.035f;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private Vector3 _restScale;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _restPosition = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _restScale = transform.localScale;
        }

        private void Update()
        {
            float phase = Time.unscaledTime * Speed * Mathf.PI * 2f + Phase;
            if (_rect != null)
            {
                _rect.anchoredPosition = _restPosition + new Vector2(
                    Mathf.Sin(phase) * Amplitude.x,
                    Mathf.Cos(phase * 0.73f) * Amplitude.y);
            }

            float pulse = 1f + Mathf.Sin(phase * 0.61f) * ScalePulse;
            transform.localScale = _restScale * pulse;
        }
    }

    /// <summary>
    /// Adds a restrained, long-cycle scale drift to full-bleed hero artwork.
    /// The amplitude stays deliberately small for a comfortable fixed glasses display.
    /// </summary>
    public sealed class UiHeroBreath : MonoBehaviour
    {
        [Range(0f, 0.03f)]
        public float ScaleAmplitude = 0.012f;

        [Range(8f, 40f)]
        public float CycleSeconds = 20f;

        private Vector3 _restScale;
        private float _phase;

        private void Awake()
        {
            _restScale = transform.localScale;
            _phase = Mathf.Abs(GetInstanceID() % 997) / 997f * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            transform.localScale = _restScale;
        }

        private void OnDisable()
        {
            transform.localScale = _restScale;
        }

        private void Update()
        {
            float cycle = Mathf.Max(1f, CycleSeconds);
            float wave = Mathf.Sin(Time.unscaledTime / cycle * Mathf.PI * 2f + _phase);
            float amount = (wave + 1f) * 0.5f;
            float scale = 1f + Mathf.SmoothStep(0f, ScaleAmplitude, amount);
            transform.localScale = _restScale * scale;
        }
    }

    /// <summary>
    /// Defers a section reveal until it reaches the visible scroll viewport.
    /// Interactivity remains enabled so remote focus can still scroll toward it.
    /// </summary>
    public sealed class UiScrollReveal : MonoBehaviour
    {
        public float Duration = 0.34f;
        public float StartScale = 0.978f;
        public float VisibilityPadding = 42f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private ScrollRect _scroll;
        private Vector3 _restScale;
        private Coroutine _routine;
        private float _delay;
        private bool _configured;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _restScale = transform.localScale;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            if (_configured)
            {
                Restart();
            }
        }

        private void OnDisable()
        {
            StopMotion();
            Restore();
        }

        public void Configure(ScrollRect scrollRect, float delay)
        {
            _scroll = scrollRect;
            _delay = Mathf.Clamp(delay, 0f, 0.24f);
            _configured = true;
            if (gameObject.activeInHierarchy)
            {
                Restart();
            }
        }

        private void Restart()
        {
            StopMotion();
            _group.alpha = 0f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            transform.localScale = _restScale * StartScale;
            _routine = StartCoroutine(RevealWhenVisible());
        }

        private IEnumerator RevealWhenVisible()
        {
            yield return null;
            while (!IntersectsViewport())
            {
                yield return null;
            }

            float waited = 0f;
            while (waited < _delay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            while (elapsed < Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, Duration));
                float amount = 1f - Mathf.Pow(1f - normalized, 3f);
                _group.alpha = amount;
                transform.localScale = Vector3.LerpUnclamped(startScale, _restScale, amount);
                yield return null;
            }

            Restore();
            _routine = null;
        }

        private bool IntersectsViewport()
        {
            if (_rect == null || _scroll == null || _scroll.viewport == null)
            {
                return true;
            }

            RectTransform viewport = _scroll.viewport;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewport,
                _rect);
            Rect rect = viewport.rect;
            float padding = Mathf.Max(0f, VisibilityPadding);
            return bounds.max.x >= rect.xMin - padding
                && bounds.min.x <= rect.xMax + padding
                && bounds.max.y >= rect.yMin - padding
                && bounds.min.y <= rect.yMax + padding;
        }

        private void StopMotion()
        {
            if (_routine == null)
            {
                return;
            }
            StopCoroutine(_routine);
            _routine = null;
        }

        private void Restore()
        {
            if (_group != null)
            {
                _group.alpha = 1f;
            }
            transform.localScale = _restScale;
        }
    }

    public sealed class SmoothScrollMotion : MonoBehaviour
    {
        private ScrollRect _scroll;
        private Vector2 _target;
        private Vector2 _velocity;
        private bool _moving;

        private void Awake()
        {
            _scroll = GetComponent<ScrollRect>();
        }

        public void ScrollTo(Vector2 target)
        {
            if (_scroll == null || _scroll.content == null)
            {
                return;
            }

            _target = target;
            _scroll.StopMovement();
            _moving = true;
        }

        private void Update()
        {
            if (!_moving || _scroll == null || _scroll.content == null)
            {
                return;
            }

            Vector2 current = _scroll.content.anchoredPosition;
            Vector2 next = Vector2.SmoothDamp(
                current,
                _target,
                ref _velocity,
                0.10f,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            _scroll.content.anchoredPosition = next;
            if ((next - _target).sqrMagnitude < 0.25f)
            {
                _scroll.content.anchoredPosition = _target;
                _velocity = Vector2.zero;
                _moving = false;
            }
        }
    }

    public sealed class UiLoadingPulse : MonoBehaviour
    {
        private Graphic[] _dots;
        private Color[] _baseColors;

        private void Awake()
        {
            _dots = GetComponentsInChildren<Graphic>(true);
            _baseColors = new Color[_dots.Length];
            for (int index = 0; index < _dots.Length; index++)
            {
                _baseColors[index] = _dots[index].color;
            }
        }

        private void Update()
        {
            for (int index = 0; index < _dots.Length; index++)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * 6.4f - index * 0.82f) + 1f) * 0.5f;
                Color color = _baseColors[index];
                color.a *= Mathf.Lerp(0.28f, 1f, wave);
                _dots[index].color = color;
                _dots[index].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.08f, wave);
            }
        }
    }
}
