using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Kitefall.Match
{
    /// <summary>
    /// One real 3D character model (from <see cref="CharacterModelLibrary"/>) worn by a <see cref="CharacterView"/>.
    /// Humanoid models (FBX imported as Humanoid – Mixamo clips retarget to every character) play their clips through a small
    /// manually evaluated PlayableGraph; generic models (glTF, legacy clips) are sampled directly. Both cross-fade and need no
    /// Animator controller. On top of the clip, two-bone IK puts the hands where the game wants them (left hand on the line,
    /// right hand on the spool) and the head turns toward the kite. Bones come from the avatar (Humanoid) or Mixamo names.
    /// </summary>
    public sealed class CharacterModel
    {
        public Transform Root { get; private set; }
        public Transform HandL => _handL;
        public Transform HandR => _handR;
        public float Height { get; private set; }

        private GameObject _instance;
        private readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();
        private Transform[] _all;
        private Vector3[] _bufPos;
        private Quaternion[] _bufRot;
        private Transform _hips, _head, _armL, _foreL, _handL, _armR, _foreR, _handR, _midL, _midR;
        private Vector3 _hipsRest;
        private AnimationClip _cur, _prev;
        private float _tCur, _tPrev, _fade = 1f;
        private float _wL, _wR, _wLook;

        // Humanoid playback (null for generic models).
        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _curPlayable, _prevPlayable;
        public bool Humanoid => _animator != null;

        public static CharacterModel TryCreate(string characterId, Transform parent, float scaleHint)
        {
            CharacterModelLibrary.Entry e = CharacterModelLibrary.Find(characterId);
            if (e == null)
            {
                Explain(characterId, CharacterModelLibrary.Instance == null
                    ? "no character library yet – in Unity run Kitefall ▸ Characters ▸ Setup Models (installs glTFast for .glb files)"
                    : "no model file for this id in Assets/Art/Characters (name it " + characterId + ".glb or .fbx, then run Setup Models)");
                return null;
            }
            var m = new CharacterModel();
            if (!m.Build(e, parent, scaleHint))
            {
                m.Destroy();
                Explain(characterId, "the model has no Mixamo-style 'Hips' bone or no mesh");
                return null;
            }
            return m;
        }

        private static readonly HashSet<string> Explained = new HashSet<string>();

        /// <summary>Says once per character why the generated body is shown instead of the 3D model.</summary>
        private static void Explain(string id, string why)
        {
            if (Explained.Add(id)) Debug.LogWarning($"[Kitefall] Character '{id}': using the generated body – {why}.");
        }

        private bool Build(CharacterModelLibrary.Entry e, Transform parent, float scaleHint)
        {
            Root = new GameObject("Model").transform;
            Root.SetParent(parent, false);
            _instance = Object.Instantiate(e.model, Root, false);
            _instance.name = e.id;
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            // Humanoid FBX: keep its Animator (the avatar retargets Mixamo clips) but drive it ourselves; anything else is sampled
            // by hand, so an imported Animator / Animation must not fight the clips.
            Animator rootAnimator = _instance.GetComponent<Animator>();
            if (rootAnimator != null && rootAnimator.avatar != null && rootAnimator.avatar.isHuman && rootAnimator.avatar.isValid)
            {
                _animator = rootAnimator;
                _animator.runtimeAnimatorController = null;
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _graph = PlayableGraph.Create("Kitefall_" + e.id);
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _mixer = AnimationMixerPlayable.Create(_graph, 2);
                AnimationPlayableOutput.Create(_graph, "Body", _animator).SetSourcePlayable(_mixer);
                _graph.Play();
            }
            foreach (Animator a in _instance.GetComponentsInChildren<Animator>(true))
                if (a != _animator) { a.enabled = false; Object.Destroy(a); }
            foreach (Animation a in _instance.GetComponentsInChildren<Animation>(true)) { a.enabled = false; Object.Destroy(a); }

            for (int i = 0; i < e.clips.Length && i < e.names.Length; i++)
                if (e.clips[i] != null && !string.IsNullOrEmpty(e.names[i]) && !_clips.ContainsKey(e.names[i])) _clips[e.names[i]] = e.clips[i];

            _all = _instance.GetComponentsInChildren<Transform>(true);
            _bufPos = new Vector3[_all.Length];
            _bufRot = new Quaternion[_all.Length];
            Transform upLegL = null, upLegR = null;
            foreach (Transform t in _all)
            {
                switch (BoneName(t.name))
                {
                    case "hips": _hips = t; break;
                    case "head": _head = t; break;
                    case "leftarm": _armL = t; break;
                    case "leftforearm": _foreL = t; break;
                    case "lefthand": _handL = t; break;
                    case "rightarm": _armR = t; break;
                    case "rightforearm": _foreR = t; break;
                    case "righthand": _handR = t; break;
                    case "lefthandmiddle1": _midL = t; break;
                    case "righthandmiddle1": _midR = t; break;
                    case "leftupleg": upLegL = t; break;
                    case "rightupleg": upLegR = t; break;
                }
            }
            if (_animator != null)
            {
                // The avatar knows the bones whatever the artist named them.
                _hips = Bone(HumanBodyBones.Hips, _hips);
                _head = Bone(HumanBodyBones.Head, _head);
                _armL = Bone(HumanBodyBones.LeftUpperArm, _armL);
                _foreL = Bone(HumanBodyBones.LeftLowerArm, _foreL);
                _handL = Bone(HumanBodyBones.LeftHand, _handL);
                _armR = Bone(HumanBodyBones.RightUpperArm, _armR);
                _foreR = Bone(HumanBodyBones.RightLowerArm, _foreR);
                _handR = Bone(HumanBodyBones.RightHand, _handR);
                _midL = Bone(HumanBodyBones.LeftMiddleProximal, _midL);
                _midR = Bone(HumanBodyBones.RightMiddleProximal, _midR);
                upLegL = Bone(HumanBodyBones.LeftUpperLeg, upLegL);
                upLegR = Bone(HumanBodyBones.RightUpperLeg, upLegR);
            }
            if (_hips == null) return false;
            _hipsRest = _hips.localPosition;

            // Face +Z of the character view whatever axis the file was exported with: the hips' left→right gives "right".
            Vector3 right = upLegL != null && upLegR != null ? upLegR.position - upLegL.position
                : _armL != null && _armR != null ? _armR.position - _armL.position : Vector3.right;
            right = Vector3.ProjectOnPlane(Root.InverseTransformDirection(right), Vector3.up);
            if (right.sqrMagnitude > 1e-6f)
            {
                Vector3 fwd = Vector3.Cross(right.normalized, Vector3.up);
                _instance.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, 0f);
            }

            // Size: keep the artist's real height (Sana is 1.68 m); only fix files in the wrong units.
            Bounds b;
            if (!WorldBounds(out b)) return false;
            float h = b.size.y / Mathf.Max(1e-4f, Root.lossyScale.y);
            if (h < 0.8f || h > 2.6f)
            {
                float s = 1.75f * scaleHint / Mathf.Max(1e-4f, h);
                _instance.transform.localScale *= s;
                WorldBounds(out b);
                h = b.size.y / Mathf.Max(1e-4f, Root.lossyScale.y);
            }
            Height = h;
            // Feet on the floor.
            _instance.transform.position += Vector3.up * (Root.position.y - b.min.y);

            foreach (Renderer r in _instance.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            Play("idle", true);
            return true;
        }

        /// <summary>Avatar bone, or the name-matched one when the avatar has no mapping (explicit null check: Unity fake null).</summary>
        private Transform Bone(HumanBodyBones bone, Transform fallback)
        {
            Transform t = _animator.GetBoneTransform(bone);
            return t != null ? t : fallback;
        }

        private bool WorldBounds(out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (Renderer r in _instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>"mixamorig:LeftHand" / "mixamorig_LeftHand" / "LeftHand" → "lefthand".</summary>
        private static string BoneName(string n)
        {
            int i = Mathf.Max(n.LastIndexOf(':'), n.LastIndexOf('|'));
            if (i >= 0) n = n.Substring(i + 1);
            if (n.StartsWith("mixamorig")) n = n.Substring(9).TrimStart('_', '.');
            return n.ToLowerInvariant();
        }

        public bool HasClip(string name) => _clips.ContainsKey(name);

        /// <summary>Switches to a clip (cross-fade). Unknown names fall back to idle.</summary>
        public void Play(string name, bool instant = false)
        {
            if (!_clips.TryGetValue(name, out AnimationClip c) && !_clips.TryGetValue("idle", out c)) return;
            if (c == _cur) return;
            if (_cur != null && !instant) { _prev = _cur; _tPrev = _tCur; _fade = 0f; }
            else { _prev = null; _fade = 1f; }
            _cur = c;
            _tCur = 0f;
            if (_animator == null) return;

            // Humanoid: input 0 = current clip, input 1 = the clip fading out.
            if (_prevPlayable.IsValid()) { _graph.Disconnect(_mixer, 1); _prevPlayable.Destroy(); }
            if (_curPlayable.IsValid())
            {
                _graph.Disconnect(_mixer, 0);
                if (_prev != null) { _prevPlayable = _curPlayable; _graph.Connect(_prevPlayable, 0, _mixer, 1); }
                else _curPlayable.Destroy();
            }
            _curPlayable = AnimationClipPlayable.Create(_graph, c);
            _curPlayable.SetApplyFootIK(true);
            _graph.Connect(_curPlayable, 0, _mixer, 0);
        }

        /// <summary>
        /// One frame: advance and sample the clip, then IK. Targets are world positions; weights 0..1 (smoothed here).
        /// </summary>
        public void Tick(float dt, float speed, Vector3 targetL, float weightL, Vector3 poleL, Vector3 targetR, float weightR, Vector3 poleR,
            Vector3 lookAt, float lookWeight)
        {
            if (_instance == null || _cur == null) return;
            _tCur += dt * speed;
            _tPrev += dt * speed;
            _fade = Mathf.MoveTowards(_fade, 1f, dt / 0.25f);

            if (_animator != null)
            {
                float w = _prev != null && _fade < 1f ? Mathf.SmoothStep(0f, 1f, _fade) : 1f;
                if (w >= 1f && _prevPlayable.IsValid()) { _graph.Disconnect(_mixer, 1); _prevPlayable.Destroy(); _prev = null; }
                _curPlayable.SetTime(Wrap(_cur, _tCur));
                _mixer.SetInputWeight(0, w);
                if (_prevPlayable.IsValid()) { _prevPlayable.SetTime(Wrap(_prev, _tPrev)); _mixer.SetInputWeight(1, 1f - w); }
                _graph.Evaluate(0f); // writes the pose to the bones now, so the IK below works on it
            }
            else if (_prev != null && _fade < 1f)
            {
                _prev.SampleAnimation(_instance, Wrap(_prev, _tPrev));
                for (int i = 0; i < _all.Length; i++) { _bufPos[i] = _all[i].localPosition; _bufRot[i] = _all[i].localRotation; }
                _cur.SampleAnimation(_instance, Wrap(_cur, _tCur));
                float w = Mathf.SmoothStep(0f, 1f, _fade);
                for (int i = 0; i < _all.Length; i++)
                {
                    Transform t = _all[i];
                    t.localPosition = Vector3.Lerp(_bufPos[i], t.localPosition, w);
                    t.localRotation = Quaternion.Slerp(_bufRot[i], t.localRotation, w);
                }
            }
            else
            {
                _prev = null;
                _cur.SampleAnimation(_instance, Wrap(_cur, _tCur));
            }

            // Clips play in place: the game moves the character, so drop any forward drift of the hips.
            Vector3 hp = _hips.localPosition;
            _hips.localPosition = new Vector3(_hipsRest.x, hp.y, _hipsRest.z);

            float k = 1f - Mathf.Exp(-10f * dt);
            _wL = Mathf.Lerp(_wL, weightL, k);
            _wR = Mathf.Lerp(_wR, weightR, k);
            _wLook = Mathf.Lerp(_wLook, lookWeight, k);
            if (_armL != null && _foreL != null && _handL != null) TwoBone(_armL, _foreL, _handL, targetL, poleL, _wL);
            if (_armR != null && _foreR != null && _handR != null) TwoBone(_armR, _foreR, _handR, targetR, poleR, _wR);

            if (_head != null && _wLook > 0.01f)
            {
                Vector3 dir = lookAt - _head.position;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion turn = Quaternion.FromToRotation(Root.forward, dir.normalized);
                    turn = Quaternion.RotateTowards(Quaternion.identity, turn, 45f);
                    _head.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.6f * _wLook) * _head.rotation;
                }
            }
        }

        private static float Wrap(AnimationClip c, float t)
        {
            float len = Mathf.Max(c.length, 1e-3f);
            return c.isLooping || IsLoopName(c.name) ? Mathf.Repeat(t, len) : Mathf.Min(t, len);
        }

        private static bool IsLoopName(string n)
        {
            n = n.ToLowerInvariant();
            return n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("fly") || n.Contains("climb") || n.Contains("dance") || n.Contains("clap") || n.Contains("wave")
                || n.Contains("pull") || n.Contains("release") || n.Contains("reel") || n.Contains("tie") || n.Contains("tug");
        }

        /// <summary>Palm centre of a hand (a bit past the wrist bone), where the line or spool is held.</summary>
        public Vector3 Palm(bool left)
        {
            Transform h = left ? _handL : _handR, m = left ? _midL : _midR;
            if (h == null) return Root.position + Vector3.up;
            return m != null ? Vector3.Lerp(h.position, m.position, 0.85f) : h.position;
        }

        /// <summary>Analytic two-bone IK in world space, blended over the animated pose by <paramref name="w"/>.</summary>
        private static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float w)
        {
            if (w < 0.01f) return;
            Quaternion la0 = a.localRotation, lb0 = b.localRotation;
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float la = (pb - pa).magnitude, lb = (pc - pb).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            float lt = Mathf.Clamp((target - pa).magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);

            // 1. Elbow angle from the law of cosines.
            Vector3 v1 = pa - pb, v2 = pc - pb;
            float cur = Vector3.Angle(v1, v2);
            float want = Mathf.Acos(Mathf.Clamp((la * la + lb * lb - lt * lt) / (2f * la * lb), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(v1, v2);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(target - pa, pole);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
            b.rotation = Quaternion.AngleAxis(want - cur, axis.normalized) * b.rotation;

            // 2. Aim the chain at the target.
            a.rotation = Quaternion.FromToRotation(c.position - pa, target - pa) * a.rotation;

            // 3. Swing the elbow toward the pole around the shoulder→target axis.
            Vector3 along = (target - pa).normalized;
            Vector3 e = Vector3.ProjectOnPlane(b.position - pa, along), p = Vector3.ProjectOnPlane(pole, along);
            if (e.sqrMagnitude > 1e-6f && p.sqrMagnitude > 1e-6f)
                a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(e, p, along), along) * a.rotation;

            if (w < 0.999f)
            {
                a.localRotation = Quaternion.Slerp(la0, a.localRotation, w);
                b.localRotation = Quaternion.Slerp(lb0, b.localRotation, w);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_instance != null) _instance.SetActive(visible);
        }

        public void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null;
            _instance = null;
        }
    }
}
