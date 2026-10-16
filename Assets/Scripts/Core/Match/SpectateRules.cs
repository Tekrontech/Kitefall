using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>Who an out player may watch (upgrade spec F5): the player who cut you first, teammates first in team modes, ◀ ▶ cycles alive players.</summary>
    public static class SpectateRules
    {
        public static bool CanSpectate(MatchSnapshot s, int me)
        {
            if (s == null || me < 0 || me >= s.Kites.Length) return false;
            KiteSnap k = s.Kites[me];
            return k.Eliminated || !k.IsFlying && k.RespawnIn > 0f;
        }

        public static List<int> Candidates(MatchSnapshot s, int me, IReadOnlyList<int> teams, bool teamMode)
        {
            var list = new List<int>();
            for (int i = 0; i < s.Kites.Length; i++)
            {
                if (i == me || s.Kites[i].Eliminated) continue;
                if (!s.Kites[i].IsFlying && s.Kites[i].Flyer != FlyerState.Prep && s.Kites[i].Flyer != FlyerState.Grounded && s.Kites[i].Flyer != FlyerState.Traversing) continue;
                list.Add(i);
            }
            if (teamMode && teams != null && me < teams.Count)
            {
                int myTeam = teams[me];
                list.Sort((a, b) =>
                {
                    bool ta = a < teams.Count && teams[a] == myTeam, tb = b < teams.Count && teams[b] == myTeam;
                    return ta != tb ? (ta ? -1 : 1) : a.CompareTo(b);
                });
            }
            return list;
        }

        public static int Default(MatchSnapshot s, int me, int cutBy, IReadOnlyList<int> teams, bool teamMode)
        {
            List<int> c = Candidates(s, me, teams, teamMode);
            if (c.Count == 0) return -1;
            if (teamMode) return c[0];
            return cutBy >= 0 && c.Contains(cutBy) ? cutBy : c[0];
        }

        public static int Cycle(MatchSnapshot s, int me, int current, int dir, IReadOnlyList<int> teams, bool teamMode)
        {
            List<int> c = Candidates(s, me, teams, teamMode);
            if (c.Count == 0) return -1;
            int i = c.IndexOf(current);
            if (i < 0) return c[0];
            return c[((i + dir) % c.Count + c.Count) % c.Count];
        }
    }
}
