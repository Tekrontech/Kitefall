using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Kitefall.Core
{
    /// <summary>
    /// Compact binary encoding of inputs, snapshots and the match roster for online play (spec 14.1).
    /// Plain System.IO so it is testable here and identical on host, dedicated server and clients.
    /// </summary>
    public static class NetCodec
    {
        public const byte Version = 2;

        // ---- Inputs (client → server, 20 Hz) ---------------------------------

        public static byte[] EncodeInput(int tick, in KiteInput input)
        {
            var ms = new MemoryStream(16);
            using (var w = new BinaryWriter(ms))
            {
                w.Write(tick);
                w.Write(Q(input.SteerX));
                w.Write(Q(input.SteerY));
                byte flags = (byte)((input.PullHeld ? 1 : 0) | (input.ReelHeld ? 2 : 0) | (input.DiveHeld ? 4 : 0));
                w.Write(flags);
                w.Write((byte)input.Command);
                w.Write(input.CommandArg);
                w.Write(input.CommandSeq);
            }
            return ms.ToArray();
        }

        public static KiteInput DecodeInput(byte[] data, out int tick)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                tick = r.ReadInt32();
                var input = new KiteInput { SteerX = DQ(r.ReadSByte()), SteerY = DQ(r.ReadSByte()) };
                byte f = r.ReadByte();
                input.PullHeld = (f & 1) != 0;
                input.ReelHeld = (f & 2) != 0;
                input.DiveHeld = (f & 4) != 0;
                if (r.BaseStream.Position < r.BaseStream.Length)
                {
                    input.Command = (PlayerCommand)r.ReadByte();
                    input.CommandArg = r.ReadInt16();
                    input.CommandSeq = r.ReadByte();
                }
                return input;
            }
        }

        private static sbyte Q(float v) => (sbyte)Math.Round(KMath.Clamp(v, -1f, 1f) * 127f);
        private static float DQ(sbyte v) => v / 127f;

        // ---- Snapshots (server → clients, 20 Hz) ------------------------------

        public static byte[] EncodeSnapshot(MatchSnapshot s)
        {
            var ms = new MemoryStream(256 + s.Kites.Length * 64);
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(s.Tick);
                w.Write(s.Time);
                w.Write(s.MatchTime);
                w.Write((byte)s.Phase);
                w.Write(s.PhaseTimeLeft);
                w.Write((byte)s.RoundNumber);
                w.Write(s.RoundTime);
                w.Write((byte)s.RoundPhase);
                byte flags = (byte)((s.GustActive ? 1 : 0) | (s.GustWarning ? 2 : 0) | (s.WindStrong ? 4 : 0) | (s.BirdsActive ? 8 : 0) | (s.StormZone ? 16 : 0));
                w.Write(flags);
                w.Write(s.NextGustIn);
                w.Write(s.WindDirection);
                w.Write(s.WindSpeed);
                W(w, s.StormCenter);
                w.Write(s.StormRadius);
                w.Write((byte)s.TeamScores.Length);
                foreach (int t in s.TeamScores) w.Write((short)t);

                w.Write((byte)s.Kites.Length);
                foreach (KiteSnap k in s.Kites)
                {
                    w.Write((byte)k.Status);
                    W(w, k.Anchor);
                    w.Write(k.Azimuth); w.Write(k.Elevation); w.Write(k.Radius); w.Write(k.Heading); w.Write(k.Speed); w.Write(k.Height);
                    w.Write(k.Tension); w.Write((byte)k.Zone); w.Write((byte)k.Strength); w.Write((byte)(k.OverStrength01 * 255f));
                    w.Write(k.LineOut); w.Write((short)k.MaxLineOut); w.Write((byte)k.Durability);
                    w.Write((byte)k.Action);
                    byte kf = (byte)((k.Diving ? 1 : 0) | (k.Approaching ? 2 : 0) | (k.Connected ? 4 : 0) | (k.Eliminated ? 8 : 0) | (k.InContest ? 16 : 0));
                    w.Write(kf);
                    w.Write((byte)(k.DiveCooldown * 50f));
                    w.Write((byte)(k.RespawnIn * 20f));
                    w.Write((sbyte)k.NearestEnemy);
                    w.Write((byte)(k.LaunchHold * 50f));
                    w.Write((short)k.Score); w.Write((byte)k.RoundsWon); w.Write((short)k.Cuts); w.Write((byte)k.StackIndex);
                    w.Write(k.SweetCenter); w.Write((byte)k.SweetWidth);
                    // Upgrade v1.4 – character state
                    w.Write((byte)k.Flyer); w.Write((sbyte)k.RoofId);
                    W(w, k.CharacterPosition); w.Write((short)(k.CharacterYaw * 10f)); w.Write((byte)(KMath.Clamp(k.CharacterSpeed, 0f, 12f) * 20f));
                    w.Write((byte)k.PrepStep);
                    w.Write((byte)((k.PrepFull ? 1 : 0) | (k.PrepHelped ? 2 : 0) | ((int)k.Kanni << 2)));
                    w.Write(k.PrepStart); w.Write(k.PrepStepStart); w.Write(k.PrepBlockedUntil);
                    w.Write((byte)(KMath.Clamp(k.PrepTossHold, 0f, 5f) * 50f));
                    w.Write((byte)((k.PrepTaps & 15) | ((k.PrepSwipes & 15) << 4))); w.Write((byte)k.PrepTugs);
                    w.Write((short)k.TraverseLink); w.Write((byte)(k.TraverseProgress * 255f));
                    w.Write((byte)KMath.Clamp(k.GroundedTime * 5f, 0f, 255f)); w.Write((byte)(k.PackUp01 * 255f));
                }

                w.Write((byte)s.Contests.Count);
                foreach (ContestSnap c in s.Contests)
                {
                    w.Write((byte)c.A); w.Write((byte)c.B); w.Write(c.M); w.Write(c.Elapsed);
                    w.Write((byte)c.AttackA); w.Write((byte)c.AttackB);
                    w.Write((byte)((c.LockedA ? 1 : 0) | (c.LockedB ? 2 : 0)));
                    W(w, c.Point); w.Write(c.PowerA); w.Write(c.PowerB);
                }

                w.Write((byte)s.Crates.Count);
                foreach (CrateSnap c in s.Crates)
                {
                    w.Write((short)c.Id); W(w, c.Position); w.Write((byte)c.Content); w.Write((byte)(c.BestProgress01 * 255f));
                }

                w.Write((byte)Math.Min(255, s.Events.Count));
                for (int i = 0; i < Math.Min(255, s.Events.Count); i++)
                {
                    MatchEvent e = s.Events[i];
                    w.Write((byte)e.Type); w.Write((sbyte)e.A); w.Write((sbyte)e.B); w.Write(e.Value); w.Write(e.Code); W(w, e.Position);
                }
            }
            return ms.ToArray();
        }

        public static void DecodeSnapshot(byte[] data, MatchSnapshot s)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (r.ReadByte() != Version) throw new InvalidDataException("Snapshot version mismatch");
                s.Tick = r.ReadInt32();
                s.Time = r.ReadSingle();
                s.MatchTime = r.ReadSingle();
                s.Phase = (MatchPhase)r.ReadByte();
                s.PhaseTimeLeft = r.ReadSingle();
                s.RoundNumber = r.ReadByte();
                s.RoundTime = r.ReadSingle();
                s.RoundPhase = (RoundPhase)r.ReadByte();
                byte f = r.ReadByte();
                s.GustActive = (f & 1) != 0; s.GustWarning = (f & 2) != 0; s.WindStrong = (f & 4) != 0; s.BirdsActive = (f & 8) != 0; s.StormZone = (f & 16) != 0;
                s.NextGustIn = r.ReadSingle();
                s.WindDirection = r.ReadSingle();
                s.WindSpeed = r.ReadSingle();
                s.StormCenter = RV(r);
                s.StormRadius = r.ReadSingle();
                int teams = r.ReadByte();
                if (s.TeamScores.Length != teams) s.TeamScores = new int[teams];
                for (int i = 0; i < teams; i++) s.TeamScores[i] = r.ReadInt16();

                int n = r.ReadByte();
                if (s.Kites.Length != n) s.Kites = new KiteSnap[n];
                for (int i = 0; i < n; i++)
                {
                    var k = new KiteSnap
                    {
                        Status = (KiteStatus)r.ReadByte(),
                        Anchor = RV(r),
                        Azimuth = r.ReadSingle(), Elevation = r.ReadSingle(), Radius = r.ReadSingle(), Heading = r.ReadSingle(), Speed = r.ReadSingle(), Height = r.ReadSingle(),
                        Tension = r.ReadSingle(), Zone = (TensionZone)r.ReadByte(), Strength = r.ReadByte(), OverStrength01 = r.ReadByte() / 255f,
                        LineOut = r.ReadSingle(), MaxLineOut = r.ReadInt16(), Durability = r.ReadByte(),
                        Action = (LineAction)r.ReadByte()
                    };
                    byte kf = r.ReadByte();
                    k.Diving = (kf & 1) != 0; k.Approaching = (kf & 2) != 0; k.Connected = (kf & 4) != 0; k.Eliminated = (kf & 8) != 0; k.InContest = (kf & 16) != 0;
                    k.DiveCooldown = r.ReadByte() / 50f;
                    k.RespawnIn = r.ReadByte() / 20f;
                    k.NearestEnemy = r.ReadSByte();
                    k.LaunchHold = r.ReadByte() / 50f;
                    k.Score = r.ReadInt16(); k.RoundsWon = r.ReadByte(); k.Cuts = r.ReadInt16(); k.StackIndex = r.ReadByte();
                    k.SweetCenter = r.ReadSingle(); k.SweetWidth = r.ReadByte();
                    k.Flyer = (FlyerState)r.ReadByte(); k.RoofId = r.ReadSByte();
                    k.CharacterPosition = RV(r); k.CharacterYaw = r.ReadInt16() / 10f; k.CharacterSpeed = r.ReadByte() / 20f;
                    k.PrepStep = (PrepStep)r.ReadByte();
                    byte pf = r.ReadByte();
                    k.PrepFull = (pf & 1) != 0; k.PrepHelped = (pf & 2) != 0; k.Kanni = (PrepGrade)((pf >> 2) & 3);
                    k.PrepStart = r.ReadSingle(); k.PrepStepStart = r.ReadSingle(); k.PrepBlockedUntil = r.ReadSingle();
                    k.PrepTossHold = r.ReadByte() / 50f;
                    byte ts = r.ReadByte(); k.PrepTaps = ts & 15; k.PrepSwipes = ts >> 4; k.PrepTugs = r.ReadByte();
                    k.TraverseLink = r.ReadInt16(); k.TraverseProgress = r.ReadByte() / 255f;
                    k.GroundedTime = r.ReadByte() / 5f; k.PackUp01 = r.ReadByte() / 255f;
                    s.Kites[i] = k;
                }

                s.Contests.Clear();
                int nc = r.ReadByte();
                for (int i = 0; i < nc; i++)
                {
                    var c = new ContestSnap { A = r.ReadByte(), B = r.ReadByte(), M = r.ReadSingle(), Elapsed = r.ReadSingle(), AttackA = (AttackType)r.ReadByte(), AttackB = (AttackType)r.ReadByte() };
                    byte lf = r.ReadByte();
                    c.LockedA = (lf & 1) != 0; c.LockedB = (lf & 2) != 0;
                    c.Point = RV(r); c.PowerA = r.ReadSingle(); c.PowerB = r.ReadSingle();
                    s.Contests.Add(c);
                }

                s.Crates.Clear();
                int ncr = r.ReadByte();
                for (int i = 0; i < ncr; i++)
                    s.Crates.Add(new CrateSnap { Id = r.ReadInt16(), Position = RV(r), Content = (CrateContent)r.ReadByte(), BestProgress01 = r.ReadByte() / 255f });

                s.Events.Clear();
                int ne = r.ReadByte();
                for (int i = 0; i < ne; i++)
                    s.Events.Add(new MatchEvent { Type = (MatchEventType)r.ReadByte(), A = r.ReadSByte(), B = r.ReadSByte(), Value = r.ReadSingle(), Code = r.ReadInt32(), Position = RV(r) });
            }
        }

        private static void W(BinaryWriter w, Vector3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        private static Vector3 RV(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        // ---- Strings (roster / setup JSON produced by the Unity layer) --------

        public static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s ?? "");
        public static string Utf8(byte[] b) => b == null ? "" : Encoding.UTF8.GetString(b);
    }
}
