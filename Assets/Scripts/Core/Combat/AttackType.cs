namespace Kitefall.Core
{
    /// <summary>Attack used in a cut contest (spec 9.4). Hold = no input in the lock window.</summary>
    public enum AttackType
    {
        Hold,
        Kheench,
        Dheel,
        Dive
    }

    public static class AttackRules
    {
        /// <summary>Counter triangle: Kheench &gt; Dive, Dive &gt; Dheel, Dheel &gt; Kheench.</summary>
        public static bool Beats(AttackType mine, AttackType theirs)
        {
            return (mine == AttackType.Kheench && theirs == AttackType.Dive)
                || (mine == AttackType.Dive && theirs == AttackType.Dheel)
                || (mine == AttackType.Dheel && theirs == AttackType.Kheench);
        }

        /// <summary>The attack that beats <paramref name="theirs"/> (Hold for Hold).</summary>
        public static AttackType CounterOf(AttackType theirs)
        {
            switch (theirs)
            {
                case AttackType.Kheench: return AttackType.Dheel;
                case AttackType.Dheel: return AttackType.Dive;
                case AttackType.Dive: return AttackType.Kheench;
                default: return AttackType.Kheench;
            }
        }

        /// <summary>Counter multiplier for "mine" against "theirs" (1.15 / 0.87 / 1.00; Dheel winner uses the dor's bonus).</summary>
        public static float CounterMultiplier(AttackType mine, AttackType theirs, float counterWin, float counterLose, float dheelCounterBonus)
        {
            if (Beats(mine, theirs)) return mine == AttackType.Dheel ? dheelCounterBonus : counterWin;
            if (Beats(theirs, mine)) return counterLose;
            return 1f;
        }

        public static string DisplayName(AttackType a)
        {
            switch (a)
            {
                case AttackType.Kheench: return "Kheench Cut";
                case AttackType.Dheel: return "Dheel Cut";
                case AttackType.Dive: return "Dive Cut";
                default: return "Hold";
            }
        }
    }
}
