namespace Kitefall.Core
{
    /// <summary>
    /// The final numbers a kite flies and fights with after combining
    /// kite class + dum + dor + charkhi + tunings (spec 7, 8).
    /// This is the only place where loadout modifiers are stacked, so the rules are easy to audit.
    /// </summary>
    public readonly struct LoadoutStats
    {
        // Flight
        public readonly float SpeedStat;
        public readonly float Stability;
        public readonly float TurnRate;
        public readonly int SweetZone;

        // Contest
        public readonly float ClassBase;
        public readonly float DorCut;
        public readonly float DorSpeed;
        public readonly float DheelCounterBonus;
        public readonly float LongRangeAngleBonus;
        public readonly float KheenchPowerMult;
        public readonly int DheelTensionFloor;
        public readonly float WearMult;

        // Tension
        public readonly int Strength;
        public readonly float TensionRiseMult;
        public readonly float PullTensionRiseMult;
        public readonly float ReleaseTensionDropMult;

        // Charkhi
        public readonly int CapacityMeters;
        public readonly float ReelSpeed;
        public readonly float ReleaseSpeed;
        public readonly float LaunchDelay;

        public LoadoutStats(KiteClassStats kite, TailStats tail, DorStats dor, CharkhiStats charkhi)
            : this(kite, tail, dor, charkhi, null)
        {
        }

        public LoadoutStats(KiteClassStats kite, TailStats tail, DorStats dor, CharkhiStats charkhi, TuningStats[] tunings)
        {
            // Tuning totals.
            float dorSpeedPct = 0f, agilityPct = 0f, dorCutPct = 0f, reelPct = 0f, kheenchPct = 0f;
            int strengthAbs = 0, capacityAbs = 0, sweetAbs = 0;
            if (tunings != null)
            {
                foreach (TuningStats t in tunings)
                {
                    Accumulate(t.plus, ref dorSpeedPct, ref agilityPct, ref dorCutPct, ref reelPct, ref kheenchPct, ref strengthAbs, ref capacityAbs, ref sweetAbs);
                    Accumulate(t.minus, ref dorSpeedPct, ref agilityPct, ref dorCutPct, ref reelPct, ref kheenchPct, ref strengthAbs, ref capacityAbs, ref sweetAbs);
                }
            }

            SpeedStat = kite.speed;
            ClassBase = kite.classBase <= 0f ? 1f : kite.classBase;
            // Stability can never fall below a small positive value (avoids divide-by-zero in wobble).
            Stability = KMath.Max(0.5f, kite.stability + tail.stabilityMod);
            // Percent modifiers multiply: "None" dum (+15%) on Reshmi dor (+5%) = ×1.15 × 1.05.
            TurnRate = KMath.Max(1f, kite.turnRate * (1f + tail.turnMod) * (1f + dor.agilityMod) * (1f + agilityPct));
            SweetZone = System.Math.Max(1, kite.sweetZoneBase + tail.sweetZoneMod + dor.sweetZoneMod + sweetAbs);

            DorCut = (dor.dorCut <= 0f ? 1f : dor.dorCut) * (1f + dorCutPct);
            DorSpeed = (dor.dorSpeed <= 0f ? 1f : dor.dorSpeed) * (1f + dorSpeedPct);
            DheelCounterBonus = dor.dheelCounterBonus <= 0f ? 1.15f : dor.dheelCounterBonus;
            LongRangeAngleBonus = dor.longRangeAngleBonus <= 0f ? 1.10f : dor.longRangeAngleBonus;
            KheenchPowerMult = (charkhi.kheenchPowerMult <= 0f ? 1f : charkhi.kheenchPowerMult) * (1f + kheenchPct);
            DheelTensionFloor = charkhi.dheelTensionFloor;
            WearMult = dor.wearMult <= 0f ? 1f : dor.wearMult;

            Strength = System.Math.Min(100, dor.strength + strengthAbs);
            TensionRiseMult = dor.tensionRiseMult <= 0f ? 1f : dor.tensionRiseMult;
            PullTensionRiseMult = charkhi.pullTensionRiseMult <= 0f ? 1f : charkhi.pullTensionRiseMult;
            ReleaseTensionDropMult = charkhi.releaseTensionDropMult <= 0f ? 1f : charkhi.releaseTensionDropMult;

            CapacityMeters = System.Math.Max(10, charkhi.capacityMeters + capacityAbs);
            ReelSpeed = charkhi.reelSpeed * (1f + reelPct);
            ReleaseSpeed = charkhi.releaseSpeed;
            LaunchDelay = charkhi.launchDelay;
        }

        private static void Accumulate(StatModifier m, ref float dorSpeed, ref float agility, ref float dorCut, ref float reel,
            ref float kheench, ref int strength, ref int capacity, ref int sweet)
        {
            switch (m.stat)
            {
                case TuningStat.DorSpeed: dorSpeed += m.value; break;
                case TuningStat.Agility: agility += m.value; break;
                case TuningStat.DorCut: dorCut += m.value; break;
                case TuningStat.ReelSpeed: reel += m.value; break;
                case TuningStat.KheenchPower: kheench += m.value; break;
                case TuningStat.Strength: strength += (int)System.Math.Round(m.value); break;
                case TuningStat.Capacity: capacity += (int)System.Math.Round(m.value); break;
                case TuningStat.SweetZone: sweet += (int)System.Math.Round(m.value); break;
            }
        }
    }
}
