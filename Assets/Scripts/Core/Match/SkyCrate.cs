using System;
using System.Numerics;

namespace Kitefall.Core
{
    public enum CrateContent { BetterDor, BetterCharkhi, PatchKit, DorRefill }

    [Serializable]
    public class CrateSettings
    {
        /// <summary>Circle within this distance of the crate…</summary>
        public float collectRadius = 16f;
        /// <summary>…for this long to collect it (spec 7.7: 2 s).</summary>
        public float collectTime = 2f;
        public float patchKitDurability = 25f;
        public float dorRefillMeters = 200f;
        /// <summary>Crate disappears if nobody collects it.</summary>
        public float lifetime = 90f;
        public float minHeight = 35f;
        public float maxHeight = 85f;
    }

    /// <summary>A crate hanging from a balloon. Contents are match-only, never added to inventory.</summary>
    public sealed class SkyCrate
    {
        public int Id;
        public Vector3 Position;
        public CrateContent Content;
        public float SpawnTime;
        public bool Taken;
        public int TakenBy = -1;
        /// <summary>Collect progress per kite, seconds.</summary>
        public float[] Progress;

        public static string DisplayName(CrateContent c)
        {
            switch (c)
            {
                case CrateContent.BetterDor: return "Better dor";
                case CrateContent.BetterCharkhi: return "Better charkhi";
                case CrateContent.PatchKit: return "Patch kit +25%";
                default: return "Dor refill +200 m";
            }
        }
    }
}
