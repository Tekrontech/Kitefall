using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>Helpers to build a <see cref="MatchSetup"/> quickly (offline matches, tests, bot fill).</summary>
    public static class MatchFactory
    {
        public static readonly string[] BotNames =
        {
            "Shahbaz", "Ayesha", "Rafael", "Kenji", "Somchai", "Bilal", "Lucia", "Arif", "Putri", "Hamid",
            "Mei", "Thiago", "Zara", "Farid", "Anong", "Yusuf", "Haruto", "Nadia", "Bruno", "Siti"
        };

        public static readonly string[] BotCountries = { "PK", "IN", "BR", "JP", "TH", "AF", "ID", "MY", "AE", "GB" };

        /// <summary>Default setup with spec data; pool lists for crates filled from all dors / charkhis.</summary>
        public static MatchSetup NewSetup(GameMode mode, ArenaType arena, int seed)
        {
            var setup = new MatchSetup
            {
                Rules = ModeRules.For(mode),
                Arena = arena,
                Seed = seed,
                FreeGuddi = SpecDefaults.FreeGuddi,
                BasicDor = SpecDefaults.Dor("sooti"),
                BasicCharkhi = SpecDefaults.Charkhi("lakri")
            };
            foreach (var d in SpecDefaults.Dors) if (d.Key != "sooti") setup.CrateDors.Add(d.Value);
            foreach (var c in SpecDefaults.Charkhis) if (c.Key != "lakri") setup.CrateCharkhis.Add(c.Value);
            var rng = new DeterministicRandom(seed);
            setup.TimeOfDay01 = rng.Range(0.25f, 0.85f);
            return setup;
        }

        public static ParticipantSetup Bot(int index, BotDifficulty difficulty, BuildIds build, int team, int seed)
        {
            var rng = new DeterministicRandom(seed + index * 13);
            // Same offset for every bot of a match + its index = unique names within a match.
            int offset = new DeterministicRandom(seed).Range(0, BotNames.Length);
            var p = new ParticipantSetup
            {
                playerId = "bot_" + index,
                displayName = BotNames[(index + offset) % BotNames.Length],
                country = BotCountries[rng.Range(0, BotCountries.Length)],
                isBot = true,
                botDifficulty = difficulty,
                team = team
            };
            ParticipantLoadout l = SpecDefaults.Loadout(build.Kite, build.Tail, build.Dor, build.Charkhi);
            for (int i = 0; i < 3; i++) p.stack.Add(l);
            return p;
        }

        /// <summary>Fills empty seats with labelled bots (spec 5: never in Ranked).</summary>
        public static void FillWithBots(MatchSetup setup, int targetCount, BotDifficulty difficulty, string playerDorId)
        {
            if (!setup.Rules.allowBots) return;
            var rng = new DeterministicRandom(setup.Seed + 77);
            int start = setup.Participants.Count;
            for (int i = start; i < targetCount; i++)
            {
                BuildIds build = BotBuilds.ForBot(difficulty, playerDorId, rng);
                int team = setup.Rules.teamCount > 1 ? i % setup.Rules.teamCount : 0;
                ParticipantSetup bot = Bot(i, difficulty, build, team, setup.Seed);
                if (setup.Rules.teamCount > 1 && i < setup.Rules.teamCount) bot.captain = true;
                setup.Participants.Add(bot);
            }
        }

        /// <summary>Creates brains for every bot participant.</summary>
        public static List<BotBrain> CreateBrains(MatchSetup setup)
        {
            var list = new List<BotBrain>();
            for (int i = 0; i < setup.Participants.Count; i++)
                if (setup.Participants[i].isBot) list.Add(new BotBrain(i, setup.Participants[i].botDifficulty, setup.Seed));
            return list;
        }
    }
}
