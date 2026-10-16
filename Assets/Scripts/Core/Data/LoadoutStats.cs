namespace Kitefall.Core
{
    /// <summary>
    /// The final numbers a kite flies with after combining kite class + dum + dor + charkhi.
    /// This is the only place where loadout modifiers are stacked, so the rules are easy to audit.
    /// (Tuning modifiers from spec 7.4 will be added here in milestone 12.)
    /// </summary>
    public readonly struct LoadoutStats
    {
        public readonly float SpeedStat;
        public readonly float Stability;
        public readonly float TurnRate;
        public readonly int SweetZone;
        public readonly float ClassBase;

        public readonly int Strength;
        public readonly float TensionRiseMult;
        public readonly float PullTensionRiseMult;
        public readonly float ReleaseTensionDropMult;

        public readonly int CapacityMeters;
        public readonly float ReelSpeed;
        public readonly float ReleaseSpeed;

        public LoadoutStats(KiteClassStats kite, TailStats tail, DorStats dor, CharkhiStats charkhi)
        {
            SpeedStat = kite.speed;
            ClassBase = kite.classBase;
            // Stability can never fall below a small positive value (avoids divide-by-zero in wobble).
            Stability = KMath.Max(0.5f, kite.stability + tail.stabilityMod);
            // Percent modifiers multiply: "None" dum (+15%) on Reshmi dor (+5%) = ×1.15 × 1.05.
            TurnRate = KMath.Max(1f, kite.turnRate * (1f + tail.turnMod) * (1f + dor.agilityMod));
            SweetZone = System.Math.Max(1, kite.sweetZoneBase + tail.sweetZoneMod + dor.sweetZoneMod);

            Strength = dor.strength;
            TensionRiseMult = dor.tensionRiseMult <= 0f ? 1f : dor.tensionRiseMult;
            PullTensionRiseMult = charkhi.pullTensionRiseMult <= 0f ? 1f : charkhi.pullTensionRiseMult;
            ReleaseTensionDropMult = charkhi.releaseTensionDropMult <= 0f ? 1f : charkhi.releaseTensionDropMult;

            CapacityMeters = charkhi.capacityMeters;
            ReelSpeed = charkhi.reelSpeed;
            ReleaseSpeed = charkhi.releaseSpeed;
        }
    }
}
