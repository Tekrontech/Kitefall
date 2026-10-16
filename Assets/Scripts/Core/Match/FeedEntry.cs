using System.Collections.Generic;

namespace Kitefall.Core
{
    public enum FeedKind { Cut, SelfSnap, Crash, Assist, CrateTaken, Storm, Eliminated }

    /// <summary>One kill-feed row (upgrade spec F4). Built from the existing match events – no new network message.</summary>
    public struct FeedEntry
    {
        public FeedKind Kind;
        public int Attacker;
        public int Victim;
        public AttackType Attack;
        public string DorId;
        public int TeamA;
        public int TeamB;
        public CrateContent Crate;
        public float Time;

        public bool Involves(int player) => Attacker == player || Victim == player;
    }

    /// <summary>Turns match events into feed rows and detects multi-cut streaks ("Double cut!", "Triple cut!" within 10 s).</summary>
    public sealed class FeedBuilder
    {
        public const float StreakWindow = 10f;
        private readonly Dictionary<int, List<float>> _cuts = new Dictionary<int, List<float>>();
        private readonly Dictionary<int, AttackType> _lastAttack = new Dictionary<int, AttackType>();

        /// <summary>Converts one event. Returns false for events that are not feed rows. <paramref name="streak"/> = 2, 3… when the attacker just made a multi-cut.</summary>
        public bool TryBuild(in MatchEvent e, float time, System.Func<int, string> dorOf, System.Func<int, int> teamOf, out FeedEntry entry, out int streak)
        {
            entry = default;
            streak = 0;
            switch (e.Type)
            {
                case MatchEventType.AttackLocked:
                    _lastAttack[e.A] = (AttackType)e.Code;
                    return false;
                case MatchEventType.KiteCut:
                    entry = new FeedEntry { Kind = FeedKind.Cut, Attacker = e.B, Victim = e.A, Time = time };
                    if (e.B >= 0 && _lastAttack.TryGetValue(e.B, out AttackType at)) entry.Attack = at;
                    if (e.B >= 0)
                    {
                        if (!_cuts.TryGetValue(e.B, out List<float> list)) _cuts[e.B] = list = new List<float>();
                        list.RemoveAll(t => time - t > StreakWindow);
                        list.Add(time);
                        if (list.Count >= 2) streak = list.Count;
                    }
                    break;
                case MatchEventType.SelfSnap:
                    entry = new FeedEntry { Kind = FeedKind.SelfSnap, Attacker = -1, Victim = e.A, Time = time };
                    break;
                case MatchEventType.Crash:
                    entry = new FeedEntry { Kind = FeedKind.Crash, Attacker = -1, Victim = e.A, Time = time };
                    break;
                case MatchEventType.Assist:
                    entry = new FeedEntry { Kind = FeedKind.Assist, Attacker = e.A, Victim = e.B, Time = time };
                    break;
                case MatchEventType.CrateCollected:
                    entry = new FeedEntry { Kind = FeedKind.CrateTaken, Attacker = e.A, Victim = -1, Crate = (CrateContent)e.Code, Time = time };
                    break;
                case MatchEventType.Eliminated:
                    entry = new FeedEntry { Kind = FeedKind.Eliminated, Attacker = -1, Victim = e.A, Time = time };
                    break;
                default:
                    return false;
            }
            entry.DorId = entry.Attacker >= 0 && dorOf != null ? dorOf(entry.Attacker) : "";
            entry.TeamA = entry.Attacker >= 0 && teamOf != null ? teamOf(entry.Attacker) : -1;
            entry.TeamB = entry.Victim >= 0 && teamOf != null ? teamOf(entry.Victim) : -1;
            return true;
        }

        public static string StreakText(int streak) => streak >= 4 ? "Mega cut!" : streak == 3 ? "Triple cut!" : streak == 2 ? "Double cut!" : "";
    }
}
