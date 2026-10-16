namespace Kitefall.Core
{
    /// <summary>The six reference builds (spec 7.5) as item ids.</summary>
    public enum BuildArchetype { AllRounder, Rusher, Tank, Sniper, Berserker, Wall }

    public struct BuildIds
    {
        public BuildArchetype Archetype;
        public string Kite, Tail, Dor, Charkhi;

        public BuildIds(BuildArchetype a, string kite, string tail, string dor, string charkhi)
        {
            Archetype = a; Kite = kite; Tail = tail; Dor = dor; Charkhi = charkhi;
        }
    }

    /// <summary>Hard bots pick a counter to the player's build (spec 14.3); Easy bots use Sooti + Lakri.</summary>
    public static class BotBuilds
    {
        public static BuildIds Get(BuildArchetype a)
        {
            switch (a)
            {
                case BuildArchetype.Rusher: return new BuildIds(a, "pipa", "none", "reshmi", "speed");
                case BuildArchetype.Tank: return new BuildIds(a, "chula", "long_tail", "mazboot", "brake");
                case BuildArchetype.Sniper: return new BuildIds(a, "rokkaku", "double_ribbon", "lambi", "bari");
                case BuildArchetype.Berserker: return new BuildIds(a, "gudiparan", "tassel", "tez_dhaar", "gear");
                case BuildArchetype.Wall: return new BuildIds(a, "wau", "long_tail", "lachakdaar", "brake");
                default: return new BuildIds(a, "guddi", "short_ribbon", "sooti", "lakri");
            }
        }

        /// <summary>Guess the archetype from the dor (each build is defined by its dor).</summary>
        public static BuildArchetype Classify(string dorId)
        {
            switch (dorId)
            {
                case "reshmi": return BuildArchetype.Rusher;
                case "mazboot": return BuildArchetype.Tank;
                case "lambi": return BuildArchetype.Sniper;
                case "tez_dhaar": return BuildArchetype.Berserker;
                case "lachakdaar": return BuildArchetype.Wall;
                default: return BuildArchetype.AllRounder;
            }
        }

        /// <summary>The build that this archetype is weak against (spec 7.5 "Weak against").</summary>
        public static BuildArchetype CounterOf(BuildArchetype a, DeterministicRandom rng)
        {
            switch (a)
            {
                case BuildArchetype.Rusher: return BuildArchetype.Tank;
                case BuildArchetype.Tank: return BuildArchetype.Sniper;
                case BuildArchetype.Sniper: return BuildArchetype.Rusher;
                case BuildArchetype.Berserker: return BuildArchetype.Wall;
                case BuildArchetype.Wall: return BuildArchetype.Rusher; // dive users
                default: return (BuildArchetype)rng.Range(1, 6);   // all-rounder: any specialist
            }
        }

        public static BuildIds ForBot(BotDifficulty difficulty, string playerDorId, DeterministicRandom rng)
        {
            switch (difficulty)
            {
                case BotDifficulty.Hard: return Get(CounterOf(Classify(playerDorId), rng));
                case BotDifficulty.Medium: return Get((BuildArchetype)rng.Range(0, 6));
                default: return Get(BuildArchetype.AllRounder);
            }
        }
    }
}
