using System;

namespace Kitefall.Core
{
    public enum GameMode
    {
        QuickDuel,
        RankedDuel,
        FreeForAll,
        PrivateRoom,
        TeamBattle,
        LastKiteFlying,
        Tutorial
    }

    public enum BotDifficulty { Easy, Medium, Hard, Dummy }

    /// <summary>Rules of one game mode (spec 5, 5.1). Defaults come from <see cref="For"/>.</summary>
    [Serializable]
    public class ModeRules
    {
        public GameMode mode;
        public string displayName;
        public int minPlayers = 2;
        public int maxPlayers = 2;

        // ---- Rounds (duels)
        /// <summary>Rounds needed to win. 0 = one long timed round (FFA, Team, LKF).</summary>
        public int roundsToWin = 2;
        public float roundMaxTime = 180f;
        /// <summary>Safety cap on rounds when there are many draws.</summary>
        public int maxRounds = 9;
        public float loadoutBreakTime;

        // ---- Long modes
        public float matchTime;
        public bool respawn;
        public float respawnDelay = 5f;
        public int kiteStackSize = 1;
        public int scoreCut = 100;
        public int scoreAssist = 40;
        public int scoreSelfSnap = -50;
        public float assistWindow = 3f;
        public float crateInterval;
        public int teamCount;
        public int teamCutsToWin;
        public int captainCutValue = 2;
        public bool stormZone;
        public float stormShrinkInterval = 120f;

        // ---- Phases & timings
        public bool useStorm = true;
        public float introTime = 5f;
        public float launchTime = 5f;
        public float roundEndTime = 4f;

        // ---- Consequences
        /// <summary>Kite count / dor meters are lost when cut (spec 5).</summary>
        public bool lossMode;
        /// <summary>Durability still drops (Quick Duel).</summary>
        public bool durabilityLoss = true;
        public bool ranked;
        public bool allowBots = true;
        /// <summary>Only Sooti + Lakri at start; better gear only from crates (LKF).</summary>
        public bool startWithBasicGear;

        public bool IsDuel => roundsToWin > 0;

        public ModeRules Clone() => (ModeRules)MemberwiseClone();

        public static ModeRules For(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.RankedDuel:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Ranked Duel", minPlayers = 2, maxPlayers = 2,
                        roundsToWin = 3, roundMaxTime = 180f, loadoutBreakTime = 20f,
                        lossMode = true, ranked = true, allowBots = false
                    };
                case GameMode.QuickDuel:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Quick Duel", minPlayers = 2, maxPlayers = 2,
                        roundsToWin = 2, roundMaxTime = 120f, useStorm = false, lossMode = false
                    };
                case GameMode.FreeForAll:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Free-for-All", minPlayers = 2, maxPlayers = 8,
                        roundsToWin = 0, matchTime = 720f, respawn = true, kiteStackSize = 3,
                        crateInterval = 180f, lossMode = true
                    };
                case GameMode.PrivateRoom:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Private Room", minPlayers = 2, maxPlayers = 8,
                        roundsToWin = 0, matchTime = 600f, respawn = true, kiteStackSize = 3,
                        crateInterval = 180f, lossMode = false, durabilityLoss = false
                    };
                case GameMode.TeamBattle:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Team Battle", minPlayers = 2, maxPlayers = 8,
                        roundsToWin = 0, matchTime = 900f, respawn = true, kiteStackSize = 3,
                        teamCount = 2, teamCutsToWin = 25, crateInterval = 180f, lossMode = true
                    };
                case GameMode.LastKiteFlying:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Last Kite Flying", minPlayers = 2, maxPlayers = 20,
                        roundsToWin = 0, matchTime = 1200f, respawn = false, crateInterval = 60f,
                        stormZone = true, startWithBasicGear = true, lossMode = true
                    };
                case GameMode.Tutorial:
                    return new ModeRules
                    {
                        mode = mode, displayName = "Tutorial", minPlayers = 1, maxPlayers = 2,
                        roundsToWin = 0, matchTime = 0f, respawn = true, respawnDelay = 2f, useStorm = false,
                        introTime = 0f, launchTime = 3f, lossMode = false, durabilityLoss = false
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
