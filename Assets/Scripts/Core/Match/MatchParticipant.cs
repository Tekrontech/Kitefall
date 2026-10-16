namespace Kitefall.Core
{
    /// <summary>Live state of one player (or bot) in a match.</summary>
    public sealed class MatchParticipant
    {
        public int Index;
        public ParticipantSetup Setup;
        public KiteSimulation Kite;
        public ParticipantLoadout Current;
        public int StackIndex;

        public int RoundsWon;
        public int Score;
        public int Cuts;
        public int Assists;
        public int SelfSnaps;
        public int Crashes;
        public int ContestsWon;
        public int ContestsLost;
        public int SweetTicks;
        public int ContestTicks;

        public float RespawnTimer;
        public bool Eliminated;
        public bool Quit;
        public bool Connected = true;
        public float DisconnectedFor;
        public bool RoundForfeitApplied;

        // Launch
        public float LaunchHold;
        public bool LaunchHeldPrev;
        public bool LaunchDone;
        public float LaunchBonus;
        public float LaunchDelayLeft;

        /// <summary>Durability to start the next duel round with (survivor keeps damage).</summary>
        public float NextRoundDurability = -1f;

        /// <summary>Match time of the last contest end against each other kite (assists).</summary>
        public float[] LastContestWith;

        public ParticipantResult Result;

        /// <summary>The kite whose end of flight was already scored (each kite is handled once).</summary>
        public KiteSimulation EndHandledFor;

        public string DisplayName => Setup.displayName;
        public bool IsBot => Setup.isBot;
        public int Team => Setup.team;
        public bool IsFlying => Kite.IsFlying;
    }
}
