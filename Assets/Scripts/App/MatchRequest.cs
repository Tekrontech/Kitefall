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
        /// <summary>Private room: host chose fixed roofs (no selection, no hopping – upgrade 10.3).</summary>
        public bool FixedRoofs;

        public MatchRequest Clone() => (MatchRequest)MemberwiseClone();
    }

    /// <summary>Training ground request (upgrade F8): any item can be tried, nothing in the inventory changes.</summary>
    public sealed class TrainingRequest
    {
        public string Kite = "guddi", Tail = "short_ribbon", Dor = "sooti", Charkhi = "lakri";
        public System.Collections.Generic.List<string> Tunings = new System.Collections.Generic.List<string>();
        public int Bots;
        public BotDifficulty Difficulty = BotDifficulty.Medium;
        public bool Dummies = true;
    }
}
