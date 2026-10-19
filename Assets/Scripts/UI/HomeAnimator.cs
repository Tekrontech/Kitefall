using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// Brings the painted home picture (made from the owner's design) to life: the kite floats and sways on its line from the
    /// flyer's hand, clouds drift, birds cross the sky, the sea sparkles and the sun glows. Everything is placed in the
    /// picture's own 0..1 coordinates (u right, v up), so it stays aligned on every screen shape.
    /// </summary>
    public sealed class HomeAnimator : MonoBehaviour
    {
        // Points measured on the 1600×900 design.
        private static readonly Vector2 Hand = new Vector2(554f / 1600f, 1f - 592f / 900f);
        private static readonly Vector2 KiteCenter = new Vector2(1012.5f / 1600f, 1f - 197.5f / 900f);
        private static readonly Vector2 KiteSize = new Vector2(195f / 1600f, 175f / 900f);
        private static readonly Vector2 KiteAttach = new Vector2(0.10f, 0.61f); // where the line meets the kite, inside the kite sprite
        private static readonly Vector2 Sun = new Vector2(668f / 1600f, 1f - 440f / 900f);

        private RectTransform _rect, _kite, _line, _sunGlow;
        private readonly List<RectTransform> _clouds = new List<RectTransform>();
        private readonly List<float> _cloudSpeed = new List<float>();
        private readonly List<RectTransform> _birds = new List<RectTransform>();
        private readonly List<Image> _sparkles = new List<Image>();
        private readonly List<float> _sparklePhase = new List<float>();
        private float _birdTimer;

        /// <summary>Hand position in picture 0..1 coordinates (the player's own character); null = the painted flyer's hand.</summary>
        public System.Func<Vector2> HandUV;

        public static HomeAnimator Attach(RectTransform picture, Sprite kiteSprite)
        {
            var a = picture.gameObject.AddComponent<HomeAnimator>();
            a.Build(kiteSprite);
            return a;
        }

        private RectTransform At(string name, Vector2 uv, Vector2 size01, Sprite sprite, Color color)
        {
            Image img = UiKit.Image(_rect, color, sprite, name);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = uv;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            img.raycastTarget = false;
            _sizes[rt] = size01;
            return rt;
        }

        private readonly Dictionary<RectTransform, Vector2> _sizes = new Dictionary<RectTransform, Vector2>();

        private void Build(Sprite kiteSprite)
        {
            _rect = (RectTransform)transform;
            var rng = new System.Random(7);

            _sunGlow = At("SunGlow", Sun, new Vector2(0.22f, 0.39f), Sprites.Soft, new Color(1f, 0.85f, 0.45f, 0.45f));

            for (int i = 0; i < 5; i++)
            {
                float v = 0.66f + (float)rng.NextDouble() * 0.28f;
                RectTransform c = At("Cloud", new Vector2((float)rng.NextDouble(), v), new Vector2(0.16f + (float)rng.NextDouble() * 0.12f, 0.07f), Sprites.Soft,
                    new Color(1f, 0.93f, 0.85f, 0.22f + (float)rng.NextDouble() * 0.12f));
                _clouds.Add(c);
                _cloudSpeed.Add(0.004f + (float)rng.NextDouble() * 0.006f);
            }

            for (int i = 0; i < 26; i++)
            {
                Vector2 uv = new Vector2(0.40f + (float)rng.NextDouble() * 0.29f, 0.34f + (float)rng.NextDouble() * 0.13f);
                RectTransform s = At("Sparkle", uv, new Vector2(0.006f, 0.0107f), Sprites.Soft, new Color(1f, 0.97f, 0.85f, 0f));
                _sparkles.Add(s.GetComponent<Image>());
                _sparklePhase.Add((float)rng.NextDouble() * 10f);
            }

            for (int i = 0; i < 3; i++)
            {
                RectTransform b = At("Bird", new Vector2(-0.1f, 0.75f), new Vector2(0.014f, 0.016f), Icons.Get("bird"), new Color(0.15f, 0.12f, 0.12f, 0.75f));
                b.gameObject.SetActive(false);
                _birds.Add(b);
            }

            // Kite line under the kite.
            Image line = UiKit.Image(_rect, new Color(1f, 1f, 1f, 0.8f), Sprites.White, "KiteLine");
            line.raycastTarget = false;
            _line = line.rectTransform;
            _line.anchorMin = _line.anchorMax = Vector2.zero;
            _line.pivot = new Vector2(0f, 0.5f);

            if (kiteSprite != null)
            {
                _kite = At("Kite", KiteCenter, KiteSize, kiteSprite, Color.white);
                _kite.GetComponent<Image>().preserveAspect = true;
            }
            _birdTimer = 2f;
            Update();
        }

        private void Update()
        {
            if (_rect == null) return;
            Vector2 size = _rect.rect.size;
            if (size.x < 1f) return;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            foreach (var kv in _sizes) kv.Key.sizeDelta = Vector2.Scale(kv.Value, size);

            // Sun glow breathes.
            _sunGlow.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 0.9f));

            // Clouds drift right and wrap around.
            for (int i = 0; i < _clouds.Count; i++)
            {
                RectTransform c = _clouds[i];
                Vector2 a = c.anchorMin;
                a.x += _cloudSpeed[i] * dt;
                if (a.x > 1.15f) a.x = -0.15f;
                c.anchorMin = c.anchorMax = a;
            }

            // Sea sparkles twinkle.
            for (int i = 0; i < _sparkles.Count; i++)
            {
                float s = Mathf.Sin(t * 2.3f + _sparklePhase[i]);
                Color col = _sparkles[i].color;
                col.a = Mathf.Pow(Mathf.Max(0f, s), 6f) * 0.9f;
                _sparkles[i].color = col;
            }

            // Birds: a small flock now and then, flapping across the sky.
            _birdTimer -= dt;
            if (_birdTimer <= 0f)
            {
                _birdTimer = 9f + Random.value * 6f;
                float v = 0.68f + Random.value * 0.15f;
                for (int i = 0; i < _birds.Count; i++)
                {
                    _birds[i].gameObject.SetActive(true);
                    _birds[i].anchorMin = _birds[i].anchorMax = new Vector2(-0.05f - i * 0.025f, v + (i % 2) * 0.02f);
                }
            }
            for (int i = 0; i < _birds.Count; i++)
            {
                RectTransform b = _birds[i];
                if (!b.gameObject.activeSelf) continue;
                Vector2 a = b.anchorMin;
                a.x += 0.035f * dt;
                a.y += Mathf.Sin(t * 1.3f + i) * 0.002f * dt * 60f * 0.1f;
                b.anchorMin = b.anchorMax = a;
                b.localScale = new Vector3(1f, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 9f + i)), 1f);
                if (a.x > 1.1f) b.gameObject.SetActive(false);
            }

            // Kite: lazy figure-eight sway with a little roll; the line follows from the flyer's hand.
            if (_kite != null)
            {
                _kite.anchoredPosition = new Vector2(Mathf.Sin(t * 0.55f) * size.x * 0.012f, Mathf.Sin(t * 1.1f + 1f) * size.y * 0.014f);
                _kite.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f) * 6f);
                Vector2 kiteLocal = new Vector2((KiteAttach.x - 0.5f) * _kite.rect.width, (KiteAttach.y - 0.5f) * _kite.rect.height);
                Vector3 world = _kite.TransformPoint(kiteLocal);
                Vector2 end = (Vector2)_rect.InverseTransformPoint(world) + _rect.rect.size * _rect.pivot; // to bottom-left origin
                Vector2 start = Vector2.Scale(HandUV != null ? HandUV() : Hand, size);
                Vector2 d = end - start;
                _line.anchoredPosition = start;
                _line.sizeDelta = new Vector2(d.magnitude, Mathf.Max(1.5f, size.y * 0.0022f));
                _line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
        }
    }
}
