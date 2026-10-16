using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// The chhudaai wala (upgrade spec 11.3): for solo players an NPC helper in a festival outfit appears on the roof,
    /// walks back 10–15 m holding the kite up, tosses it on the launcher's release, then walks to the stairwell and leaves.
    /// </summary>
    public sealed class HelperNpc : MonoBehaviour
    {
        private enum Stage { WalkOut, Hold, Toss, Leave, Gone }

        public CharacterView View { get; private set; }
        public bool Done => _stage == Stage.Gone;
        private Stage _stage;
        private Vector3 _pos, _holdSpot, _exit;
        private float _yaw, _stageTime;

        public static HelperNpc Spawn(Transform parent, Vector3 flyerFeet, float windDirDeg, Vector3 roofCenter, Vector2 roofSize, string kiteId, string skinId)
        {
            var npc = new GameObject("Helper").AddComponent<HelperNpc>();
            npc.transform.SetParent(parent, false);
            CharacterBuilder.Look look = CharacterBuilder.LookFor("rookie_m", "festival_lights", new[] { "acc_turban", "", "", "" }, "lakri", kiteId, skinId);
            npc.View = new GameObject("HelperView").AddComponent<CharacterView>();
            npc.View.transform.SetParent(npc.transform, false);
            npc.View.Init(look, "", new Color(1f, 0.85f, 0.4f), false, false);
            npc.View.SuppressPrepKite = true;
            if (npc.View.Rig.Charkhi != null) npc.View.Rig.Charkhi.gameObject.SetActive(false);

            Vector3 fwd = new Vector3(Mathf.Sin(windDirDeg * Mathf.Deg2Rad), 0f, Mathf.Cos(windDirDeg * Mathf.Deg2Rad));
            npc._pos = flyerFeet + Vector3.Cross(Vector3.up, fwd) * 1.2f;
            // Walk back downwind as far as the roof allows (10–15 m, clamped to the roof).
            Vector3 hold = flyerFeet + fwd * 12f;
            hold.x = Mathf.Clamp(hold.x, roofCenter.x - roofSize.x * 0.5f + 0.8f, roofCenter.x + roofSize.x * 0.5f - 0.8f);
            hold.z = Mathf.Clamp(hold.z, roofCenter.z - roofSize.y * 0.5f + 0.8f, roofCenter.z + roofSize.y * 0.5f - 0.8f);
            if (Vector3.Distance(hold, flyerFeet) < 4f) hold = flyerFeet + fwd * 4f;
            hold.y = flyerFeet.y;
            npc._holdSpot = hold;
            npc._exit = new Vector3(roofCenter.x - roofSize.x * 0.5f + 0.6f, flyerFeet.y, roofCenter.z - roofSize.y * 0.5f + 0.6f);
            npc._yaw = windDirDeg;
            return npc;
        }

        public void Toss() { if (_stage < Stage.Toss) { _stage = Stage.Toss; _stageTime = 0f; } }
        public void Leave() { if (_stage < Stage.Leave) { _stage = Stage.Leave; _stageTime = 0f; } }

        public void Tick(float time, float windDirDeg, float dt)
        {
            if (_stage == Stage.Gone) return;
            _stageTime += dt;
            var f = new CharacterFrame { Time = time, WindDirection = windDirDeg, Position = _pos, Yaw = _yaw };
            switch (_stage)
            {
                case Stage.WalkOut:
                {
                    Vector3 d = _holdSpot - _pos;
                    d.y = 0f;
                    if (d.magnitude < 0.2f) { _stage = Stage.Hold; break; }
                    _pos += d.normalized * Mathf.Min(d.magnitude, 1.6f * dt);
                    _yaw = Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg; // walks backwards facing the flyer
                    f.State = FlyerState.Walking;
                    f.Speed = 1.6f;
                    f.Yaw = _yaw;
                    f.Position = _pos;
                    break;
                }
                case Stage.Hold:
                    f.State = FlyerState.Prep;
                    f.PrepStep = PrepStep.Toss;
                    f.WindDirection = windDirDeg + 180f; // faces the flyer, kite raised
                    break;
                case Stage.Toss:
                    f.State = FlyerState.Prep;
                    f.PrepStep = _stageTime < 0.05f ? PrepStep.Toss : PrepStep.Climb;
                    f.WindDirection = windDirDeg + 180f;
                    if (_stageTime > 1.2f) Leave();
                    break;
                case Stage.Leave:
                {
                    Vector3 d = _exit - _pos;
                    d.y = 0f;
                    _pos += d.normalized * Mathf.Min(d.magnitude, 1.8f * dt);
                    _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    f.State = FlyerState.Walking;
                    f.Speed = 1.8f;
                    f.Yaw = _yaw;
                    f.Position = _pos;
                    if (d.magnitude < 0.3f || _stageTime > 8f) { _stage = Stage.Gone; gameObject.SetActive(false); }
                    break;
                }
            }
            View.Apply(f, dt);
            if (_stage == Stage.Toss && _stageTime > 0.05f && View.Rig.HeldKite != null) View.Rig.HeldKite.gameObject.SetActive(false);
        }
    }
}
