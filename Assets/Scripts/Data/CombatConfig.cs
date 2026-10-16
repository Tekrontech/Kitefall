using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>
    /// Global combat numbers (spec 14.6 CombatConfig). Later overridable by Remote Config.
    /// Milestone 1 uses the tension fields; contest fields are used from milestone 3–5.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "Kitefall/Config/Combat Config", order = 20)]
    public sealed class CombatConfig : ScriptableObject
    {
        [Header("Tension (spec 9.1)")]
        public float tensionPullRate = 35f;
        public float tensionReleaseRate = 40f;
        public float tensionIdleTarget = 40f;
        [Tooltip("NOT in spec – points/second of idle drift toward the idle target. See docs/DECISIONS.md.")]
        public float tensionIdleDriftRate = 20f;
        [Tooltip("Spec 9.1: in strong wind tension rises 10% faster.")]
        public float strongWindTensionRiseMult = 1.10f;

        [Header("Tension zones (spec 9.2)")]
        public float safeZoneMax = 60f;
        public float strongZoneMax = 85f;
        public float snapHoldTime = 0.6f;

        [Header("Controls")]
        [Tooltip("Spec 10: releasing PULL = dheel (line feeds out until REEL/PULL). Off = releasing PULL just idles.")]
        public bool dheelOnPullRelease = true;

        [Header("Contact & contest (milestones 3–5)")]
        public float contactDistance = 0.3f;
        public float approachDistance = 15f;
        public float attackLockWindow = 0.3f;
        public float contestMaxTime = 3f;
        public float contestK = 6f;
        public float lateWinThreshold = 40f;
        public float counterWin = 1.15f;
        public float counterLose = 0.87f;
        public float diveBurst = 1.25f;
        public float diveBurstTime = 1f;
        public float noise = 0.5f;
        public float neutralDurabilityLoss = 5f;
        public float neutralCooldown = 1f;
        public float multiContestMult = 0.8f;

        /// <summary>Contest settings: the flat spec fields here + the extra factors from BalanceConfig.</summary>
        public ContestSettings ToContestSettings(ContestSettings extras)
        {
            var s = extras != null ? (ContestSettings)UnityEngine.JsonUtility.FromJson(UnityEngine.JsonUtility.ToJson(extras), typeof(ContestSettings)) : new ContestSettings();
            s.contestK = contestK;
            s.contestMaxTime = contestMaxTime;
            s.lateWinThreshold = lateWinThreshold;
            s.attackLockWindow = attackLockWindow;
            s.counterWin = counterWin;
            s.counterLose = counterLose;
            s.diveBurst = diveBurst;
            s.diveBurstTime = diveBurstTime;
            s.noise = noise;
            s.neutralDurabilityLoss = neutralDurabilityLoss;
            s.multiContestMult = multiContestMult;
            s.redZone = strongZoneMax;
            return s;
        }

        public ContactSettings ToContactSettings(ContactSettings extras)
        {
            var s = extras != null ? (ContactSettings)UnityEngine.JsonUtility.FromJson(UnityEngine.JsonUtility.ToJson(extras), typeof(ContactSettings)) : new ContactSettings();
            s.contactDistance = contactDistance;
            s.approachDistance = approachDistance;
            s.neutralCooldown = neutralCooldown;
            return s;
        }

        public TensionSettings ToTensionSettings() => new TensionSettings
        {
            pullRate = tensionPullRate,
            releaseRate = tensionReleaseRate,
            idleTarget = tensionIdleTarget,
            idleDriftRate = tensionIdleDriftRate,
            strongWindRiseMult = strongWindTensionRiseMult,
            safeZoneMax = safeZoneMax,
            strongZoneMax = strongZoneMax,
            snapHoldTime = snapHoldTime
        };
    }
}
