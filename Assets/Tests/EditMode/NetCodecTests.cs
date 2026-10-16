using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class NetCodecTests
    {
        [Test]
        public void Input_RoundTrips()
        {
            var input = new KiteInput { SteerX = 0.5f, SteerY = -1f, PullHeld = true, DiveHeld = true };
            byte[] data = NetCodec.EncodeInput(77, input);
            KiteInput back = NetCodec.DecodeInput(data, out int tick);
            Assert.AreEqual(77, tick);
            Assert.AreEqual(0.5f, back.SteerX, 0.01f);
            Assert.AreEqual(-1f, back.SteerY, 0.01f);
            Assert.IsTrue(back.PullHeld && back.DiveHeld && !back.ReelHeld);
            Assert.LessOrEqual(data.Length, 8);
        }

        [Test]
        public void Snapshot_RoundTrips_AndFitsInOnePacket()
        {
            MatchSimulation sim = MatchRunner.Run(GameMode.FreeForAll, 8, BotDifficulty.Medium, 9, maxSeconds: 90f);
            var snap = new MatchSnapshot();
            sim.Capture(snap);
            byte[] data = NetCodec.EncodeSnapshot(snap);
            var back = new MatchSnapshot();
            NetCodec.DecodeSnapshot(data, back);
            Assert.AreEqual(snap.Tick, back.Tick);
            Assert.AreEqual(snap.Phase, back.Phase);
            Assert.AreEqual(snap.Kites.Length, back.Kites.Length);
            for (int i = 0; i < snap.Kites.Length; i++)
            {
                Assert.AreEqual(snap.Kites[i].Status, back.Kites[i].Status);
                Assert.AreEqual(snap.Kites[i].Azimuth, back.Kites[i].Azimuth, 1e-4f);
                Assert.AreEqual(snap.Kites[i].Tension, back.Kites[i].Tension, 1e-4f);
                Assert.AreEqual(snap.Kites[i].Score, back.Kites[i].Score);
            }
            Assert.Less(data.Length, 1200, "8-player snapshot must fit in one unreliable packet");
        }

        [Test]
        public void MatchResultDto_RoundTrips()
        {
            MatchSimulation sim = MatchRunner.Run(GameMode.QuickDuel, 2, BotDifficulty.Medium, 3);
            MatchResultDto dto = MatchResultDto.From(sim.Result, "m1");
            Assert.IsNull(dto.Validate(ModeRules.For(GameMode.QuickDuel)));
            MatchResult back = dto.ToResult();
            Assert.AreEqual(sim.Result.WinnerIndex, back.WinnerIndex);
            Assert.AreEqual(sim.Result.Participants[0].Cuts, back.Participants[0].Cuts);
            dto.participants[0].cuts = 999;
            Assert.IsNotNull(dto.Validate(ModeRules.For(GameMode.QuickDuel)), "Implausible reports are rejected");
        }
    }
}
