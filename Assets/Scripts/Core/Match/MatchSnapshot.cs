using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>Everything the view / HUD needs about one kite on one tick. Also what the server sends to clients.</summary>
    public struct KiteSnap
    {
        public KiteStatus Status;
        public Vector3 Anchor;
        public float Azimuth;
        public float Elevation;
        public float Radius;
        public float Heading;
        public float Speed;
        public float Height;
        public float Tension;
        public TensionZone Zone;
        public float Strength;
        public float OverStrength01;
        public float LineOut;
        public float MaxLineOut;
        public float Durability;
        public LineAction Action;
        public bool Diving;
        public float DiveCooldown;
        public float RespawnIn;
        public bool Approaching;
        public int NearestEnemy;
        public float LaunchHold;
        public bool Connected;
        public bool Eliminated;
        public int Score;
        public int RoundsWon;
        public int Cuts;
        public int StackIndex;
        public bool InContest;
        public float SweetCenter;
        public float SweetWidth;

        // ---- Upgrade v1.4: the character (flyer) – replicated as state + timestamps, not frame by frame
        public FlyerState Flyer;
        public int RoofId;
        public Vector3 CharacterPosition;
        public float CharacterYaw;
        public float CharacterSpeed;
        public PrepStep PrepStep;
        public bool PrepFull;
        public float PrepStart;
        public float PrepStepStart;
        public float PrepBlockedUntil;
        public float PrepTossHold;
        public int PrepTaps;
        public int PrepTugs;
        public int PrepSwipes;
        public PrepGrade Kanni;
        public bool PrepHelped;
        public int TraverseLink;
        public float TraverseProgress;
        public float GroundedTime;
        public float PackUp01;

        public Vector3 Direction => KiteFlightSim.DirectionOf(Azimuth, Elevation);
        public Vector3 Nose => KiteFlightSim.NoseOf(Azimuth, Elevation, Heading);
        public Vector3 WorldPosition => Anchor + Direction * Radius;
        public bool IsFlying => Status == KiteStatus.Flying;
    }

    public struct ContestSnap
    {
        public int A;
        public int B;
        public float M;
        public float Elapsed;
        public AttackType AttackA;
        public AttackType AttackB;
        public bool LockedA;
        public bool LockedB;
        public Vector3 Point;
        public float PowerA;
        public float PowerB;
    }

    public struct CrateSnap
    {
        public int Id;
        public Vector3 Position;
        public CrateContent Content;
        public float BestProgress01;
    }

    /// <summary>A full picture of the match on one tick (offline view, online replication, replays).</summary>
    public sealed class MatchSnapshot
    {
        public int Tick;
        public float Time;
        public float MatchTime;
        public MatchPhase Phase;
        public float PhaseTimeLeft;
        public int RoundNumber;
        public float RoundTime;
        public RoundPhase RoundPhase;
        public bool GustActive;
        public bool GustWarning;
        public float NextGustIn;
        public float WindDirection;
        public float WindSpeed;
        public bool WindStrong;
        public bool BirdsActive;
        public Vector3 StormCenter;
        public float StormRadius;
        public bool StormZone;
        public int[] TeamScores = new int[0];
        public KiteSnap[] Kites = new KiteSnap[0];
        public List<ContestSnap> Contests = new List<ContestSnap>();
        public List<CrateSnap> Crates = new List<CrateSnap>();
        public List<MatchEvent> Events = new List<MatchEvent>();

        public ContestSnap? ContestOf(int kite)
        {
            foreach (ContestSnap c in Contests)
                if (c.A == kite || c.B == kite) return c;
            return null;
        }
    }

    public sealed partial class MatchSimulation
    {
        /// <summary>Copies the current state into <paramref name="snap"/> (re-uses its arrays).</summary>
        public void Capture(MatchSnapshot snap)
        {
            snap.Tick = Tick;
            snap.Time = Time;
            snap.MatchTime = MatchTime;
            snap.Phase = Phase;
            snap.PhaseTimeLeft = PhaseTimeLeft;
            snap.RoundNumber = RoundNumber;
            snap.RoundTime = RoundTime;
            snap.RoundPhase = Phases.Phase;
            snap.GustActive = Phases.GustActive;
            snap.GustWarning = Phases.GustWarning;
            snap.NextGustIn = Phases.Phase == RoundPhase.Gust ? KMath.Max(0f, Phases.NextGustAt - RoundTime) : -1f;
            snap.WindDirection = CurrentWind.DirectionDeg;
            snap.WindSpeed = CurrentWind.Speed;
            snap.WindStrong = CurrentWind.IsStrong;
            snap.BirdsActive = BirdsActive;
            snap.StormCenter = StormCenter;
            snap.StormRadius = StormRadius;
            snap.StormZone = Rules.stormZone;
            if (snap.TeamScores.Length != TeamScores.Length) snap.TeamScores = new int[TeamScores.Length];
            System.Array.Copy(TeamScores, snap.TeamScores, TeamScores.Length);

            if (snap.Kites.Length != _participants.Count) snap.Kites = new KiteSnap[_participants.Count];
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                KiteSimulation k = p.Kite;
                SweetZoneBand? band = SweetZoneFor(i);
                snap.Kites[i] = new KiteSnap
                {
                    Status = k.Status,
                    Anchor = k.Anchor,
                    Azimuth = k.Flight.AzimuthDeg,
                    Elevation = k.Flight.ElevationDeg,
                    Radius = k.Flight.Radius,
                    Heading = k.Flight.HeadingDeg,
                    Speed = k.Flight.Speed,
                    Height = k.Flight.Height,
                    Tension = k.Tension.Tension,
                    Zone = k.Tension.Zone,
                    Strength = k.Tension.Strength,
                    OverStrength01 = k.Tension.SnapHoldTime > 0f ? KMath.Clamp01(k.Tension.OverStrengthTime / k.Tension.SnapHoldTime) : 0f,
                    LineOut = k.Spool.LineOut,
                    MaxLineOut = k.Spool.MaxLineOut,
                    Durability = k.Durability,
                    Action = k.CurrentAction,
                    Diving = k.Flight.IsDiving,
                    DiveCooldown = k.Flight.DiveCooldownLeft,
                    RespawnIn = p.RespawnTimer,
                    Approaching = Contacts.IsApproaching(i),
                    NearestEnemy = Contacts.NearestOther(i),
                    LaunchHold = p.LaunchHold,
                    Connected = p.Connected,
                    Eliminated = p.Eliminated,
                    Score = p.Score,
                    RoundsWon = p.RoundsWon,
                    Cuts = p.Cuts,
                    StackIndex = p.StackIndex,
                    InContest = band.HasValue,
                    SweetCenter = band?.Center ?? -1f,
                    SweetWidth = band?.Width ?? k.Loadout.SweetZone,
                    Flyer = p.Flyer,
                    RoofId = p.RoofId,
                    CharacterPosition = p.CharacterPosition,
                    CharacterYaw = p.CharacterYaw,
                    CharacterSpeed = p.CharacterSpeed,
                    PrepStep = p.Prep?.Step ?? PrepStep.Flying,
                    PrepFull = p.Prep?.Full ?? false,
                    PrepStart = p.Prep?.StartTime ?? 0f,
                    PrepStepStart = p.Prep?.StepStartTime ?? 0f,
                    PrepBlockedUntil = p.Prep?.BlockedUntil ?? 0f,
                    PrepTossHold = p.Prep?.TossHold ?? 0f,
                    PrepTaps = p.Prep?.KanniTapsDone ?? 0,
                    PrepTugs = p.Prep?.TugsDone ?? 0,
                    PrepSwipes = p.Prep?.SwipesDone ?? 0,
                    Kanni = p.Prep != null && p.Prep.Full ? p.Prep.KanniGrade : p.LastKanni,
                    PrepHelped = p.Prep?.HelperPresent ?? false,
                    TraverseLink = p.Flyer == FlyerState.Traversing ? p.TraverseLink : -1,
                    TraverseProgress = p.Flyer == FlyerState.Traversing ? 1f - KMath.Clamp01(p.TraverseLeft / KMath.Max(0.01f, p.TraverseTotal)) : 0f,
                    GroundedTime = p.GroundedTime,
                    PackUp01 = p.Flyer == FlyerState.PackingUp ? 1f - KMath.Clamp01(p.PackUpLeft / KMath.Max(0.01f, p.PackUpTotal)) : 0f
                };
            }

            snap.Contests.Clear();
            foreach (Contest c in _contests)
            {
                if (c.IsOver) continue;
                snap.Contests.Add(new ContestSnap
                {
                    A = c.A, B = c.B, M = c.M, Elapsed = c.Elapsed,
                    AttackA = c.AttackA, AttackB = c.AttackB, LockedA = c.LockedA, LockedB = c.LockedB,
                    Point = c.Point, PowerA = c.LastA.Total, PowerB = c.LastB.Total
                });
            }

            snap.Crates.Clear();
            foreach (SkyCrate c in _crates)
            {
                if (c.Taken) continue;
                float best = 0f;
                foreach (float pr in c.Progress) best = KMath.Max(best, pr);
                snap.Crates.Add(new CrateSnap { Id = c.Id, Position = c.Position, Content = c.Content, BestProgress01 = KMath.Clamp01(best / Setup.Crates.collectTime) });
            }

            snap.Events.Clear();
            snap.Events.AddRange(_events);
        }
    }
}
