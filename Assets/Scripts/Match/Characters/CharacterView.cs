using System.Collections.Generic;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>Everything the character needs to animate on one frame (built by the presenter from the snapshot).</summary>
    public struct CharacterFrame
    {
        public FlyerState State;
        public LineAction Action;
        public bool Diving;
        public bool InContest;
        public PrepStep PrepStep;
        public float PrepStepAge;
        public bool PrepHelped;
        public int PrepTaps;
        public int PrepTugs;
        public float PrepTossHold;
        public float NosePhase;
        public Vector3 Position;
        public float Yaw;
        public float Speed;
        public Vector3 KitePosition;
        public bool KiteValid;
        public float WindDirection;
        public RoofLinkKind LinkKind;
        public float TraverseProgress;
        public Vector3 LinkFrom, LinkTo;
        public float Time;
    }

    /// <summary>
    /// A visible, animated humanoid flyer (upgrade W1): generated body + outfit (<see cref="CharacterBuilder"/>), procedural
    /// animation with two-bone IK so the hands stay on the string and the charkhi, head aim at the kite, walk/run blending,
    /// every launch-ritual step, roof-hopping traversal (plank, rope bridge, ladder, stairs, jump), emotes and reactions.
    /// The kite line starts at <see cref="LineAnchor"/> (left hand). Visual only – the server never reads it.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        public CharacterRig Rig { get; private set; }
        public Transform LineAnchor => Rig?.LineAnchor;
        public Transform HeadTop => _nameRoot;
        public bool IsLocal { get; private set; }
        /// <summary>The ritual kite (before launch) – the camera follows it during tunke / climb.</summary>
        public Transform PrepKite => _prepKite;

        private TextMesh _name, _nameShadow, _bubble;
        private Transform _nameRoot, _bubbleRoot;
        private MeshRenderer _bubbleBack;
        private float _bubbleLeft;
        private Transform _ring;
        private readonly List<Renderer> _details = new List<Renderer>();
        private bool _detailsOn = true;

        // Smoothed pose state
        private Vector3 _pos;
        private float _yaw;
        private bool _placed;
        private float _walkPhase;
        private float _hipDrop, _spinePitch, _chestPitch, _spineYaw, _headYaw, _headPitch, _kneel, _jump;
        private Vector3 _handL, _handR, _poleL = new Vector3(-1f, -1f, -0.4f), _poleR = new Vector3(1f, -1f, -0.4f);
        private float _crank;

        // One-shot overlays
        private string _emote = "";
        private float _emoteTime, _emoteDuration;
        private bool _emoteLoop;
        private int _reaction; // 1 win, −1 lose, 2 hit
        private float _reactionTime;
        private int _lastTugs = -1, _lastTaps = -1;
        private float _tugKick, _tapKick;
        private PrepStep _lastStep = PrepStep.Flying;
        private float _throwTime = -9f;

        // Ritual kite (before the real kite is launched)
        private Transform _prepKite;
        private LineRenderer _prepLine;
        private float _prepKiteHeight;

        public void Init(CharacterBuilder.Look look, string displayName, Color teamColor, bool isLocal, bool showName)
        {
            IsLocal = isLocal;
            Rig = CharacterBuilder.Build(transform, look);
            foreach (Renderer r in Rig.Renderers)
            {
                string n = r.name;
                if (n.StartsWith("Eye") || n == "Pupil" || n == "Brow" || n == "Ear" || n == "Nose" || n == "Mouth" || n == "Thumb" || n == "Button" || n == "Cuff" || n == "Curl")
                    _details.Add(r);
            }
            _handL = new Vector3(-0.2f, 0.85f, 0.05f);
            _handR = new Vector3(0.2f, 0.85f, 0.05f);

            // Team ring under the feet (readable from the shoulder camera).
            _ring = MeshKit.Part(transform, "TeamRing", MeshKit.Cached("teamring", () => MeshKit.Torus(0.45f, 0.025f, 28, 4)), Materials.Lit(teamColor, 0.6f), new Vector3(0f, 0.03f, 0f));

            _nameRoot = new GameObject("NameTag").transform;
            _nameRoot.SetParent(transform, false);
            _nameRoot.localPosition = new Vector3(0f, 2.15f * Rig.Scale, 0f);
            _nameShadow = Text(_nameRoot, displayName, new Color(0f, 0f, 0f, 0.7f), new Vector3(0.012f, -0.012f, 0.01f));
            _name = Text(_nameRoot, displayName, Color.Lerp(teamColor, Color.white, 0.35f), Vector3.zero);
            _nameRoot.gameObject.SetActive(showName && !isLocal);

            _bubbleRoot = new GameObject("Bubble").transform;
            _bubbleRoot.SetParent(transform, false);
            _bubbleRoot.localPosition = new Vector3(0f, 2.55f * Rig.Scale, 0f);
            _bubbleBack = MeshKit.Part(_bubbleRoot, "Back", MeshKit.Cached("bubbleq", () => MeshKit.Box(new Vector3(1f, 0.32f, 0.01f))), Materials.Lit(new Color(1f, 1f, 1f), 0.1f), new Vector3(0f, 0f, 0.02f)).GetComponent<MeshRenderer>();
            _bubble = Text(_bubbleRoot, "", new Color(0.1f, 0.1f, 0.12f), Vector3.zero);
            _bubbleRoot.gameObject.SetActive(false);

            var lineGo = new GameObject("PrepLine");
            lineGo.transform.SetParent(transform, false);
            _prepLine = lineGo.AddComponent<LineRenderer>();
            _prepLine.sharedMaterial = Materials.Unlit("prepline");
            _prepLine.widthMultiplier = 0.035f;
            _prepLine.positionCount = 2;
            _prepLine.startColor = _prepLine.endColor = new Color(0.97f, 0.96f, 0.92f, 0.95f);
            _prepLine.enabled = false;
            _prepKite = KiteVisualFactory.Build(look.KiteId ?? "guddi", look.KiteSkin ?? "", transform).transform;
            _prepKite.gameObject.SetActive(false);
        }

        private static TextMesh Text(Transform parent, string text, Color color, Vector3 offset)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (tm.font != null) go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            return tm;
        }

        public void SetNameVisible(bool visible) { if (_nameRoot != null) _nameRoot.gameObject.SetActive(visible && !IsLocal); }

        // ---- Triggers -----------------------------------------------------------

        public void PlayEmote(string emoteId)
        {
            CosmeticItem e = CharacterCatalog.EmoteItem(emoteId);
            if (e == null) return;
            _emote = emoteId;
            _emoteTime = 0f;
            _emoteDuration = Mathf.Max(1f, e.duration);
            _emoteLoop = e.loop;
        }

        public void React(bool win) { _reaction = win ? 1 : -1; _reactionTime = 0f; }
        public void HitReact() { _reaction = 2; _reactionTime = 0f; }

        public void ShowChat(string text, float seconds = 3f)
        {
            _bubble.text = text;
            _bubbleBack.transform.localScale = new Vector3(Mathf.Max(0.6f, text.Length * 0.11f), 1f, 1f);
            _bubbleRoot.gameObject.SetActive(true);
            _bubbleLeft = seconds;
        }

        // ---- Per frame ------------------------------------------------------------

        public void Apply(in CharacterFrame f, float dt)
        {
            if (Rig == null) return;
            dt = Mathf.Max(dt, 1e-4f);
            float k = 1f - Mathf.Exp(-12f * dt);

            // Position & facing
            if (!_placed) { _pos = f.Position; _placed = true; }
            _pos = Vector3.Lerp(_pos, f.Position, f.State == FlyerState.Traversing ? 1f : 1f - Mathf.Exp(-14f * dt));
            float targetYaw = DesiredYaw(f);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-8f * dt));
            transform.SetPositionAndRotation(_pos, Quaternion.Euler(0f, _yaw, 0f));

            // Triggered counters (tugs / kanni taps) → short kicks
            if (_lastTugs >= 0 && f.PrepTugs > _lastTugs) _tugKick = 1f;
            if (_lastTaps >= 0 && f.PrepTaps > _lastTaps) _tapKick = 1f;
            _lastTugs = f.PrepTugs;
            _lastTaps = f.PrepTaps;
            _tugKick = Mathf.MoveTowards(_tugKick, 0f, dt * 5f);
            _tapKick = Mathf.MoveTowards(_tapKick, 0f, dt * 6f);
            if (_lastStep == PrepStep.Toss && f.PrepStep != PrepStep.Toss && f.State == FlyerState.Prep) _throwTime = f.Time;
            _lastStep = f.State == FlyerState.Prep ? f.PrepStep : PrepStep.Flying;

            Pose p = BasePose(f, dt);
            OverlayReactions(ref p, f, dt);

            // Smooth toward the pose
            _hipDrop = Mathf.Lerp(_hipDrop, p.HipDrop, k);
            _kneel = Mathf.Lerp(_kneel, p.Kneel, k);
            _jump = Mathf.Lerp(_jump, p.Jump, 1f - Mathf.Exp(-20f * dt));
            _spinePitch = Mathf.LerpAngle(_spinePitch, p.SpinePitch, k);
            _chestPitch = Mathf.LerpAngle(_chestPitch, p.ChestPitch, k);
            _spineYaw = Mathf.LerpAngle(_spineYaw, p.SpineYaw, k);
            _headYaw = Mathf.LerpAngle(_headYaw, p.HeadYaw, k);
            _headPitch = Mathf.LerpAngle(_headPitch, p.HeadPitch, k);
            float hk = 1f - Mathf.Exp(-(p.FastHands ? 24f : 13f) * dt);
            _handL = Vector3.Lerp(_handL, p.HandL, hk);
            _handR = Vector3.Lerp(_handR, p.HandR, hk);
            _poleL = Vector3.Lerp(_poleL, p.PoleL, k);
            _poleR = Vector3.Lerp(_poleR, p.PoleR, k);

            ApplyBody(p);
            SolveArm(Rig.UpperArmL, Rig.LowerArmL, Rig.HandL, transform.TransformPoint(_handL), transform.TransformDirection(_poleL));
            SolveArm(Rig.UpperArmR, Rig.LowerArmR, Rig.HandR, transform.TransformPoint(_handR), transform.TransformDirection(_poleR));

            // Charkhi spin (reel / dheel / pack up)
            float spin = f.State == FlyerState.PackingUp ? 1300f : f.Action == LineAction.Reel ? 900f : f.Action == LineAction.Dheel ? -650f : 0f;
            if (f.State != FlyerState.Flying && f.State != FlyerState.PackingUp) spin = 0f;
            _crank += spin * dt;
            if (Rig.CharkhiSpool != null) Rig.CharkhiSpool.localRotation = Quaternion.Euler(0f, _crank, 0f);

            PlaceHeldKite(f, p);
            UpdatePrepKite(f, dt);
            FaceCamera(dt);
        }

        private float DesiredYaw(in CharacterFrame f)
        {
            switch (f.State)
            {
                case FlyerState.Flying:
                case FlyerState.PackingUp:
                    if (f.KiteValid)
                    {
                        Vector3 to = f.KitePosition - _pos;
                        to.y = 0f;
                        if (to.sqrMagnitude > 0.5f) return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    }
                    return f.WindDirection;
                case FlyerState.Prep:
                case FlyerState.OnRoof:
                    return f.WindDirection;
                case FlyerState.Traversing:
                {
                    Vector3 d = f.LinkTo - f.LinkFrom;
                    d.y = 0f;
                    return d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : _yaw;
                }
                default:
                    return f.Speed > 0.25f ? f.Yaw : _yaw;
            }
        }

        // ---- Pose synthesis --------------------------------------------------------

        private struct Pose
        {
            public float HipDrop, Kneel, Jump, SpinePitch, ChestPitch, SpineYaw, HeadYaw, HeadPitch;
            public Vector3 HandL, HandR, PoleL, PoleR;
            public float WalkSpeed;
            public bool Run, FastHands, KneelPose, ClimbPose;
            public int HeldKite; // 0 hidden, 1 front, 2 floor, 3 under arm, 4 over head
        }

        private static readonly Vector3 CharkhiHold = new Vector3(0.14f, 1.0f, 0.28f);
        private static readonly Vector3 PoleLDefault = new Vector3(-1f, -1f, -0.4f), PoleRDefault = new Vector3(1f, -1f, -0.4f);

        private Pose BasePose(in CharacterFrame f, float dt)
        {
            var p = new Pose
            {
                HandL = new Vector3(-0.21f, 0.84f, 0.03f),
                HandR = CharkhiHold,
                PoleL = PoleLDefault,
                PoleR = PoleRDefault
            };
            float t = f.Time;
            float breathe = Mathf.Sin(t * 1.6f) * 1.2f;
            p.ChestPitch = breathe;

            switch (f.State)
            {
                case FlyerState.Flying:
                case FlyerState.PackingUp:
                    FlightPose(ref p, f, t);
                    break;
                case FlyerState.Prep:
                    PrepPose(ref p, f, t);
                    break;
                case FlyerState.Grounded:
                case FlyerState.Walking:
                    p.WalkSpeed = f.Speed;
                    p.Run = f.Speed > 3f;
                    if (f.State == FlyerState.Grounded) { p.HeldKite = 3; p.HandL = new Vector3(-0.27f, 1.0f, 0.08f); p.HandR = new Vector3(0.22f, 0.9f, 0.1f); }
                    else SwingArms(ref p, f.Speed, t);
                    break;
                case FlyerState.Traversing:
                    TraversePose(ref p, f, t);
                    break;
                case FlyerState.Out:
                    p.HeadPitch = 22f;
                    p.SpinePitch = 8f;
                    p.HandR = new Vector3(0.22f, 0.86f, 0.05f);
                    break;
                default: // OnRoof: ready stance
                    p.HandL = new Vector3(-0.15f, 1.05f, 0.3f);
                    p.HeadPitch = -6f;
                    break;
            }
            if (p.WalkSpeed > 0.15f) _walkPhase += dt * p.WalkSpeed * (p.Run ? 2.6f : 3.4f);
            return p;
        }

        private void FlightPose(ref Pose p, in CharacterFrame f, float t)
        {
            Vector3 toKite = f.KiteValid ? transform.InverseTransformDirection((f.KitePosition - Rig.UpperArmL.position).normalized) : new Vector3(0f, 0.6f, 0.8f);
            toKite.z = Mathf.Max(0.25f, toKite.z);
            toKite.Normalize();
            Vector3 shoulderL = new Vector3(-0.18f, 1.42f, 0f);
            float reach = 0.42f;
            bool packing = f.State == FlyerState.PackingUp;
            LineAction a = packing ? LineAction.Reel : f.Action;
            switch (a)
            {
                case LineAction.Pull:
                    reach = 0.27f + Mathf.Sin(t * 9f) * 0.035f;
                    p.SpinePitch = -10f;
                    p.ChestPitch -= 6f;
                    p.HipDrop = 0.04f;
                    break;
                case LineAction.Dheel:
                    reach = 0.56f;
                    p.SpinePitch = 6f;
                    break;
                case LineAction.Reel:
                    reach = 0.3f;
                    p.SpinePitch = 3f;
                    float c = t * (packing ? 20f : 14f);
                    p.HandR = CharkhiHold + new Vector3(Mathf.Cos(c) * 0.035f, Mathf.Sin(c) * 0.035f, 0f);
                    p.FastHands = true;
                    break;
            }
            if (f.Diving) { reach = 0.2f; p.SpinePitch = -14f; }
            if (f.InContest) p.HipDrop += 0.05f + Mathf.Abs(Mathf.Sin(t * 13f)) * 0.01f;
            p.HandL = shoulderL + toKite * reach + Vector3.down * 0.08f;
            if (a == LineAction.Reel) p.HandL = Vector3.Lerp(p.HandL, CharkhiHold + new Vector3(-0.14f, 0.05f, 0.06f), 0.5f);
            p.PoleL = new Vector3(-1f, -1.3f, -0.2f);
            AimHeadAt(ref p, f.KitePosition, f.KiteValid);
        }

        private void PrepPose(ref Pose p, in CharacterFrame f, float t)
        {
            float age = f.PrepStepAge;
            switch (f.PrepStep)
            {
                case PrepStep.PickKite:
                {
                    bool down = age < 0.8f;
                    p.HipDrop = down ? 0.28f : 0f;
                    p.SpinePitch = down ? 48f : 0f;
                    p.HeadPitch = down ? 20f : 10f;
                    p.HandL = down ? new Vector3(-0.16f, 0.3f, 0.45f) : new Vector3(-0.2f, 1.18f, 0.42f);
                    p.HandR = down ? new Vector3(0.16f, 0.3f, 0.45f) : new Vector3(0.2f, 1.18f, 0.42f);
                    p.HeldKite = down ? 2 : 1;
                    p.HeadYaw = down ? 0f : Mathf.Sin(t * 1.7f) * 14f; // inspect the sail
                    break;
                }
                case PrepStep.TieKanni:
                    p.KneelPose = true;
                    p.Kneel = 1f;
                    p.HipDrop = 0.42f;
                    p.SpinePitch = 28f;
                    p.HeadPitch = 30f;
                    float w = Mathf.Sin(t * 16f) * 0.02f - _tapKick * 0.08f;
                    p.HandL = new Vector3(-0.08f + w, 0.36f, 0.52f);
                    p.HandR = new Vector3(0.08f - w, 0.36f + _tapKick * 0.06f, 0.52f);
                    p.FastHands = true;
                    p.HeldKite = 2;
                    break;
                case PrepStep.TieLine:
                    p.HandL = new Vector3(-0.04f + Mathf.Sin(t * 11f) * 0.03f, 1.02f, 0.34f);
                    p.HandR = new Vector3(0.06f, 1.0f, 0.32f);
                    p.HeadPitch = 24f;
                    p.HeldKite = f.PrepHelped ? 0 : 3;
                    p.FastHands = true;
                    break;
                case PrepStep.Toss:
                    if (f.PrepHelped || HelperNearby)
                    {
                        // The helper holds the kite downwind; we hold the line and the charkhi.
                        p.HandL = new Vector3(-0.16f, 1.25f, 0.38f - Mathf.Clamp01(f.PrepTossHold) * 0.12f);
                        p.SpinePitch = -4f - f.PrepTossHold * 4f;
                        p.HeadPitch = -8f;
                    }
                    else
                    {
                        float back = Mathf.Clamp01(f.PrepTossHold / 1.4f);
                        p.HandL = new Vector3(-0.12f, 2.0f, 0.18f - back * 0.2f);
                        p.HandR = new Vector3(0.12f, 2.0f, 0.18f - back * 0.2f);
                        p.SpinePitch = -back * 10f;
                        p.HeadPitch = -25f;
                        p.HeldKite = 4;
                    }
                    break;
                case PrepStep.Tunke:
                case PrepStep.Climb:
                {
                    Vector3 kite = PrepKitePosition(f);
                    Vector3 to = transform.InverseTransformDirection((kite - Rig.UpperArmL.position).normalized);
                    float reach = f.PrepStep == PrepStep.Climb ? 0.55f : 0.36f - _tugKick * 0.2f;
                    p.HandL = new Vector3(-0.18f, 1.36f, 0f) + to * reach;
                    p.SpinePitch = -_tugKick * 10f;
                    p.FastHands = _tugKick > 0.01f;
                    AimHeadAt(ref p, kite, true);
                    if (t - _throwTime < 0.45f && !f.PrepHelped) { p.HandL = new Vector3(-0.12f, 1.7f, 0.6f); p.HandR = new Vector3(0.12f, 1.7f, 0.6f); }
                    break;
                }
                case PrepStep.Untangle:
                {
                    float s = Mathf.Sin(t * 9f);
                    p.HandL = new Vector3(-0.2f + s * 0.25f, 1.45f, 0.35f);
                    p.HandR = new Vector3(0.2f + s * 0.25f, 1.3f, 0.3f);
                    p.SpineYaw = s * 12f;
                    p.FastHands = true;
                    AimHeadAt(ref p, PrepKitePosition(f), true);
                    break;
                }
            }
        }

        /// <summary>Set by the presenter while an NPC helper stands with this flyer's kite.</summary>
        public bool HelperNearby { get; set; }

        /// <summary>NPC helpers never show the ritual kite + line (the launcher does).</summary>
        public bool SuppressPrepKite { get; set; }

        private void TraversePose(ref Pose p, in CharacterFrame f, float t)
        {
            switch (f.LinkKind)
            {
                case RoofLinkKind.Plank:
                    p.WalkSpeed = 1.2f;
                    p.HandL = new Vector3(-0.62f, 1.35f, 0.05f);
                    p.HandR = new Vector3(0.62f, 1.35f, 0.05f);
                    p.PoleL = new Vector3(-0.2f, -1f, -1f);
                    p.PoleR = new Vector3(0.2f, -1f, -1f);
                    p.SpineYaw = Mathf.Sin(t * 3f) * 6f; // balance wobble
                    p.HeadPitch = 18f;
                    break;
                case RoofLinkKind.RopeBridge:
                    p.WalkSpeed = 1.6f;
                    p.HandL = new Vector3(-0.42f, 1.15f, 0.12f);
                    p.HandR = new Vector3(0.42f, 1.15f, 0.12f);
                    p.HeadPitch = 12f;
                    break;
                case RoofLinkKind.Ladder:
                {
                    p.ClimbPose = true;
                    float c = Mathf.Sin(t * 6f);
                    p.HandL = new Vector3(-0.16f, 1.62f + c * 0.16f, 0.3f);
                    p.HandR = new Vector3(0.16f, 1.62f - c * 0.16f, 0.3f);
                    p.HeadPitch = -15f;
                    break;
                }
                case RoofLinkKind.Jump:
                {
                    float k = Mathf.InverseLerp(0.25f, 0.75f, f.TraverseProgress);
                    p.Jump = k > 0f && k < 1f ? Mathf.Sin(k * Mathf.PI) * 0.7f : 0f;
                    p.WalkSpeed = k <= 0f || k >= 1f ? 3.5f : 0f;
                    p.Run = true;
                    p.HandL = new Vector3(-0.3f, 1.5f, 0.25f);
                    p.HandR = new Vector3(0.3f, 1.5f, 0.25f);
                    break;
                }
                default: // stairs
                    p.WalkSpeed = 1.8f;
                    SwingArms(ref p, 1.8f, t);
                    break;
            }
            p.HeldKite = f.LinkKind == RoofLinkKind.Ladder ? 0 : 3;
            if (p.HeldKite == 3 && f.LinkKind != RoofLinkKind.Jump && f.LinkKind != RoofLinkKind.Plank && f.LinkKind != RoofLinkKind.RopeBridge) p.HandL = new Vector3(-0.27f, 1.0f, 0.08f);
        }

        private void SwingArms(ref Pose p, float speed, float t)
        {
            float s = Mathf.Sin(_walkPhase) * Mathf.Clamp01(speed / 2f);
            p.HandL = new Vector3(-0.22f, 0.86f + Mathf.Abs(s) * 0.05f, s * 0.2f);
            p.HandR = new Vector3(0.22f, 0.86f + Mathf.Abs(s) * 0.05f, -s * 0.2f);
        }

        private void AimHeadAt(ref Pose p, Vector3 world, bool valid)
        {
            if (!valid) return;
            Vector3 local = transform.InverseTransformPoint(world) - new Vector3(0f, 1.6f, 0f);
            p.HeadYaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg, -60f, 60f);
            float flat = new Vector2(local.x, local.z).magnitude;
            p.HeadPitch = Mathf.Clamp(-Mathf.Atan2(local.y, Mathf.Max(0.1f, flat)) * Mathf.Rad2Deg * 0.7f, -45f, 30f);
        }

        // ---- Emotes & reactions -------------------------------------------------------

        private void OverlayReactions(ref Pose p, in CharacterFrame f, float dt)
        {
            bool flying = f.State == FlyerState.Flying || f.State == FlyerState.PackingUp || f.State == FlyerState.Prep;
            if (!string.IsNullOrEmpty(_emote))
            {
                _emoteTime += dt;
                float e = _emoteTime;
                if (!_emoteLoop && e >= _emoteDuration || _emoteLoop && e >= _emoteDuration * 2f) _emote = "";
                else if (flying)
                {
                    // Upper body only while flying – the hands keep the string (spec F6).
                    p.HeadPitch += Mathf.Sin(e * 8f) * 12f;
                    p.SpineYaw += Mathf.Sin(e * 5f) * 8f;
                }
                else Emote(ref p, _emote, e);
            }

            if (_reaction != 0)
            {
                _reactionTime += dt;
                float r = _reactionTime;
                if (r > (_reaction == 2 ? 0.6f : 2.5f)) _reaction = 0;
                else if (_reaction == 1 && !flying)
                {
                    p.Jump = Mathf.Abs(Mathf.Sin(r * 7f)) * 0.35f;
                    p.HandL = new Vector3(-0.3f, 2.05f, 0.1f);
                    p.HandR = new Vector3(0.3f, 2.05f, 0.1f);
                    p.HeadPitch = -15f;
                }
                else if (_reaction == -1)
                {
                    p.HeadPitch = 30f;
                    p.SpinePitch += 14f;
                    if (!flying) { p.HandL = new Vector3(-0.2f, 0.8f, 0.05f); p.HandR = new Vector3(0.2f, 0.8f, 0.05f); }
                }
                else if (_reaction == 2)
                {
                    float k = Mathf.Sin(Mathf.Clamp01(r / 0.6f) * Mathf.PI);
                    p.SpinePitch -= 16f * k;
                    p.HeadPitch -= 12f * k;
                }
            }
        }

        private void Emote(ref Pose p, string id, float e)
        {
            float s = Mathf.Sin(e * 10f);
            switch (id)
            {
                case "taunt_wave":
                    p.HandR = new Vector3(0.3f + s * 0.12f, 1.85f, 0.12f);
                    p.PoleR = new Vector3(1f, -0.3f, -0.5f);
                    break;
                case "thumbs_up":
                    p.HandR = new Vector3(0.12f, 1.35f, 0.45f);
                    p.HeadPitch = -5f;
                    break;
                case "bo_kata":
                    p.HandL = new Vector3(-0.28f, 2.05f, 0.15f);
                    p.HandR = new Vector3(0.28f, 2.05f, 0.15f);
                    p.Jump = e < 0.6f ? Mathf.Sin(e / 0.6f * Mathf.PI) * 0.35f : 0f;
                    p.HeadPitch = -22f;
                    break;
                case "victory_dance":
                    p.HandL = new Vector3(-0.3f, 1.5f + s * 0.35f, 0.15f);
                    p.HandR = new Vector3(0.3f, 1.5f - s * 0.35f, 0.15f);
                    p.SpineYaw = Mathf.Sin(e * 5f) * 15f;
                    p.WalkSpeed = 1.2f;
                    break;
                case "clap":
                {
                    float c = Mathf.Abs(Mathf.Sin(e * 9f));
                    p.HandL = new Vector3(-0.04f - c * 0.12f, 1.32f, 0.38f);
                    p.HandR = new Vector3(0.04f + c * 0.12f, 1.32f, 0.38f);
                    p.FastHands = true;
                    break;
                }
                case "bow":
                    p.SpinePitch = Mathf.Sin(Mathf.Clamp01(e / 1.8f) * Mathf.PI) * 45f;
                    p.HandR = new Vector3(0.05f, 1.15f, 0.25f);
                    break;
                case "spin":
                    p.SpineYaw = e * 400f % 360f;
                    p.HandL = new Vector3(-0.55f, 1.4f, 0f);
                    p.HandR = new Vector3(0.55f, 1.4f, 0f);
                    break;
                case "laugh":
                    p.HandL = new Vector3(-0.12f, 1.0f, 0.2f);
                    p.HandR = new Vector3(0.12f, 1.0f, 0.2f);
                    p.SpinePitch = Mathf.Abs(Mathf.Sin(e * 12f)) * 12f;
                    p.HeadPitch = -20f;
                    break;
                case "shrug":
                    p.HandL = new Vector3(-0.42f, 1.05f, 0.3f);
                    p.HandR = new Vector3(0.42f, 1.05f, 0.3f);
                    p.HeadPitch = 8f;
                    p.HeadYaw = 10f;
                    break;
                case "dhol_beat":
                {
                    float c = Mathf.Abs(Mathf.Sin(e * 12f));
                    p.HandL = new Vector3(-0.18f, 0.95f + c * 0.12f, 0.35f);
                    p.HandR = new Vector3(0.18f, 1.07f - c * 0.12f, 0.35f);
                    p.HipDrop = c * 0.04f;
                    p.FastHands = true;
                    break;
                }
                case "salute":
                    p.HandR = new Vector3(0.1f, 1.68f, 0.12f);
                    p.PoleR = new Vector3(1f, 0f, 0f);
                    p.HeadPitch = -6f;
                    break;
                case "facepalm":
                    p.HandR = new Vector3(0.02f, 1.62f, 0.14f);
                    p.HeadPitch = 22f;
                    break;
            }
        }

        // ---- Applying the pose --------------------------------------------------------

        private void ApplyBody(in Pose p)
        {
            float lift = _jump;
            Rig.Hips.localPosition = new Vector3(0f, Rig.HipHeight - _hipDrop + lift + (p.WalkSpeed > 0.15f ? Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.035f : 0f), 0f);
            Rig.Hips.localRotation = Quaternion.Euler(0f, _spineYaw * 0.3f, p.WalkSpeed > 0.15f ? Mathf.Sin(_walkPhase) * 3f : 0f);
            Rig.Spine.localRotation = Quaternion.Euler(_spinePitch * 0.5f, _spineYaw * 0.4f, 0f);
            Rig.Chest.localRotation = Quaternion.Euler(_spinePitch * 0.5f + _chestPitch, _spineYaw * 0.3f, 0f);
            Rig.Neck.localRotation = Quaternion.Euler(_headPitch * 0.4f, _headYaw * 0.4f, 0f);
            Rig.Head.localRotation = Quaternion.Euler(_headPitch * 0.6f, _headYaw * 0.6f, 0f);

            // Legs
            float upL, loL, upR, loR, footL = 0f, footR = 0f;
            if (p.KneelPose || _kneel > 0.05f)
            {
                // Right knee on the roof, left foot forward (kanni tying).
                upL = Mathf.Lerp(0f, -95f, _kneel); loL = Mathf.Lerp(0f, 95f, _kneel);
                upR = Mathf.Lerp(0f, -10f, _kneel); loR = Mathf.Lerp(0f, 110f, _kneel);
                footR = Mathf.Lerp(0f, -50f, _kneel);
            }
            else if (p.ClimbPose)
            {
                float c = Mathf.Sin(_walkPhase += Time.deltaTime * 6f);
                upL = -40f - c * 25f; loL = 60f + c * 20f; upR = -40f + c * 25f; loR = 60f - c * 20f;
            }
            else if (p.WalkSpeed > 0.15f)
            {
                float amp = p.Run ? 42f : 26f;
                float s = Mathf.Sin(_walkPhase);
                upL = s * amp; upR = -s * amp;
                loL = Mathf.Max(0f, Mathf.Sin(_walkPhase - 1.2f)) * amp * 1.5f;
                loR = Mathf.Max(0f, Mathf.Sin(_walkPhase + Mathf.PI - 1.2f)) * amp * 1.5f;
                footL = -loL * 0.2f; footR = -loR * 0.2f;
            }
            else if (_jump > 0.05f)
            {
                upL = upR = -50f; loL = loR = 80f;
            }
            else
            {
                // Standing: a soft knee bend that follows the hip drop.
                float bend = Mathf.Clamp01(_hipDrop / 0.3f);
                upL = upR = -bend * 55f;
                loL = loR = bend * 95f;
                footL = footR = -bend * 40f;
            }
            Rig.UpperLegL.localRotation = Quaternion.Euler(upL, 0f, 2f);
            Rig.LowerLegL.localRotation = Quaternion.Euler(loL, 0f, 0f);
            Rig.FootL.localRotation = Quaternion.Euler(footL, 0f, 0f);
            Rig.UpperLegR.localRotation = Quaternion.Euler(upR, 0f, -2f);
            Rig.LowerLegR.localRotation = Quaternion.Euler(loR, 0f, 0f);
            Rig.FootR.localRotation = Quaternion.Euler(footR, 0f, 0f);
        }

        /// <summary>Analytic two-bone IK (shoulder → elbow → hand) with a pole hint for the elbow.</summary>
        private void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole)
        {
            float scale = transform.lossyScale.y * Rig.Scale;
            float a = Rig.UpperArm * scale, b = Rig.LowerArm * scale;
            Vector3 s = upper.position;
            Vector3 toT = target - s;
            float d = Mathf.Clamp(toT.magnitude, 0.02f, a + b - 0.002f);
            Vector3 dir = toT.sqrMagnitude > 1e-6f ? toT.normalized : -transform.up;
            Vector3 poleDir = pole - dir * Vector3.Dot(pole, dir);
            if (poleDir.sqrMagnitude < 1e-6f) poleDir = -transform.forward;
            poleDir.Normalize();
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = s + dir * (a * cosA) + poleDir * (a * sinA);
            Vector3 handPos = s + dir * d;

            Vector3 upDir = (elbow - s).normalized;
            Vector3 loDir = (handPos - elbow).normalized;
            upper.rotation = BoneRotation(upDir, poleDir);
            lower.rotation = BoneRotation(loDir, poleDir);
            hand.rotation = lower.rotation;
        }

        /// <summary>World rotation whose local −Y points along <paramref name="boneDir"/>.</summary>
        private Quaternion BoneRotation(Vector3 boneDir, Vector3 hint)
        {
            Vector3 up = -boneDir;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward - hint * 0.3f, up);
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.ProjectOnPlane(transform.right, up);
            return Quaternion.LookRotation(fwd.normalized, up);
        }

        // ---- Held kite & ritual kite ---------------------------------------------------

        private void PlaceHeldKite(in CharacterFrame f, in Pose p)
        {
            Transform socket = Rig.HeldKiteSocket;
            bool show = p.HeldKite != 0;
            Rig.HeldKite.gameObject.SetActive(show);
            if (!show) return;
            switch (p.HeldKite)
            {
                case 1: // in front, inspecting
                    socket.localPosition = (_handL + _handR) * 0.5f + new Vector3(0f, 0.12f, 0.08f);
                    socket.localRotation = Quaternion.Euler(-10f, 180f + Mathf.Sin(f.Time * 1.7f) * 15f, 0f);
                    break;
                case 2: // flat on the roof
                    socket.localPosition = new Vector3(0f, 0.04f, 0.6f);
                    socket.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case 3: // under the left arm
                    socket.localPosition = new Vector3(-0.32f, 1.05f, 0.05f);
                    socket.localRotation = Quaternion.Euler(0f, 90f, 10f);
                    break;
                case 4: // over the head for a solo toss
                    socket.localPosition = (_handL + _handR) * 0.5f + new Vector3(0f, 0.25f, 0.05f);
                    socket.localRotation = Quaternion.Euler(-20f, 180f, 0f);
                    break;
            }
        }

        private Vector3 PrepKitePosition(in CharacterFrame f)
        {
            float climb = f.PrepStep == PrepStep.Climb ? Mathf.Clamp01(f.PrepStepAge / 1.5f) : 0f;
            float h = 3.5f + f.PrepTugs * 1.4f + climb * 8f;
            float dist = 7f + f.PrepTugs * 1.5f + climb * 10f;
            float swing = f.PrepStep == PrepStep.Untangle ? Mathf.Sin(f.Time * 14f) * 1.2f : Mathf.Sin(f.NosePhase * Mathf.PI * 2f) * 1.1f;
            return transform.position + transform.forward * dist + transform.right * swing + Vector3.up * h;
        }

        private void UpdatePrepKite(in CharacterFrame f, float dt)
        {
            bool show = !SuppressPrepKite && f.State == FlyerState.Prep && (f.PrepStep == PrepStep.Tunke || f.PrepStep == PrepStep.Climb || f.PrepStep == PrepStep.Untangle);
            _prepKite.gameObject.SetActive(show);
            _prepLine.enabled = show;
            if (!show) return;
            Vector3 target = PrepKitePosition(f);
            _prepKite.position = Vector3.Lerp(_prepKite.position, target, 1f - Mathf.Exp(-6f * dt));
            Vector3 dir = (_prepKite.position - LineAnchor.position).normalized;
            float nose = Mathf.Sin(f.NosePhase * Mathf.PI * 2f) * 35f;
            _prepKite.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, nose);
            _prepLine.SetPosition(0, LineAnchor.position);
            _prepLine.SetPosition(1, _prepKite.position);
        }

        private void FaceCamera(float dt)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // Cheap LOD: face details off for other players far away (upgrade 12).
            bool near = IsLocal || Vector3.Distance(cam.transform.position, transform.position) < KitefallGraphics.CharacterDetailDistance;
            if (near != _detailsOn)
            {
                _detailsOn = near;
                foreach (Renderer r in _details) if (r != null) r.enabled = near;
            }
            if (_nameRoot.gameObject.activeSelf)
            {
                _nameRoot.rotation = Quaternion.LookRotation(_nameRoot.position - cam.transform.position, Vector3.up);
                float dist = Vector3.Distance(cam.transform.position, _nameRoot.position);
                _nameRoot.localScale = Vector3.one * Mathf.Clamp(dist * 0.06f, 0.6f, 6f);
            }
            if (_bubbleRoot.gameObject.activeSelf)
            {
                _bubbleLeft -= dt;
                if (_bubbleLeft <= 0f) _bubbleRoot.gameObject.SetActive(false);
                _bubbleRoot.rotation = Quaternion.LookRotation(_bubbleRoot.position - cam.transform.position, Vector3.up);
                float dist = Vector3.Distance(cam.transform.position, _bubbleRoot.position);
                _bubbleRoot.localScale = Vector3.one * Mathf.Clamp(dist * 0.06f, 0.6f, 6f);
            }
        }
    }
}
