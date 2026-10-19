using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>Small UI animations used by the home screen design: slide-in, breathing, shine sweep, press bounce, bar fill, count-up.</summary>
    public static class UiMotion
    {
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>Slides a panel in from <paramref name="offset"/> with a fade, after <paramref name="delay"/> seconds.</summary>
        public static void SlideIn(Component c, Vector2 offset, float delay, float duration = 0.45f)
        {
            if (c == null) return;
            var s = c.gameObject.AddComponent<SlideIn>();
            s.Offset = offset; s.Delay = delay; s.Duration = duration;
        }

        public static void Bounce(Component c) { if (c != null && c.GetComponent<PressBounce>() == null) c.gameObject.AddComponent<PressBounce>(); }
    }

    public sealed class SlideIn : MonoBehaviour
    {
        public Vector2 Offset;
        public float Delay, Duration = 0.45f;
        private RectTransform _rt;
        private CanvasGroup _group;
        private Vector2 _target;
        private float _t;
        private bool _ready;

        private void Start()
        {
            _rt = (RectTransform)transform;
            _target = _rt.anchoredPosition;
            // Unity components are never C# null when missing ("fake null"), so ?? would not add one – check with == null.
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            Apply(0f);
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((_t - Delay) / Duration);
            Apply(k);
            if (k >= 1f) Destroy(this);
        }

        private void Apply(float k)
        {
            _rt.anchoredPosition = _target + Offset * (1f - UiMotion.EaseOutBack(k));
            _group.alpha = Mathf.Clamp01(k * 1.6f);
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.anchoredPosition = _target;
            if (_group != null) _group.alpha = 1f;
        }
    }

    /// <summary>Gentle scale pulse (PLAY button, selected mode, available chest).</summary>
    public sealed class Breathe : MonoBehaviour
    {
        public float Amount = 0.035f, Speed = 2.2f;
        private float _phase;
        private void Awake() => _phase = Random.value * 6f;
        private void Update() => transform.localScale = Vector3.one * (1f + Amount * Mathf.Sin(Time.unscaledTime * Speed + _phase));
    }

    /// <summary>A soft white band that sweeps across the button every few seconds (needs a RectMask2D on the parent).</summary>
    public sealed class Shine : MonoBehaviour
    {
        public float Every = 3.2f, Duration = 0.75f;
        private RectTransform _rt, _parent;

        public static void Add(RectTransform button)
        {
            if (button.GetComponent<RectMask2D>() == null) button.gameObject.AddComponent<RectMask2D>();
            Image band = UiKit.Image(button, new Color(1f, 1f, 1f, 0.35f), Sprites.White, "Shine");
            band.rectTransform.anchorMin = new Vector2(0f, -0.2f);
            band.rectTransform.anchorMax = new Vector2(0f, 1.2f);
            band.rectTransform.sizeDelta = new Vector2(46f, 0f);
            band.rectTransform.localRotation = Quaternion.Euler(0, 0, -18f);
            band.gameObject.AddComponent<Shine>();
        }

        private void Start() { _rt = (RectTransform)transform; _parent = (RectTransform)transform.parent; }

        private void Update()
        {
            float t = Time.unscaledTime % Every;
            float k = Mathf.Clamp01(t / Duration);
            float w = _parent.rect.width;
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(-80f, w + 80f, k), 0f);
        }
    }

    /// <summary>Squash on press, spring back on release.</summary>
    public sealed class PressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float _target = 1f, _scale = 1f;
        public void OnPointerDown(PointerEventData e) => _target = 0.93f;
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        private void Update()
        {
            if (GetComponent<Breathe>() != null && Mathf.Approximately(_target, 1f) && Mathf.Abs(_scale - 1f) < 0.002f) return;
            _scale = Mathf.Lerp(_scale, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f));
            transform.localScale = Vector3.one * _scale;
        }
    }

    /// <summary>Progress bar fill grows from 0 to its value.</summary>
    public sealed class BarFill : MonoBehaviour
    {
        public float Target, Delay;
        private float _t;
        private RectTransform _rt;
        private void Start() { _rt = (RectTransform)transform; Set(0f); }
        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = UiMotion.EaseOut(Mathf.Clamp01((_t - Delay) / 0.7f));
            Set(Target * k);
            if (k >= 1f) Destroy(this);
        }
        private void Set(float v) => _rt.anchorMax = new Vector2(Mathf.Max(0.001f, v), 1f);
    }

    /// <summary>Number counts up to its value (coins, gems).</summary>
    public sealed class CountUp : MonoBehaviour
    {
        public long Value;
        public string Suffix = "";
        public float Duration = 0.9f;
        private float _t;
        private Text _text;
        private void Start() => _text = GetComponent<Text>();
        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = UiMotion.EaseOut(Mathf.Clamp01(_t / Duration));
            if (_text != null) _text.text = ((long)Mathf.Round(Value * k)).ToString("N0") + Suffix;
            if (k >= 1f) Destroy(this);
        }
    }
}

