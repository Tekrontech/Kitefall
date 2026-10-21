using System;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>
    /// Builds every sound in code – original, tiny install size (spec 0: ≤ 150 MB, rule 5: original assets).
    /// These are the fallbacks: a recorded file in Assets/Resources/KitefallAudio/&lt;name&gt; replaces a clip by itself
    /// (AudioService loads it first – names and specs in docs/AUDIO_LIST.md).
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 22050;

        private static AudioClip Make(string name, float seconds, Func<float, int, float> sample, bool loop = false)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, i), -1f, 1f);
            if (loop)
            {
                // Crossfade the ends so the loop has no click.
                int fade = Mathf.Min(n / 8, Rate / 4);
                for (int i = 0; i < fade; i++)
                {
                    float t = i / (float)fade;
                    data[i] = data[i] * t + data[n - fade + i] * (1f - t);
                }
                Array.Resize(ref data, n - fade);
                n = data.Length;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private sealed class Noise
        {
            private uint _s;
            private float _lp, _lp2;
            public Noise(uint seed) { _s = seed; }
            public float White() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFF) / 32768f - 1f; }
            public float Low(float k) { _lp += (White() - _lp) * k; return _lp; }
            public float Low2(float k) { _lp2 += (Low(k) - _lp2) * k; return _lp2; }
        }

        public static AudioClip Wind()
        {
            var n = new Noise(1);
            return Make("wind", 6f, (t, i) =>
            {
                float swell = 0.55f + 0.45f * Mathf.Sin(t * 1.3f) * Mathf.Sin(t * 0.37f + 1f);
                return n.Low2(0.04f) * 3.2f * swell;
            }, true);
        }

        public static AudioClip LineHiss()
        {
            var n = new Noise(2);
            return Make("hiss", 3f, (t, i) => (n.White() - n.Low(0.3f)) * 0.25f * (0.7f + 0.3f * Mathf.Sin(t * 9f)), true);
        }

        public static AudioClip ContestBuzz()
        {
            return Make("buzz", 2f, (t, i) =>
            {
                float f = 110f + 8f * Mathf.Sin(t * 30f);
                float saw = 2f * (t * f - Mathf.Floor(t * f + 0.5f));
                return saw * 0.25f * (0.6f + 0.4f * Mathf.Sin(t * 47f));
            }, true);
        }

        public static AudioClip Snap()
        {
            var n = new Noise(3);
            return Make("snap", 0.35f, (t, i) => n.White() * Mathf.Exp(-t * 22f) * 0.9f + Mathf.Sin(t * 2200f) * Mathf.Exp(-t * 40f) * 0.4f);
        }

        public static AudioClip Cut()
        {
            return Make("cut", 0.9f, (t, i) =>
            {
                float f = Mathf.Lerp(900f, 180f, Mathf.Clamp01(t * 1.4f));
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 4f) * 0.6f;
            });
        }

        public static AudioClip Whoosh()
        {
            var n = new Noise(4);
            return Make("whoosh", 0.7f, (t, i) => n.Low(Mathf.Lerp(0.05f, 0.4f, t / 0.7f)) * Mathf.Sin(Mathf.PI * t / 0.7f) * 2.5f);
        }

        public static AudioClip Gust()
        {
            var n = new Noise(5);
            return Make("gust", 2.5f, (t, i) => n.Low2(0.08f) * 4f * Mathf.Sin(Mathf.PI * t / 2.5f));
        }

        public static AudioClip Click() => Make("click", 0.06f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1400f * t) * Mathf.Exp(-t * 80f) * 0.5f);

        /// <summary>Two rising soft plucks – a "yes / start" button.</summary>
        public static AudioClip Confirm() => Make("confirm", 0.35f, (t, i) => (Pluck(659.25f, t) + Pluck(987.77f, t - 0.08f)) * 0.45f);

        public static AudioClip Spark()
        {
            var n = new Noise(6);
            return Make("spark", 0.25f, (t, i) => (n.White() - n.Low(0.5f)) * Mathf.Exp(-t * 18f) * 0.8f);
        }

        public static AudioClip Stinger(bool win)
        {
            float[] notes = win ? new[] { 523.25f, 659.25f, 783.99f, 1046.5f } : new[] { 392f, 349.23f, 311.13f, 261.63f };
            return Make(win ? "win" : "lose", 1.6f, (t, i) =>
            {
                int k = Mathf.Min(notes.Length - 1, (int)(t / 0.22f));
                float local = t - k * 0.22f;
                return Pluck(notes[k], local) * 0.5f;
            });
        }

        public static AudioClip Crowd()
        {
            var n = new Noise(7);
            return Make("crowd", 2f, (t, i) => n.Low(0.2f) * (1f - t / 2f) * 1.5f * (0.6f + 0.4f * Mathf.Sin(t * 13f)));
        }

        /// <summary>A short rising-falling murmur, the crowd reacting to lines crossing.</summary>
        public static AudioClip CrowdOoh()
        {
            var n = new Noise(8);
            return Make("crowd_ooh", 1.4f, (t, i) => n.Low(0.12f) * Mathf.Sin(Mathf.PI * t / 1.4f) * 1.6f * (0.8f + 0.2f * Mathf.Sin(t * 7f)));
        }

        private static float Pluck(float freq, float t)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t * 5f);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t) + 0.15f * Mathf.Sin(6f * Mathf.PI * freq * t)) * env * 0.6f;
        }

        /// <summary>
        /// A short original loop per arena style: plucked melody over a drone + hand-drum pattern.
        /// Scales: 0 old city (raag-like), 1 coastal (major, syncopated), 2 river (pentatonic), others reuse.
        /// </summary>
        public static AudioClip Music(int style, int seed)
        {
            float[][] scales =
            {
                new[] { 0f, 1f, 4f, 5f, 7f, 8f, 11f, 12f },  // old city
                new[] { 0f, 2f, 4f, 5f, 7f, 9f, 11f, 12f },  // coastal
                new[] { 0f, 2f, 4f, 7f, 9f, 12f, 14f, 16f }, // river / island
                new[] { 0f, 2f, 3f, 5f, 7f, 8f, 10f, 12f },  // mountain
            };
            float[] scale = scales[Mathf.Clamp(style, 0, scales.Length - 1)];
            float root = style == 1 ? 220f : style == 3 ? 196f : 233.08f;
            float bpm = style == 1 ? 118f : 92f;
            float beat = 60f / bpm;
            int bars = 8;
            float length = bars * 4 * beat;
            var rng = new System.Random(seed + style * 1000);
            int steps = bars * 8;
            var melody = new int[steps];
            int degree = 0;
            for (int s = 0; s < steps; s++)
            {
                degree = Mathf.Clamp(degree + rng.Next(-2, 3), 0, scale.Length - 1);
                melody[s] = rng.NextDouble() < 0.3 ? -1 : degree;
            }
            var noise = new Noise((uint)(seed + 11));
            float half = beat / 2f;
            return Make("music_" + style, length, (t, i) =>
            {
                float v = 0f;
                // Drone (tanpura-like root + fifth)
                v += (Mathf.Sin(2f * Mathf.PI * root * 0.5f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * root * 0.75f * t)) * 0.06f;
                // Melody
                int step = (int)(t / half) % steps;
                float local = t - Mathf.Floor(t / half) * half;
                if (melody[step] >= 0) v += Pluck(root * Mathf.Pow(2f, scale[melody[step]] / 12f), local) * 0.35f;
                // Hand drum: low hit on beats 1 & 3, high taps on off-beats (coastal adds syncopation)
                float beatPos = t / beat;
                float inBeat = (beatPos - Mathf.Floor(beatPos)) * beat;
                int beatIndex = (int)beatPos % 4;
                if (beatIndex == 0 || beatIndex == 2) v += Mathf.Sin(2f * Mathf.PI * 80f * inBeat) * Mathf.Exp(-inBeat * 18f) * 0.35f;
                float off = inBeat - beat * 0.5f;
                if (off >= 0f) v += noise.White() * Mathf.Exp(-off * 60f) * 0.12f;
                if (style == 1 && beatIndex == 3)
                {
                    float sync = inBeat - beat * 0.75f;
                    if (sync >= 0f) v += noise.White() * Mathf.Exp(-sync * 60f) * 0.1f;
                }
                return v;
            }, false);
        }
    }
}
