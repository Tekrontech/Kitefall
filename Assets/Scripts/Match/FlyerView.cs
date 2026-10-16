using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>The player character on the rooftop: pull/release hands, spinning charkhi, win/lose reactions (spec 8.4).</summary>
    public sealed class FlyerView : MonoBehaviour
    {
        private Transform _body, _head, _armL, _armR, _charkhi;
        private Vector3 _anchor;
        private float _spin;
        private float _react;
        private int _reaction; // 1 win, -1 lose

        public void Init(Vector3 anchor, Color shirt)
        {
            _anchor = anchor;
            transform.position = anchor;
            _body = Part(PrimitiveType.Capsule, new Vector3(-0.35f, -0.5f, -0.35f), new Vector3(0.55f, 0.75f, 0.55f), Materials.Lit(shirt));
            _head = Part(PrimitiveType.Sphere, new Vector3(-0.35f, 0.42f, -0.35f), Vector3.one * 0.36f, Materials.Lit(new Color(0.78f, 0.58f, 0.42f)));
            _armL = Part(PrimitiveType.Cube, new Vector3(-0.15f, -0.05f, -0.1f), new Vector3(0.1f, 0.1f, 0.6f), Materials.Lit(shirt));
            _armR = Part(PrimitiveType.Cube, new Vector3(-0.5f, -0.05f, -0.1f), new Vector3(0.1f, 0.1f, 0.6f), Materials.Lit(shirt));
            _charkhi = Part(PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 0f), new Vector3(0.28f, 0.08f, 0.28f), Materials.Lit(new Color(0.7f, 0.45f, 0.2f)));
            _charkhi.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private Transform Part(PrimitiveType type, Vector3 local, Vector3 scale, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        public void React(bool win)
        {
            _reaction = win ? 1 : -1;
            _react = 2.5f;
        }

        public void Apply(in KiteSnap s, Vector3 kitePos, float dt)
        {
            // Face the kite (yaw only).
            Vector3 to = kitePos - _anchor;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            float pull = s.Action == LineAction.Pull ? 1f : 0f;
            float armZ = Mathf.Lerp(0.15f, -0.15f, pull);
            _armL.localPosition = new Vector3(-0.15f, -0.05f + pull * 0.1f, armZ);
            _armR.localPosition = new Vector3(-0.5f, -0.05f + pull * 0.1f, armZ);

            float spinSpeed = s.Action == LineAction.Reel ? 900f : s.Action == LineAction.Dheel ? -700f : 0f;
            _spin += spinSpeed * dt;
            _charkhi.localRotation = Quaternion.Euler(_spin, 0f, 90f);

            if (_react > 0f)
            {
                _react -= dt;
                float y = _reaction > 0 ? Mathf.Abs(Mathf.Sin(_react * 8f)) * 0.35f : -0.15f;
                transform.position = _anchor + Vector3.up * y;
                _head.localPosition = new Vector3(-0.35f, _reaction > 0 ? 0.42f : 0.3f, -0.35f);
            }
            else
            {
                transform.position = _anchor;
                _head.localPosition = new Vector3(-0.35f, 0.42f, -0.35f);
            }
        }
    }
}
