using Kitefall.Core;

namespace Kitefall.App
{
    /// <summary>What the player asked to play.</summary>
    public sealed class MatchRequest
    {
        public GameMode Mode = GameMode.QuickDuel;
        public ArenaType Arena = ArenaType.OldCityRooftops;
        /// <summary>Total players including you (private room / offline fill).</summary>
        public int Players;
        public BotDifficulty? Difficulty;
        public float MatchMinutes;
        public bool FirstDuel;
        /// <summary>Private room: 1v1 duel instead of free-for-all.</summary>
        public bool PrivateDuel;
        public int Seed;

        public MatchRequest Clone() => (MatchRequest)MemberwiseClone();
    }
}
