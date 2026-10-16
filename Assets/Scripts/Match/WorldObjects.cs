using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>Sky crate hanging from a balloon (spec 7.7).</summary>
    public sealed class CrateView : MonoBehaviour
    {
        private Transform _balloon;
        private LineRenderer _string;
        private Vector3 _base;

        public void Init(in CrateSnap c)
        {
            _base = ArenaBuilder.ToUnity(c.Position);
            Color col = c.Content == CrateContent.BetterDor ? new Color(0.25f, 0.55f, 1f)
                : c.Content == CrateContent.BetterCharkhi ? new Color(1f, 0.55f, 0.15f)
                : c.Content == CrateContent.PatchKit ? new Color(0.25f, 0.85f, 0.4f) : Color.white;
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(transform, false);
            box.transform.localScale = Vector3.one * 1.6f;
            box.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.6f, 0.42f, 0.22f));
            GameObject balloon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(balloon.GetComponent<Collider>());
            balloon.transform.SetParent(transform, false);
            balloon.transform.localPosition = new Vector3(0f, 5f, 0f);
            balloon.transform.localScale = new Vector3(2.6f, 3.2f, 2.6f);
            balloon.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(col, 0.6f);
            _balloon = balloon.transform;
            _string = gameObject.AddComponent<LineRenderer>();
            _string.sharedMaterial = Materials.Unlit();
            _string.widthMultiplier = 0.04f;
            _string.positionCount = 2;
            _string.startColor = _string.endColor = Color.white;
            transform.position = _base;
        }

        private void Update()
        {
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * 1.3f) * 0.6f;
            transform.Rotate(Vector3.up, 20f * Time.deltaTime);
            _string.SetPosition(0, transform.position + Vector3.up * 0.8f);
            _string.SetPosition(1, _balloon.position - Vector3.up * 1.4f);
        }
    }

    /// <summary>The shrinking storm wall in Last Kite Flying.</summary>
    public sealed class StormZoneView : MonoBehaviour
    {
        private float _radius = -1f;

        public void Init()
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(transform, false);
            cyl.transform.localScale = new Vector3(1f, 200f, 1f);
            cyl.transform.localPosition = new Vector3(0f, 200f, 0f);
            var mat = new Material(Materials.Unlit("storm")) { color = new Color(0.55f, 0.25f, 0.9f, 0.18f) };
            cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public void Apply(Vector3 center, float radius)
        {
            transform.position = new Vector3(center.x, 0f, center.z);
            _radius = _radius < 0f ? radius : Mathf.Lerp(_radius, radius, Time.deltaTime * 0.5f);
            transform.localScale = new Vector3(_radius * 2f, 1f, _radius * 2f);
        }
    }

    /// <summary>Contact sparks, snap flash, paper fragments, cut confetti – all particles made in code.</summary>
    public sealed class Effects : MonoBehaviour
    {
        private ParticleSystem _sparks, _paper, _flash, _confetti;

        public void Init()
        {
            _sparks = Make("Sparks", new Color(1f, 0.85f, 0.4f), 0.15f, 8f, 0.35f, true, 0.2f);
            _paper = Make("Paper", new Color(0.95f, 0.9f, 0.8f), 0.35f, 4f, 2.5f, true, 0.5f);
            _flash = Make("Flash", new Color(1f, 1f, 0.9f), 3.5f, 0f, 0.18f, false, 0f);
            _confetti = Make("Confetti", Color.white, 0.25f, 9f, 2.0f, true, 0.35f);
            var main = _confetti.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 1f));
        }

        private ParticleSystem Make(string name, Color color, float size, float speed, float lifetime, bool gravity, float gravityMod)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.gravityModifier = gravity ? gravityMod : 0f;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            Material m = new Material(Materials.Unlit("particle"));
            m.mainTexture = Kitefall.UI.Sprites.Circle.texture;
            r.sharedMaterial = m;
            return ps;
        }

        public void Sparks(Vector3 at, int count = 6) => Emit(_sparks, at, count);
        public void Snap(Vector3 at) { Emit(_flash, at, 1); Emit(_sparks, at, 30); }
        public void Cut(Vector3 at, bool confetti) { Emit(_paper, at, 25); Emit(_flash, at, 1); if (confetti) Emit(_confetti, at, 60); }

        private static void Emit(ParticleSystem ps, Vector3 at, int count)
        {
            var p = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            ps.Emit(p, count);
        }
    }
}
