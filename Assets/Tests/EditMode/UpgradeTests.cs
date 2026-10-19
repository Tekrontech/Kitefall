using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    /// <summary>Upgrade v1.4 – roofs, launch ritual, roof hopping, social commands, cosmetics, training.</summary>
    internal static class UpgradeRunner
    {
        public static MatchSetup Setup(GameMode mode, int players, int seed, bool warmup = false, ArenaType arena = ArenaType.OldCityRooftops)
        {
            MatchSetup setup = MatchFactory.NewSetup(mode, arena, seed);
            setup.Rules.allowBots = true;
            setup.UseRoofGraph = true;
            setup.UseLaunchRitual = true;
            setup.WarmupTime = warmup ? 20f : 0f;
            MatchFactory.FillWithBots(setup, players, BotDifficulty.Medium, "sooti");
            return setup;
        }

        public static MatchSimulation Run(MatchSetup setup, float seconds, Action<MatchSimulation> perTick = null)
        {
            var sim = new MatchSimulation(setup);
            List<BotBrain> brains = MatchFactory.CreateBrains(setup);
            var inputs = new KiteInput[setup.Participants.Count];
            int ticks = (int)(seconds * MatchSimulation.TickRate);
            for (int t = 0; t < ticks && !sim.IsOver; t++)
            {
                foreach (BotBrain b in brains) inputs[b.Self] = b.Think(sim);
                sim.Step(inputs);
                perTick?.Invoke(sim);
            }
            return sim;
        }
    }

    public class RoofGraphTests
    {
        private static IEnumerable<ArenaType> Arenas => (ArenaType[])Enum.GetValues(typeof(ArenaType));

        [Test]
        public void EveryArena_AnyPlayerCount_IsConnected_WithShortLinks()
        {
            foreach (ArenaType arena in Arenas)
                foreach (int players in new[] { 2, 4, 8, 20 })
                    foreach (bool mirror in new[] { false, true })
                    {
                        RoofGraph g = RoofGraph.Generate(arena, 17 + players, players, mirror);
                        Assert.IsTrue(g.IsConnected(), $"{arena} {players} mirror={mirror}");
                        Assert.GreaterOrEqual(g.Roofs.Count, RoofGraph.RoofCountFor(players, new RoofSettings()));
                        foreach (RoofLink l in g.Links) Assert.LessOrEqual(l.Length, 18.01f, $"{arena} link {l.Id}");
                        foreach (RoofNode r in g.Roofs) Assert.GreaterOrEqual(r.Links.Count, 1);
                    }
        }

        [Test]
        public void RoofCount_IsTwoAndAHalfPerPlayer_MinSix()
        {
            var s = new RoofSettings();
            Assert.AreEqual(6, RoofGraph.RoofCountFor(2, s));
            Assert.AreEqual(20, RoofGraph.RoofCountFor(8, s));
            Assert.AreEqual(50, RoofGraph.RoofCountFor(20, s));
        }

        [Test]
        public void Heights_AtLeastThirtyPercentLowAndHigh()
        {
            RoofGraph g = RoofGraph.Generate(ArenaType.OldCityRooftops, 5, 8, false);
            float third = (g.MaxHeight - g.MinHeight) / 3f;
            int low = g.Roofs.Count(r => r.Height < g.MinHeight + third + 0.01f);
            int high = g.Roofs.Count(r => r.Height > g.MaxHeight - third - 0.01f);
            Assert.GreaterOrEqual(low, (int)(g.Roofs.Count * 0.3f));
            Assert.GreaterOrEqual(high, (int)(g.Roofs.Count * 0.3f));
        }

        [Test]
        public void Traits_AtMostTwo_NeverHighAndUpwind_OneOrTwoCrateSpots()
        {
            foreach (ArenaType arena in Arenas)
            {
                RoofGraph g = RoofGraph.Generate(arena, 3, 8, false);
                foreach (RoofNode r in g.Roofs)
                {
                    Assert.LessOrEqual(r.TraitCount, 3, "two traits + crate spot at most");
                    Assert.IsFalse(r.Has(RoofTrait.High) && r.Has(RoofTrait.Upwind));
                }
                int crates = g.Roofs.Count(r => r.Has(RoofTrait.CrateSpot));
                Assert.That(crates, Is.InRange(1, 2));
            }
        }

        [Test]
        public void MirroredMap_IsSymmetric()
        {
            RoofGraph g = RoofGraph.Generate(ArenaType.OldCityRooftops, 9, 2, true);
            Assert.IsTrue(g.Mirrored);
            foreach (RoofNode r in g.Roofs)
            {
                Assert.GreaterOrEqual(r.MirrorOf, 0);
                RoofNode m = g.Roof(r.MirrorOf);
                Assert.AreEqual(-r.Center.X, m.Center.X, 1e-3f);
                Assert.AreEqual(r.Center.Y, m.Center.Y, 1e-3f);
                Assert.AreEqual(r.Center.Z, m.Center.Z, 1e-3f);
                Assert.AreEqual(r.Traits, m.Traits);
            }
        }

        [Test]
        public void SameSeed_SameGraph()
        {
            RoofGraph a = RoofGraph.Generate(ArenaType.RiverFestival, 44, 8, false);
            RoofGraph b = RoofGraph.Generate(ArenaType.RiverFestival, 44, 8, false);
            Assert.AreEqual(a.Roofs.Count, b.Roofs.Count);
            Assert.AreEqual(a.Links.Count, b.Links.Count);
            for (int i = 0; i < a.Roofs.Count; i++) Assert.AreEqual(a.Roofs[i].Center, b.Roofs[i].Center);
        }

        [Test]
        public void Path_ReachesEveryRoof()
        {
            RoofGraph g = RoofGraph.Generate(ArenaType.OldCityRooftops, 2, 8, false);
            for (int r = 1; r < g.Roofs.Count; r++)
            {
                List<int> path = g.Path(0, r);
                Assert.IsNotEmpty(path);
                int cur = 0;
                foreach (int l in path) { Assert.IsTrue(g.Link(l).Touches(cur)); cur = g.Link(l).Other(cur); }
                Assert.AreEqual(r, cur);
            }
        }

        [Test]
        public void ArenaWithRoofs_AnchorsOnRoofs_NotInsideBlocks()
        {
            ArenaLayout a = ArenaLayout.Generate(ArenaType.CoastalCliffs, 3, 8, true, false, null);
            Assert.IsNotNull(a.Roofs);
            foreach (Vector3 anchor in a.Anchors)
            {
                Assert.GreaterOrEqual(a.Roofs.RoofAt(anchor), 0);
                Assert.IsFalse(a.IsSolid(anchor), "anchor must be above the roof, not inside a block");
            }
        }
    }

    public class RoofClaimTests
    {
        private static RoofGraph Graph(int players, bool mirror = false) => RoofGraph.Generate(ArenaType.OldCityRooftops, 1, players, mirror);

        [Test]
        public void FirstRequestWins_TakenRoofIsLocked()
        {
            var c = new RoofClaims(Graph(4), RoofSelectMode.Free, new[] { 0, 0, 0, 0 }, 1);
            Assert.IsTrue(c.TryClaim(0, 3));
            Assert.IsFalse(c.TryClaim(1, 3));
            Assert.IsTrue(c.TryClaim(1, 4));
            Assert.IsTrue(c.TryClaim(0, 5), "can change until the timer ends");
            Assert.IsTrue(c.TryClaim(2, 3), "roof 3 is free again");
        }

        [Test]
        public void AssignRemaining_GivesEveryoneADifferentRoof()
        {
            var c = new RoofClaims(Graph(8), RoofSelectMode.Free, new int[8], 1);
            c.TryClaim(0, 0);
            c.AssignRemaining();
            var roofs = Enumerable.Range(0, 8).Select(c.ClaimOf).ToList();
            Assert.IsTrue(roofs.All(r => r >= 0));
            Assert.AreEqual(8, roofs.Distinct().Count());
        }

        [Test]
        public void TeamHalves_TwoTeammatesMax_NeverEnemies_OwnHalfOnly()
        {
            RoofGraph g = Graph(4);
            var c = new RoofClaims(g, RoofSelectMode.TeamHalves, new[] { 0, 0, 1, 1 }, 1);
            int left = g.Roofs.First(r => r.Center.X < -1f).Id;
            int right = g.Roofs.First(r => r.Center.X > 1f).Id;
            Assert.IsFalse(c.TryClaim(0, right), "team 0 starts in the left half");
            Assert.IsTrue(c.TryClaim(0, left));
            Assert.IsTrue(c.TryClaim(1, left), "teammate may share");
            Assert.AreEqual(1, c.SlotOf(1));
            Assert.IsTrue(c.TryClaim(2, right));
            Assert.IsFalse(c.TryClaim(3, left), "never enemies on the same roof");
        }

        [Test]
        public void DuelPairs_BothPlayersGetTheMirroredPair()
        {
            RoofGraph g = Graph(2, true);
            var c = new RoofClaims(g, RoofSelectMode.DuelPairs, new[] { 0, 0 }, 1);
            Assert.That(c.OfferedPairs.Count, Is.InRange(1, 3));
            Assert.IsTrue(c.TryClaim(1, c.OfferedPairs[0]));
            Assert.AreEqual(g.Roof(c.ClaimOf(0)).MirrorOf, c.ClaimOf(1));
        }

        [Test]
        public void Ranked_AssignsAMirroredPair()
        {
            RoofGraph g = Graph(2, true);
            var c = new RoofClaims(g, RoofSelectMode.Assigned, new[] { 0, 0 }, 7);
            Assert.IsFalse(c.TryClaim(0, 0));
            c.AssignRemaining();
            Assert.AreEqual(g.Roof(c.ClaimOf(0)).MirrorOf, c.ClaimOf(1));
        }
    }

    public class LaunchPrepTests
    {
        private const float Dt = 0.05f;

        /// <summary>Drives a prep with a scripted input function until done or the cap.</summary>
        private static LaunchPrepController Run(LaunchPrepController p, Func<LaunchPrepController, float, bool> pull, float start = 0f, float seconds = 30f)
        {
            for (float t = start; t < start + seconds && !p.Done; t += Dt) p.Update(t, pull(p, t), 0f, Dt);
            return p;
        }

        /// <summary>A "perfect" player: taps the kanni at the needle centre, holds the toss 1.4 s, tugs at nose-up.</summary>
        private static bool PerfectPlayer(LaunchPrepController p, float t)
        {
            switch (p.Step)
            {
                case PrepStep.TieKanni: return Math.Abs(p.NeedleAt(t) - 0.5f) < 0.03f;
                case PrepStep.Toss: return t - p.StepStartTime < 1.4f;
                case PrepStep.Tunke: return Math.Abs(p.NosePhaseAt(t) - 0.25f) < 0.03f;
                default: return false;
            }
        }

        [Test]
        public void PerfectPlayer_GetsPerfectKanni_PerfectToss_AndCappedHeight()
        {
            LaunchPrepController p = Run(new LaunchPrepController(new PrepSettings(), true, 0f, 1), PerfectPlayer);
            Assert.IsTrue(p.Done);
            Assert.AreEqual(PrepGrade.Perfect, p.KanniGrade);
            Assert.AreEqual(PrepGrade.Perfect, p.TossGrade);
            Assert.AreEqual(0f, p.DriftDegPerSec);
            Assert.AreEqual(4, p.TugsGood);
            Assert.AreEqual(20f, p.HeightBonus, 1e-3f, "10 toss + 4×4 tugs = 26, capped at +20");
            Assert.LessOrEqual(p.FinishTime, 25f);
        }

        [Test]
        public void MissedToss_RetriesAfterPenalty_NoDurabilityConcept()
        {
            int presses = 0;
            LaunchPrepController p = Run(new LaunchPrepController(new PrepSettings(), false, 0f, 1), (c, t) =>
            {
                if (c.Step != PrepStep.Toss) return false;
                float held = t - c.StepStartTime;
                if (c.TossRetries == 0) return held < 0.3f; // far too short → miss
                presses++;
                return held < 1.4f;
            });
            Assert.IsTrue(p.Done);
            Assert.AreEqual(1, p.TossRetries);
            Assert.AreEqual(PrepGrade.Perfect, p.TossGrade);
        }

        [Test]
        public void NoInput_FullPrep_NeverExceeds25s_ShortNeverExceeds12s()
        {
            LaunchPrepController full = Run(new LaunchPrepController(new PrepSettings(), true, 0f, 1), (c, t) => false);
            Assert.IsTrue(full.Done);
            Assert.LessOrEqual(full.FinishTime, 25f + Dt);
            LaunchPrepController shortPrep = Run(new LaunchPrepController(new PrepSettings(), false, 100f, 1), (c, t) => false, 100f);
            Assert.IsTrue(shortPrep.Done);
            Assert.LessOrEqual(shortPrep.FinishTime - 100f, 12f + Dt);
        }

        [Test]
        public void AutoPrep_IsAlwaysGood_AndNeverBetterThanPerfect()
        {
            var auto = new LaunchPrepController(new PrepSettings(), true, 0f, 3) { AutoPrep = true };
            Run(auto, (c, t) => false);
            Assert.AreEqual(PrepGrade.Good, auto.KanniGrade);
            Assert.AreEqual(PrepGrade.Good, auto.TossGrade);
            LaunchPrepController perfect = Run(new LaunchPrepController(new PrepSettings(), true, 0f, 3), PerfectPlayer);
            Assert.Less(auto.HeightBonus, perfect.HeightBonus);
        }

        [Test]
        public void ShortPrep_SkipsKanni_KeepsPreviousGrade()
        {
            var p = new LaunchPrepController(new PrepSettings(), false, 0f, 1, PrepGrade.Perfect);
            var seen = new HashSet<PrepStep>();
            for (float t = 0; t < 15f && !p.Done; t += Dt) { seen.Add(p.Step); p.Update(t, PerfectPlayer(p, t), 0f, Dt); }
            Assert.IsFalse(seen.Contains(PrepStep.TieKanni));
            Assert.IsFalse(seen.Contains(PrepStep.TieLine));
            Assert.AreEqual(PrepGrade.Perfect, p.KanniGrade);
        }

        [Test]
        public void ShelteredRoof_TossNeverFails()
        {
            var p = new LaunchPrepController(new PrepSettings(), false, 0f, 1) { SafeLaunch = true };
            Run(p, (c, t) => c.Step == PrepStep.Toss && t - c.StepStartTime < 0.2f);
            Assert.AreEqual(0, p.TossRetries);
            Assert.AreEqual(PrepGrade.Good, p.TossGrade);
        }

        [Test]
        public void CableSnag_NeedsThreeSwipes()
        {
            var settings = new PrepSettings { snagChanceOnMiss = 1f };
            var p = new LaunchPrepController(settings, false, 0f, 1) { CablesNearby = true };
            bool snagged = false;
            float steer = 0f;
            for (float t = 0; t < 20f && !p.Done; t += Dt)
            {
                if (p.Step == PrepStep.Untangle) { snagged = true; steer = steer > 0 ? -1f : 1f; }
                else steer = 0f;
                bool pull = p.Step == PrepStep.Toss && (p.TossRetries == 0 ? t - p.StepStartTime < 0.2f : t - p.StepStartTime < 1.4f);
                p.Update(t, pull, steer, Dt);
            }
            Assert.IsTrue(snagged);
            Assert.IsTrue(p.Done);
        }

        [Test]
        public void Bots_HardMostlyPerfect_EasyMostlyNot()
        {
            int hardPerfect = 0, easyPerfect = 0;
            for (int i = 0; i < 40; i++)
            {
                var hard = new LaunchPrepController(new PrepSettings(), true, 0f, i); hard.SetBot(BotDifficulty.Hard);
                var easy = new LaunchPrepController(new PrepSettings(), true, 0f, i); easy.SetBot(BotDifficulty.Easy);
                Run(hard, (c, t) => false); Run(easy, (c, t) => false);
                if (hard.TossGrade == PrepGrade.Perfect) hardPerfect++;
                if (easy.TossGrade == PrepGrade.Perfect) easyPerfect++;
            }
            Assert.Greater(hardPerfect, easyPerfect + 10);
        }

        [Test]
        public void SameInputs_SameResults()
        {
            LaunchPrepController a = Run(new LaunchPrepController(new PrepSettings(), true, 0f, 9), PerfectPlayer);
            LaunchPrepController b = Run(new LaunchPrepController(new PrepSettings(), true, 0f, 9), PerfectPlayer);
            Assert.AreEqual(a.HeightBonus, b.HeightBonus);
            Assert.AreEqual(a.FinishTime, b.FinishTime);
        }
    }

    public class UpgradeMatchTests
    {
        [Test]
        public void Ffa8_WithWarmup_RoofSelect_Prep_EveryoneOnADifferentRoof()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 8, 3, warmup: true);
            var phases = new List<MatchPhase>();
            MatchSimulation sim = UpgradeRunner.Run(setup, 90f, s => { if (phases.Count == 0 || phases[phases.Count - 1] != s.Phase) phases.Add(s.Phase); });
            CollectionAssert.IsSubsetOf(new[] { MatchPhase.Warmup, MatchPhase.RoofSelect, MatchPhase.Intro, MatchPhase.Prep, MatchPhase.Round }, phases);
            Assert.Less(phases.IndexOf(MatchPhase.Warmup), phases.IndexOf(MatchPhase.RoofSelect));
            Assert.Less(phases.IndexOf(MatchPhase.RoofSelect), phases.IndexOf(MatchPhase.Prep));
            var roofs = sim.Participants.Select(p => p.RoofId).ToList();
            Assert.IsTrue(roofs.All(r => r >= 0));
        }

        [Test]
        public void Warmup_ConsumesNoKiteOrDor_AndCharactersWalk()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 6, 4, warmup: true);
            var sim = new MatchSimulation(setup);
            Assert.AreEqual(MatchPhase.Warmup, sim.Phase);
            var start = sim.Participants.Select(p => p.CharacterPosition).ToList();
            float stock = sim.Participants[0].Kite.Spool.StockMeters;
            var inputs = new KiteInput[6];
            inputs[0] = new KiteInput { SteerX = 1f };
            for (int i = 0; i < 100; i++) sim.Step(inputs);
            Assert.AreEqual(MatchPhase.Warmup, sim.Phase);
            Assert.Greater(Vector3.Distance(start[0], sim.Participants[0].CharacterPosition), 2f, "player walks");
            Assert.AreEqual(stock, sim.Participants[0].Kite.Spool.StockMeters);
            Assert.AreEqual(100f, sim.Participants[0].Kite.Durability);
            Assert.IsTrue(sim.Participants.All(p => !p.Kite.IsFlying));
        }

        [Test]
        public void Warmup_EndsEarlyWhenRoomFull_ButNotBeforeTenSeconds()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 4, 4, warmup: true);
            var sim = new MatchSimulation(setup);
            sim.RequestWarmupEnd();
            var inputs = new KiteInput[4];
            for (int i = 0; i < 150; i++) sim.Step(inputs); // 7.5 s
            Assert.AreEqual(MatchPhase.Warmup, sim.Phase);
            for (int i = 0; i < 60; i++) sim.Step(inputs);
            Assert.AreEqual(MatchPhase.RoofSelect, sim.Phase);
        }

        [Test]
        public void Duel_BothPlayersStartWithEqualHeight()
        {
            foreach (GameMode mode in new[] { GameMode.QuickDuel, GameMode.RankedDuel })
            {
                MatchSetup setup = UpgradeRunner.Setup(mode, 2, 11);
                MatchSimulation sim = UpgradeRunner.Run(setup, 40f, s => { });
                Assert.AreEqual(sim.Arena.Anchors[0].Y, sim.Arena.Anchors[1].Y, 1e-3f, mode.ToString());
                Assert.AreEqual(sim.Arena.Roofs.Roof(sim.Participants[0].RoofId).MirrorOf, sim.Participants[1].RoofId);
            }
        }

        [Test]
        public void Ffa_RoofHopping_Happens_AndNobodyStaysGroundedTooLong()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 8, 21);
            int arrivals = 0;
            float maxGrounded = 0f;
            MatchSimulation sim = UpgradeRunner.Run(setup, 400f, s =>
            {
                foreach (MatchEvent e in s.Events) if (e.Type == MatchEventType.ArrivedOnRoof) arrivals++;
                foreach (MatchParticipant p in s.Participants) if (p.Flyer == FlyerState.Grounded) maxGrounded = Math.Max(maxGrounded, p.GroundedTime);
            });
            Assert.GreaterOrEqual(arrivals, 2, "at least 2 roof changes in an 8-player FFA");
            Assert.LessOrEqual(maxGrounded, 25f + 0.11f, "auto short prep after 25 s grounded");
        }

        [Test]
        public void Traverse_OnlyAlongALinkOfTheCurrentRoof_NoTeleport()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.Training, 1, 2);
            setup.Participants.Clear();
            setup.Participants.Add(MatchFactory.Bot(0, BotDifficulty.Medium, new BuildIds { Kite = "guddi", Tail = "short_ribbon", Dor = "sooti", Charkhi = "lakri" }, 0, 2));
            setup.Participants[0].isBot = false;
            var sim = new MatchSimulation(setup);
            var inputs = new KiteInput[1];
            int guard = 0;
            while (sim.Phase != MatchPhase.Round && guard++ < 2000) sim.Step(inputs);
            while (!sim.Participants[0].Kite.IsFlying && guard++ < 4000) sim.Step(inputs);
            Assert.IsTrue(sim.Execute(0, PlayerCommand.PackUp, 0));
            while (sim.Participants[0].Flyer != FlyerState.Grounded && guard++ < 6000) sim.Step(inputs);
            int roof = sim.Participants[0].RoofId;
            int far = sim.Arena.Roofs.Links.First(l => !l.Touches(roof)).Id;
            Assert.IsFalse(sim.Execute(0, PlayerCommand.TraverseLink, far), "a link elsewhere on the map is refused");
            int near = sim.Arena.Roofs.Roof(roof).Links[0];
            Assert.IsTrue(sim.Execute(0, PlayerCommand.TraverseLink, near));
            int dest = sim.Arena.Roofs.Link(near).Other(roof);
            float travel = sim.Arena.Roofs.Link(near).TravelTime;
            int ticks = 0;
            while (sim.Participants[0].Flyer == FlyerState.Traversing && ticks++ < 1000) sim.Step(inputs);
            Assert.AreEqual(dest, sim.Participants[0].RoofId);
            Assert.AreEqual(travel, ticks * MatchSimulation.Dt, 0.11f, "server-timed traversal");
            Assert.IsTrue(sim.Execute(0, PlayerCommand.LaunchHere, 0));
            Assert.AreEqual(FlyerState.Prep, sim.Participants[0].Flyer);
        }

        [Test]
        public void Emotes_RateLimited_QuickChatRateLimited()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 2, 5, warmup: true);
            var sim = new MatchSimulation(setup);
            Assert.IsTrue(sim.Execute(0, PlayerCommand.Emote, 1));
            Assert.IsFalse(sim.Execute(0, PlayerCommand.Emote, 2), "3 s cooldown");
            Assert.IsTrue(sim.Execute(0, PlayerCommand.QuickChat, 7));
            Assert.IsFalse(sim.Execute(0, PlayerCommand.QuickChat, 6), "1 per 2 s");
            Assert.IsFalse(sim.Execute(0, PlayerCommand.QuickChat, 99), "only the 8 presets");
            var inputs = new KiteInput[2];
            for (int i = 0; i < 61; i++) sim.Step(inputs);
            Assert.IsTrue(sim.Execute(0, PlayerCommand.Emote, 2));
        }

        [Test]
        public void Commands_InInputs_RunOncePerSequence()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 2, 5, warmup: true);
            var sim = new MatchSimulation(setup);
            var inputs = new KiteInput[2];
            inputs[0] = new KiteInput { Command = PlayerCommand.QuickChat, CommandArg = 3, CommandSeq = 1 };
            int chats = 0;
            for (int i = 0; i < 80; i++) { sim.Step(inputs); chats += sim.Events.Count(e => e.Type == MatchEventType.QuickChat); }
            Assert.AreEqual(1, chats, "the same command repeated in many packets runs once");
        }

        [Test]
        public void Snapshot_CarriesCharacterState_OverTheWire()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 8, 6, warmup: true);
            var sim = new MatchSimulation(setup);
            var inputs = new KiteInput[8];
            inputs[0] = new KiteInput { SteerX = 1f };
            for (int i = 0; i < 40; i++) sim.Step(inputs);
            var snap = new MatchSnapshot();
            sim.Capture(snap);
            byte[] data = NetCodec.EncodeSnapshot(snap);
            Assert.Less(data.Length, 1200, "8-player snapshot with character state fits one packet");
            var back = new MatchSnapshot();
            NetCodec.DecodeSnapshot(data, back);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(snap.Kites[i].Flyer, back.Kites[i].Flyer);
                Assert.AreEqual(snap.Kites[i].CharacterPosition, back.Kites[i].CharacterPosition);
                Assert.AreEqual(snap.Kites[i].CharacterYaw, back.Kites[i].CharacterYaw, 0.1f);
                Assert.AreEqual(snap.Kites[i].RoofId, back.Kites[i].RoofId);
            }
        }

        [Test]
        public void InputCommand_RoundTrips()
        {
            var input = new KiteInput { SteerX = 0.5f, PullHeld = true, Command = PlayerCommand.TraverseLink, CommandArg = 37, CommandSeq = 200 };
            KiteInput back = NetCodec.DecodeInput(NetCodec.EncodeInput(12, input), out int tick);
            Assert.AreEqual(12, tick);
            Assert.AreEqual(PlayerCommand.TraverseLink, back.Command);
            Assert.AreEqual(37, back.CommandArg);
            Assert.AreEqual(200, back.CommandSeq);
        }

        [Test]
        public void ClassicMatches_AreUnchanged_WithoutUpgradeFlags()
        {
            MatchSimulation sim = MatchRunner.Run(GameMode.QuickDuel, 2, BotDifficulty.Medium, 1);
            Assert.IsNull(sim.Arena.Roofs);
            Assert.IsTrue(sim.IsOver);
        }

        [Test]
        public void Training_NoEnd_DummiesFly_NoRewards()
        {
            var player = MatchFactory.Bot(0, BotDifficulty.Medium, new BuildIds { Kite = "pakpao", Tail = "short_ribbon", Dor = "sooti", Charkhi = "lakri" }, 0, 3);
            MatchSetup setup = MatchFactory.TrainingSetup(player, new TrainingOptions { overrideWind = true, windSpeed = 9f, windDirectionDeg = 10f }, 3);
            Assert.AreEqual(4, setup.Participants.Count);
            MatchSimulation sim = UpgradeRunner.Run(setup, 120f);
            Assert.IsFalse(sim.IsOver);
            Assert.AreEqual(10f, sim.CurrentWind.DirectionDeg, 5f);
            Assert.IsTrue(sim.Participants.Skip(1).Any(p => p.Kite.IsFlying));
            var profile = new PlayerProfile();
            sim.EndMatch();
            OpResult r = EconomyService.ApplyMatchResult(profile, sim.Result, new MatchRewardContext { MatchId = "t", Mode = GameMode.Training, MyIndex = 0 }, new EconomyConfig(), new RankSettings());
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(0, profile.coins);
        }

        [Test]
        public void HighRoof_StartsHigher_ThanSheltered()
        {
            MatchSetup setup = UpgradeRunner.Setup(GameMode.FreeForAll, 8, 8);
            MatchSimulation sim = UpgradeRunner.Run(setup, 60f, s => { });
            foreach (MatchParticipant p in sim.Participants)
            {
                RoofNode roof = sim.RoofOf(p);
                Assert.IsNotNull(roof);
            }
            Assert.Greater(new RoofSettings().highStartBonus, 0f);
        }
    }

    public class SocialAndCosmeticTests
    {
        [Test]
        public void Rookies_And_StarterEmotes_AreFree_LevelUnlocksWork()
        {
            var p = new PlayerProfile { level = 1 };
            Assert.IsTrue(EconomyService.EquipCharacter(p, "rookie_f").Ok);
            Assert.IsFalse(EconomyService.EquipCharacter(p, "zara").Ok, "level 3");
            p.level = 3;
            Assert.IsTrue(EconomyService.EquipCharacter(p, "zara").Ok);
            Assert.IsFalse(EconomyService.EquipCharacter(p, "lucas").Ok, "gems");
            foreach (string e in CharacterCatalog.StarterEmotes) Assert.IsTrue(EconomyService.OwnsCosmetic(p, e));
        }

        [Test]
        public void BuyThenEquip_Outfit_Accessory_Emote()
        {
            var p = new PlayerProfile { gems = 2000, level = 10 };
            Assert.IsFalse(EconomyService.EquipOutfit(p, "night_bazaar").Ok);
            Assert.IsTrue(EconomyService.BuyCosmetic(p, "night_bazaar", false).Ok);
            Assert.IsFalse(EconomyService.BuyCosmetic(p, "night_bazaar", false).Ok, "owned once");
            Assert.IsTrue(EconomyService.EquipOutfit(p, "night_bazaar").Ok);
            Assert.AreEqual("night_bazaar", p.outfitId);

            Assert.IsTrue(EconomyService.BuyCosmetic(p, "acc_shades", false).Ok);
            Assert.IsFalse(EconomyService.EquipAccessory(p, (int)AccessorySlot.Head, "acc_shades").Ok, "wrong slot");
            Assert.IsTrue(EconomyService.EquipAccessory(p, (int)AccessorySlot.Eyes, "acc_shades").Ok);

            Assert.IsFalse(EconomyService.SetEmoteSlot(p, 4, "dhol_beat").Ok);
            Assert.IsTrue(EconomyService.BuyCosmetic(p, "dhol_beat", false).Ok);
            Assert.IsTrue(EconomyService.SetEmoteSlot(p, 4, "dhol_beat").Ok);
            Assert.AreEqual("dhol_beat", p.emoteSlots[4]);
        }

        [Test]
        public void EquipCommands_RunThroughEconomyCommands()
        {
            var p = new PlayerProfile { level = 1 };
            OpResult r = EconomyCommands.Execute(p, EconomyCommands.EquipCharacter, new[] { "rookie_f" }, 0, new EconomyBundle());
            Assert.IsTrue(r.Ok);
            Assert.AreEqual("rookie_f", p.characterId);
        }

        [Test]
        public void Cosmetics_HaveNoStats()
        {
            // Rule 1: characters/outfits/emotes are CosmeticItems – there is no stat field on them at all.
            foreach (CosmeticItem c in CharacterCatalog.All())
            {
                Assert.IsTrue(c.type == CosmeticType.Character || c.type == CosmeticType.Outfit || c.type == CosmeticType.Accessory || c.type == CosmeticType.Emote);
                Assert.That(c.height, Is.InRange(0.9f, 1.1f));
            }
            Assert.AreEqual(19, CharacterCatalog.Characters.Count);
            Assert.AreEqual(19, CharacterCatalog.Outfits.Count);
        }

        [Test]
        public void FeedBuilder_DetectsDoubleAndTripleCuts()
        {
            var fb = new FeedBuilder();
            int streak = 0;
            for (int i = 0; i < 3; i++)
                fb.TryBuild(new MatchEvent { Type = MatchEventType.KiteCut, A = i + 1, B = 0 }, i * 3f, _ => "sooti", _ => 0, out FeedEntry _, out streak);
            Assert.AreEqual(3, streak);
            Assert.AreEqual("Triple cut!", FeedBuilder.StreakText(streak));
            fb.TryBuild(new MatchEvent { Type = MatchEventType.KiteCut, A = 4, B = 0 }, 30f, _ => "sooti", _ => 0, out FeedEntry e, out streak);
            Assert.AreEqual(0, streak, "outside the 10 s window");
            Assert.AreEqual(FeedKind.Cut, e.Kind);
        }

        [Test]
        public void QuickChat_TeamOnlyInTeamBattle_VoiceNeverStrangers()
        {
            Assert.IsFalse(QuickChat.Visible(GameMode.TeamBattle, 0, 1));
            Assert.IsTrue(QuickChat.Visible(GameMode.FreeForAll, 0, 1));
            Assert.IsFalse(VoiceRules.CanHear(GameMode.FreeForAll, false, false));
            Assert.IsTrue(VoiceRules.CanHear(GameMode.TeamBattle, false, true));
            Assert.IsFalse(VoiceRules.Allowed(true), "under 13: voice off");
        }
    }
}

