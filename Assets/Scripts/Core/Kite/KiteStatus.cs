namespace Kitefall.Core
{
    public enum KiteStatus
    {
        Flying,
        /// <summary>Own line broke from over-tension (spec 9.2).</summary>
        SelfSnapped,
        /// <summary>Hit the ground or a building, or durability reached 0 (spec 6.2, 8.3).</summary>
        Crashed,
        /// <summary>Line cut by an opponent (spec 9.6).</summary>
        Cut,
        /// <summary>On the roof, not launched yet (launch phase, respawn, Bari launch delay).</summary>
        Waiting
    }

    /// <summary>Raw player input for one tick – exactly what a client sends to the server (spec 14.1).</summary>
    public struct KiteInput
    {
        public float SteerX;
        public float SteerY;
        public bool PullHeld;
        public bool ReelHeld;
        public bool DiveHeld;
        /// <summary>One-shot command (upgrade v1.4): roof claim, pack up, traverse, launch here, emote, quick chat, help launch.</summary>
        public PlayerCommand Command;
        public short CommandArg;
        /// <summary>Client increments this per new command; the server runs each sequence number once (inputs repeat).</summary>
        public byte CommandSeq;
    }

    public enum PlayerCommand : byte
    {
        None = 0,
        ClaimRoof = 1,
        PackUp = 2,
        TraverseLink = 3,
        LaunchHere = 4,
        Emote = 5,
        QuickChat = 6,
        HelpLaunch = 7
    }

    /// <summary>What happened during one simulation step.</summary>
    public readonly struct KiteStepResult
    {
        public readonly bool ZoneChanged;
        public readonly bool SelfSnapped;
        public readonly bool Crashed;
        public readonly bool ActionChanged;
        public readonly bool DiveStarted;

        public KiteStepResult(bool zoneChanged, bool selfSnapped, bool crashed, bool actionChanged, bool diveStarted = false)
        {
            ZoneChanged = zoneChanged;
            SelfSnapped = selfSnapped;
            Crashed = crashed;
            ActionChanged = actionChanged;
            DiveStarted = diveStarted;
        }
    }
}
