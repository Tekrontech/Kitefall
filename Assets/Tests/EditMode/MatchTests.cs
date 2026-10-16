using System.Collections.Generic;
using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    internal static class MatchRunner
    {
        public static MatchSimulation Run(GameMode mode, int players, BotDifficulty diff, int seed, float maxSeconds = 1500f, System.Action<MatchSimulation> perTick = null)
        {
            MatchSetup setup = MatchFactory.NewSetup(mode, ArenaType.OldCityRooftops, seed);
            setup.Rules.allowBots = true;
            MatchFactory.FillWithBots(setup, players, diff, "sooti");
            var sim = new MatchSimulation(setup);
            List<BotBrain> brains = MatchFactory.CreateBrains(setup);
            var inputs = new KiteInput[players];
            int maxTicks = (int)(maxSeconds * MatchSimulation.TickRate);
            for (int t = 0; t < maxTicks && !sim.IsOver; t++)
            {
                foreach (BotBrain b in brains) inputs[b.Self] = b.Think(sim);
                sim.Step(inputs);
                perTick?.Invoke(sim);
            }
            return sim;
        }
    }

    public class MatchSimulationTests
    {
        [Test]
        public void QuickDuel_BotsFinish_FirstToTwo()
        {
            MatchSimulation sim = MatchRunner.Run(GameMode.QuickDuel, 2, BotDifficulty.Medium, 1);
            Assert.IsTrue(sim.IsOver);
            MatchResult r = sim.Result;
            Assert.IsTrue(r.Draw || r.Participants[r.WinnerIndex].RoundsWon == 2);
            Assert.GreaterOrEqual(r.Rounds.Count, 2);
        }

        [Test]
        public void RankedDuel_IsBestOfFive_WithLoadoutBreak()
        {
            bool sawBreak = false;
            MatchSimulation sim = MatchRunner.Run(GameMode.RankedDuel, 2, BotDifficulty.Hard, 4, perTick: s => { if (s.Phase == MatchPhase.LoadoutBreak) sawBreak = true; });
            Assert.IsTrue(sim.IsOver);
            Assert.IsTrue(sawBreak);
            if (!sim.Result.Draw) Assert.AreEqual(3, sim.Result.Participants[sim.Result.WinnerIndex].RoundsWon);
        }

        [Test]
        public void SameSeed_SameMatch()
        {
            MatchSimulation a = MatchRunner.Run(GameMode.QuickDuel, 2, BotDifficulty.Hard, 11);
            MatchSimulation b = MatchRunner.Run(GameMode.QuickDuel, 2, BotDifficulty.Hard, 11);
            Assert.AreEqual(a.Tick, b.Tick);
            Assert.AreEqual(a.Result.WinnerIndex, b.Result.WinnerIndex);
            Assert.AreEqual(a.Rounds.Count, b.Rounds.Count);
        }

        [Test]
        public void FreeForAll_RunsFullTime_WithCutsRespawnsAndPlacements()
        {
            int respawns = 0, crates = 0;
            MatchSimulation sim = MatchRunner.Run(GameMode.FreeForAll, 8, BotDifficulty.Medium, 3, perTick: s =>
            {
                foreach (MatchEvent e in s.Events)
                {
                    if (e.Type == MatchEventType.Respawned) respawns++;
                    if (e.Type == MatchEventType.CrateSpawned) crates++;
                }
            });
            Assert.IsTrue(sim.IsOver);
            Assert.AreEqual(720f, sim.MatchTime, 1f);
            Assert.Greater(respawns, 0);
            Assert.AreEqual(4, crates, "One sky crate every 3 minutes");
            var places = new HashSet<int>();
            int cuts = 0, scoreFromCuts = 0;
            foreach (ParticipantResult p in sim.Result.Participants)
            {
                places.Add(p.Placement);
                cuts += p.Cuts;
                scoreFromCuts += p.Cuts * 100 + p.Assists * 40 - p.SelfSnaps * 50;
                Assert.AreEqual(p.Cuts * 100 + p.Assists * 40 - p.SelfSnaps * 50, p.Score);
            }
            Assert.AreEqual(8, places.Count);
            Assert.Greater(cuts, 5);
        }

        [Test]
        public void TeamBattle_NoContestsBetweenTeamMates()
        {
            bool friendly = false;
            MatchSimulation sim = MatchRunner.Run(GameMode.TeamBattle, 8, BotDifficulty.Medium, 5, perTick: s =>
            {
                foreach (MatchEvent e in s.Events)
                    if (e.Type == MatchEventType.ContactStarted && s.Participants[e.A].Team == s.Participants[e.B].Team) friendly = true;
            });
            Assert.IsTrue(sim.IsOver);
            Assert.IsFalse(friendly);
            Assert.IsTrue(sim.Result.Draw || sim.Result.WinningTeam >= 0);
        }

        [Test]
        public void LastKiteFlying_EndsWithOneWinner_AndBasicGear()
        {
            MatchSetup check = MatchFactory.NewSetup(GameMode.LastKiteFlying, ArenaType.StormArchipelago, 2);
            MatchFactory.FillWithBots(check, 6, BotDifficulty.Hard, "sooti");
            var s0 = new MatchSimulation(check);
            foreach (MatchParticipant p in s0.Participants) Assert.AreEqual("sooti", p.Current.dorId);

            MatchSimulation sim = MatchRunner.Run(GameMode.LastKiteFlying, 6, BotDifficulty.Medium, 2);
            Assert.IsTrue(sim.IsOver);
            Assert.GreaterOrEqual(sim.Result.WinnerIndex, 0);
            Assert.AreEqual(1, sim.Result.Participants[sim.Result.WinnerIndex].Placement);
        }

        [Test]
        public void Forfeit_EndsDuel_OpponentWins()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.QuickDuel, ArenaType.OldCityRooftops, 1);
            MatchFactory.FillWithBots(setup, 2, BotDifficulty.Easy, "sooti");
            var sim = new MatchSimulation(setup);
            sim.Forfeit(0);
            Assert.IsTrue(sim.IsOver);
            Assert.AreEqual(1, sim.Result.WinnerIndex);
            Assert.IsTrue(sim.Result.Participants[0].Quit);
        }

        [Test]
        public void PerfectLaunch_RaisesStartHeight()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.QuickDuel, ArenaType.RoyalField, 1);
            MatchFactory.FillWithBots(setup, 2, BotDifficulty.Easy, "sooti");
            setup.Rules.introTime = 0f;
            var sim = new MatchSimulation(setup);
            var inputs = new KiteInput[2];
            int ticks = 0;
            bool perfect = false;
            while (sim.Phase == MatchPhase.Launch)
            {
                inputs[0].PullHeld = ticks < 28;   // 1.4 s hold → perfect window
                inputs[1].PullHeld = ticks < 10;   // 0.5 s → normal
                sim.Step(inputs);
                foreach (MatchEvent e in sim.Events) if (e.Type == MatchEventType.PerfectLaunch && e.A == 0) perfect = true;
                ticks++;
            }
            Assert.IsTrue(perfect);
            Assert.Greater(sim.Participants[0].Kite.Flight.Height, sim.Participants[1].Kite.Flight.Height + 5f);
        }

        [Test]
        public void LoadoutChange_OnlyDuringBreak_AndKeepsKiteAndDor()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.RankedDuel, ArenaType.OldCityRooftops, 1);
            setup.Rules.allowBots = true;
            MatchFactory.FillWithBots(setup, 2, BotDifficulty.Easy, "sooti");
            var sim = new MatchSimulation(setup);
            Assert.IsFalse(sim.TryChangeLoadout(0, SpecDefaults.Tail("long_tail"), "long_tail", SpecDefaults.Charkhi("speed"), "speed"));
        }

        [Test]
        public void Disconnected_Player_ForfeitsAfter60s()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.QuickDuel, ArenaType.OldCityRooftops, 1);
            MatchFactory.FillWithBots(setup, 2, BotDifficulty.Easy, "sooti");
            setup.Participants[1].isBot = false;
            var sim = new MatchSimulation(setup);
            var inputs = new KiteInput[2];
            sim.SetConnected(1, false);
            for (int i = 0; i < 20 * 200 && !sim.IsOver; i++) sim.Step(inputs);
            Assert.IsTrue(sim.IsOver);
            Assert.AreEqual(0, sim.Result.WinnerIndex);
        }

        [Test]
        public void Snapshot_MatchesSimulation()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.FreeForAll, ArenaType.OldCityRooftops, 1);
            MatchFactory.FillWithBots(setup, 4, BotDifficulty.Easy, "sooti");
            var sim = new MatchSimulation(setup);
            var brains = MatchFactory.CreateBrains(setup);
            var inputs = new KiteInput[4];
            for (int i = 0; i < 400; i++) { foreach (var b in brains) inputs[b.Self] = b.Think(sim); sim.Step(inputs); }
            var snap = new MatchSnapshot();
            sim.Capture(snap);
            Assert.AreEqual(4, snap.Kites.Length);
            for (int i = 0; i < 4; i++)
            {
                if (!sim.Participants[i].Kite.IsFlying) continue;
                Assert.Less(System.Numerics.Vector3.Distance(snap.Kites[i].WorldPosition, sim.Participants[i].Kite.WorldPosition), 0.01f);
            }
        }

        [Test]
        public void Arena_IsDeterministic_AndAnchorsAreFree()
        {
            ArenaLayout a = ArenaLayout.Generate(ArenaType.OldCityRooftops, 5, 20);
            ArenaLayout b = ArenaLayout.Generate(ArenaType.OldCityRooftops, 5, 20);
            Assert.AreEqual(a.Blocks.Count, b.Blocks.Count);
            Assert.AreEqual(20, a.Anchors.Count);
            foreach (var anchor in a.Anchors) Assert.IsFalse(a.IsSolid(anchor), "Flyer hands must not be inside a building");
        }
    }

    public class BotTests
    {
        [Test]
        public void HardBots_BeatEasyBots_MostOfTheTime()
        {
            int hardWins = 0, games = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                MatchSetup setup = MatchFactory.NewSetup(GameMode.QuickDuel, ArenaType.OldCityRooftops, seed);
                setup.Participants.Add(MatchFactory.Bot(0, BotDifficulty.Hard, BotBuilds.Get(BuildArchetype.AllRounder), 0, seed));
                setup.Participants.Add(MatchFactory.Bot(1, BotDifficulty.Easy, BotBuilds.Get(BuildArchetype.AllRounder), 0, seed));
                var sim = new MatchSimulation(setup);
                var brains = MatchFactory.CreateBrains(setup);
                var inputs = new KiteInput[2];
                for (int t = 0; t < 20 * 900 && !sim.IsOver; t++) { foreach (var b in brains) inputs[b.Self] = b.Think(sim); sim.Step(inputs); }
                if (!sim.IsOver || sim.Result.Draw) continue;
                games++;
                if (sim.Result.WinnerIndex == 0) hardWins++;
            }
            Assert.Greater(games, 6);
            Assert.Greater(hardWins, games / 2);
        }

        [Test]
        public void HardBot_PicksCounterBuild()
        {
            var rng = new DeterministicRandom(1);
            Assert.AreEqual(BuildArchetype.Tank, BotBuilds.ForBot(BotDifficulty.Hard, "reshmi", rng).Archetype);
            Assert.AreEqual(BuildArchetype.Wall, BotBuilds.ForBot(BotDifficulty.Hard, "tez_dhaar", rng).Archetype);
            Assert.AreEqual("sooti", BotBuilds.ForBot(BotDifficulty.Easy, "lambi", rng).Dor);
        }

        [Test]
        public void Bots_AreLabelled_AndNeverFillRanked()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.RankedDuel, ArenaType.OldCityRooftops, 1);
            MatchFactory.FillWithBots(setup, 2, BotDifficulty.Easy, "sooti");
            Assert.AreEqual(0, setup.Participants.Count);
            MatchSetup quick = MatchFactory.NewSetup(GameMode.QuickDuel, ArenaType.OldCityRooftops, 1);
            MatchFactory.FillWithBots(quick, 2, BotDifficulty.Easy, "sooti");
            Assert.IsTrue(quick.Participants.TrueForAll(p => p.isBot && p.playerId.StartsWith("bot_")));
        }
    }

    public class ExplainerTests
    {
        [Test]
        public void WinningWithHeightAndCounter_IsExplained()
        {
            var c = new ContestSummary
            {
                A = 0, B = 1, Winner = 0, Loser = 1, Outcome = ContestOutcome.AWins, AttackA = AttackType.Dive, AttackB = AttackType.Dheel,
                PositionA = 1.10f, PositionB = 0.90f, CounterA = 1.15f, CounterB = 0.87f, InputA = 1f, InputB = 1f, AngleA = 1.1f, AngleB = 1.1f,
                WindA = 1, WindB = 1, DiveA = 1.25f, DiveB = 1, ConditionA = 1, ConditionB = 1, ClassA = 1, ClassB = 1.05f, DorA = 1, DorB = 1,
                CharkhiA = 1, CharkhiB = 1, MultiA = 1, MultiB = 1, SweetA = 0.82f, SweetB = 0.5f
            };
            string win = ResultsExplainer.Explain(c, 0);
            StringAssert.StartsWith("You won:", win);
            StringAssert.Contains("82%", win);
            string loss = ResultsExplainer.Explain(c, 1);
            StringAssert.StartsWith("You lost:", loss);
            StringAssert.Contains("try Kheench Cut", loss);
        }
    }
}