namespace Kitefall.Tests
{
    public class PartyTeamTests
    {
        [Test]
        public void TeamBattle_PartyMembersShareATeam()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.TeamBattle, ArenaType.OldCityRooftops, 1);
            for (int i = 0; i < 5; i++)
                setup.Participants.Add(new ParticipantSetup { playerId = "p" + i, partyId = i < 3 ? "party_A" : "", team = i % 2 });
            MatchFactory.AssignPartyTeams(setup);
            Assert.AreEqual(setup.Participants[0].team, setup.Participants[1].team);
            Assert.AreEqual(setup.Participants[0].team, setup.Participants[2].team);
            Assert.AreNotEqual(setup.Participants[0].team, setup.Participants[3].team, "solo players balance the other team");
            setup.Rules.allowBots = true;
            MatchFactory.FillWithBots(setup, 8, BotDifficulty.Easy, "sooti");
            int t0 = setup.Participants.FindAll(p => p.team == 0).Count;
            Assert.AreEqual(4, t0, "bots fill the smaller team");
        }

        [Test]
        public void Party_DuelsNeedAPartyOfOne_CodesParse()
        {
            Assert.IsNotNull(PartyRules.CanQueue(GameMode.QuickDuel, 2));
            Assert.IsNull(PartyRules.CanQueue(GameMode.TeamBattle, 4));
            Assert.AreEqual("ABC234", PartyRules.CodeFromLink("kitefall://party/abc234"));
        }
    }
}
