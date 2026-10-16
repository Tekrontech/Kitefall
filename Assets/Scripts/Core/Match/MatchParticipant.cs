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

        // ---- Upgrade v1.4: roofs, ritual, social
        public FlyerState Flyer = FlyerState.OnRoof;
        public int RoofId = -1;
        public int RoofSlot;
        public LaunchPrepController Prep;
        public PrepGrade LastKanni = PrepGrade.Good;
        public float PackUpLeft;
        public float PackUpTotal;
        public float GroundedTime;
        public int TraverseLink = -1;
        public float TraverseLeft;
        public float TraverseTotal;
        public System.Numerics.Vector3 CharacterPosition;
        public float CharacterYaw;
        public float CharacterSpeed;
        public byte LastCommandSeq;
        public bool CommandSeen;
        public SocialLimiter Social = new SocialLimiter();
        public float UpwindBonusUntil = -1f;
        public int HelpedBy = -1;
        public float BotRoofThink;
        public float BotLaunchAt = -1f;
        public int BotHopTarget = -1;

        public string DisplayName => Setup.displayName;
        public bool IsBot => Setup.isBot;
        public int Team => Setup.team;
        public bool IsFlying => Kite.IsFlying;
        public bool IsGrounded => Flyer == FlyerState.Grounded || Flyer == FlyerState.Traversing;
    }

    /// <summary>What the flyer (the character) is doing – drives the character animation on every client.</summary>
    public enum FlyerState : byte
    {
        /// <summary>Standing on the roof, kite not launched (intro, classic launch, respawn wait).</summary>
        OnRoof,
        /// <summary>Walking around the waiting plaza (Warmup).</summary>
        Walking,
        /// <summary>Launch ritual (full or short).</summary>
        Prep,
        Flying,
        /// <summary>Fast reel to bring the kite down before changing roof (kite can still be contacted).</summary>
        PackingUp,
        /// <summary>Kite in hand on a roof (can walk to another roof or launch here).</summary>
        Grounded,
        /// <summary>Crossing a plank / bridge / ladder / stairs / jump (server-timed).</summary>
        Traversing,
        /// <summary>Out (cut and waiting, eliminated).</summary>
        Out
    }
}
