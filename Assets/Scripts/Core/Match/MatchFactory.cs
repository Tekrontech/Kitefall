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
            DressBot(p, rng);
            return p;
        }

        /// <summary>Gives a bot a character, outfit and maybe an accessory (visual only).</summary>
        public static void DressBot(ParticipantSetup p, DeterministicRandom rng)
        {
            var chars = CharacterCatalog.Characters;
            p.characterId = chars[rng.Range(0, chars.Count)].id;
            p.outfitId = rng.Chance(0.5f) ? "" : CharacterCatalog.Outfits[rng.Range(0, CharacterCatalog.Outfits.Count - 1)].id;
            p.accessories = new string[4];
            if (rng.Chance(0.5f))
            {
                CosmeticItem acc = CharacterCatalog.Accessories[rng.Range(0, CharacterCatalog.Accessories.Count)];
                p.accessories[acc.slot] = acc.id;
            }
        }

        /// <summary>
        /// Training ground (upgrade F8): target dummies at 3 heights (static, slow, fast) and optional sparring bots.
        /// Uses the roof network so the player can practise roof hopping too.
        /// </summary>
        public static MatchSetup TrainingSetup(ParticipantSetup player, TrainingOptions options, int seed)
        {
            MatchSetup setup = NewSetup(GameMode.Training, ArenaType.OldCityRooftops, seed);
            setup.Training = options ?? new TrainingOptions();
            setup.UseRoofGraph = true;
            setup.UseLaunchRitual = true;
            setup.Participants.Add(player);
            var rng = new DeterministicRandom(seed + 5);
            if (setup.Training.dummies)
            {
                for (int i = 0; i < 3; i++)
                {
                    ParticipantSetup d = Bot(setup.Participants.Count, BotDifficulty.Dummy, new BuildIds { Kite = "guddi", Tail = "short_ribbon", Dor = "sooti", Charkhi = "lakri" }, 0, seed);
                    d.displayName = i == 0 ? "Dummy (static, low)" : i == 1 ? "Dummy (slow, mid)" : "Dummy (fast, high)";
                    d.dummyPattern = i;
                    d.dummyHeight = i;
                    setup.Participants.Add(d);
                }
            }
            for (int i = 0; i < setup.Training.sparringBots; i++)
            {
                BuildIds build = BotBuilds.ForBot(setup.Training.sparringDifficulty, player.stack.Count > 0 ? player.stack[0].dorId : "sooti", rng);
                setup.Participants.Add(Bot(setup.Participants.Count, setup.Training.sparringDifficulty, build, 0, seed));
            }
            return setup;
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
