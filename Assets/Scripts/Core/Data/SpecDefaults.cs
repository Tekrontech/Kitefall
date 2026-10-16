using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>
    /// Starting values from the spec tables (7.2, 7.3, 7.4, 8.1, 8.2) in one place.
    /// Used by the editor to create data assets, by tests, by bots and by the Cloud Code server module.
    /// In the game the ScriptableObject assets (and later Remote Config) are the real source.
    /// </summary>
    public static class SpecDefaults
    {
        /// <summary>Display names by id. Declared first: static fields initialise in order.</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>();

        public sealed class KiteInfo
        {
            public KiteClassStats Stats;
            public string Name;
            public string InspiredBy;
            public string Style;
            public bool Launch;
        }

        public static readonly List<KiteInfo> Kites = new List<KiteInfo>
        {
            K("guddi", "Guddi", 3, 4, 3, 1.00f, 180, 15, "Pakistan / India fighter", "Balanced", true),
            K("pipa", "Pipa", 5, 4, 2, 0.92f, 240, 12, "Brazil", "Dive, hit-and-run", true),
            K("rokkaku", "Rokkaku", 2, 2, 5, 1.05f, 130, 18, "Japan", "Strong wind, Dheel", true),
            K("pakpao", "Pakpao", 3, 5, 2, 0.92f, 280, 12, "Thailand", "Dodge, timed Kheench", false),
            K("gudiparan", "Gudiparan", 2, 2, 4, 1.08f, 130, 16, "Afghanistan", "Long contests", false),
            K("chula", "Chula", 1, 1, 5, 1.10f, 100, 18, "Thailand", "Tank", false),
            K("wau", "Wau", 3, 3, 4, 1.00f, 180, 16, "Malaysia", "Fast stall recovery", false),
        };

        public static readonly List<KeyValuePair<string, TailStats>> Tails = new List<KeyValuePair<string, TailStats>>
        {
            T("none", "None", -5, 0.15f, -2, 0),
            T("short_ribbon", "Short ribbon", 0, 0f, 1, 10),
            T("double_ribbon", "Double ribbon", 3, -0.05f, 2, 10),
            T("long_tail", "Long tail", 5, -0.15f, 3, 14),
            T("tassel", "Tassel", 2, -0.05f, 1, 6),
        };

        public static readonly List<KeyValuePair<string, DorStats>> Dors = new List<KeyValuePair<string, DorStats>>
        {
            D("sooti", "Sooti", 1.00f, 1.00f, 95, 1.00f, 1.00f, 0f, 0, 1.15f, 1.10f, 1),
            D("reshmi", "Reshmi", 0.95f, 1.20f, 93, 1.10f, 1.00f, 0.05f, 0, 1.15f, 1.10f, 5),
            D("tez_dhaar", "Tez Dhaar", 1.10f, 1.00f, 92, 1.15f, 1.30f, 0f, 0, 1.15f, 1.10f, 10),
            D("mazboot", "Mazboot", 1.00f, 0.85f, 98, 1.00f, 1.00f, -0.08f, 0, 1.15f, 1.10f, 15),
            D("lambi", "Lambi", 0.95f, 0.95f, 94, 1.00f, 1.00f, 0f, 0, 1.15f, 1.15f, 20),
            D("lachakdaar", "Lachakdaar", 0.97f, 0.95f, 96, 1.00f, 1.00f, 0f, 4, 1.20f, 1.10f, 25),
        };

        public static readonly List<KeyValuePair<string, CharkhiStats>> Charkhis = new List<KeyValuePair<string, CharkhiStats>>
        {
            C("lakri", "Lakri", 600, 8, 10, 1.00f, 0, 1.00f, 1.00f, 0f, 1),
            C("speed", "Speed", 450, 14, 14, 1.20f, 0, 1.00f, 1.00f, 0f, 3),
            C("bari", "Bari", 1000, 6, 9, 1.00f, 0, 1.00f, 1.00f, 1f, 8),
            C("brake", "Brake", 600, 8, 7, 1.00f, 55, 1.00f, 1.00f, 0f, 12),
            C("gear", "Gear", 550, 10, 9, 1.00f, 0, 1.05f, 1.15f, 0f, 18),
        };

        public static readonly List<KeyValuePair<string, TuningStats>> Tunings = new List<KeyValuePair<string, TuningStats>>
        {
            Tu("waxed_coat", "Waxed coat", TuningTarget.Dor, TuningStat.DorSpeed, 0.05f, TuningStat.Strength, -1f),
            Tu("double_twist", "Double twist", TuningTarget.Dor, TuningStat.Strength, 2f, TuningStat.DorSpeed, -0.05f),
            Tu("light_thread", "Light thread", TuningTarget.Dor, TuningStat.Agility, 0.03f, TuningStat.DorCut, -0.02f),
            Tu("oiled_axle", "Oiled axle", TuningTarget.Charkhi, TuningStat.ReelSpeed, 0.20f, TuningStat.Capacity, -50f),
            Tu("wide_drum", "Wide drum", TuningTarget.Charkhi, TuningStat.Capacity, 100f, TuningStat.ReelSpeed, -0.15f),
            Tu("soft_grip", "Soft grip", TuningTarget.Charkhi, TuningStat.SweetZone, 2f, TuningStat.KheenchPower, -0.03f),
        };

        static SpecDefaults()
        {
            foreach (KiteInfo k in Kites) Names[k.Stats.id] = k.Name;
        }

        public static KiteClassStats Kite(string id)
        {
            foreach (KiteInfo k in Kites) if (k.Stats.id == id) return k.Stats;
            return Kites[0].Stats;
        }

        public static TailStats Tail(string id) => Find(Tails, id, Tails[1].Value);
        public static DorStats Dor(string id) => Find(Dors, id, Dors[0].Value);
        public static CharkhiStats Charkhi(string id) => Find(Charkhis, id, Charkhis[0].Value);
        public static TuningStats Tuning(string id) => Find(Tunings, id, default);

        public static string NameOf(string id) => id != null && Names.TryGetValue(id, out string n) ? n : id;

        /// <summary>Builds a full match loadout from ids.</summary>
        public static ParticipantLoadout Loadout(string kite, string tail, string dor, string charkhi, float durability = 100f, float dorStock = 600f, string[] tunings = null)
        {
            TuningStats[] t = null;
            if (tunings != null)
            {
                t = new TuningStats[tunings.Length];
                for (int i = 0; i < tunings.Length; i++) t[i] = Tuning(tunings[i]);
            }
            return new ParticipantLoadout
            {
                kiteId = kite, tailId = tail, dorId = dor, charkhiId = charkhi,
                kite = Kite(kite), tail = Tail(tail), dor = Dor(dor), charkhi = Charkhi(charkhi),
                tunings = t, durability = durability, dorStockMeters = dorStock
            };
        }

        public static ParticipantLoadout FreeGuddi => Loadout("guddi", "short_ribbon", "sooti", "lakri");

        private static T Find<T>(List<KeyValuePair<string, T>> list, string id, T fallback)
        {
            foreach (var kv in list) if (kv.Key == id) return kv.Value;
            return fallback;
        }

        private static KiteInfo K(string id, string name, float speed, float agility, float stability, float classBase, float turn, int sweet, string inspired, string style, bool launch)
        {
            return new KiteInfo
            {
                Stats = new KiteClassStats { id = id, speed = speed, agility = agility, stability = stability, classBase = classBase, turnRate = turn, sweetZoneBase = sweet },
                Name = name, InspiredBy = inspired, Style = style, Launch = launch
            };
        }

        private static KeyValuePair<string, TailStats> T(string id, string name, int sweet, float turn, float stab, int seg)
        {
            Names[id] = name;
            return new KeyValuePair<string, TailStats>(id, new TailStats { id = id, sweetZoneMod = sweet, turnMod = turn, stabilityMod = stab, ropeSegments = seg });
        }

        private static KeyValuePair<string, DorStats> D(string id, string name, float cut, float speed, int strength, float rise, float wear, float agility, int sweet, float dheel, float longBonus, int level)
        {
            Names[id] = name;
            return new KeyValuePair<string, DorStats>(id, new DorStats
            {
                id = id, dorCut = cut, dorSpeed = speed, strength = strength, tensionRiseMult = rise, wearMult = wear,
                agilityMod = agility, sweetZoneMod = sweet, dheelCounterBonus = dheel, longRangeAngleBonus = longBonus, unlockLevel = level
            });
        }

        private static KeyValuePair<string, CharkhiStats> C(string id, string name, int cap, float reel, float release, float drop, int floor, float kheench, float pullRise, float delay, int level)
        {
            Names[id] = name;
            return new KeyValuePair<string, CharkhiStats>(id, new CharkhiStats
            {
                id = id, capacityMeters = cap, reelSpeed = reel, releaseSpeed = release, releaseTensionDropMult = drop,
                dheelTensionFloor = floor, kheenchPowerMult = kheench, pullTensionRiseMult = pullRise, launchDelay = delay, unlockLevel = level
            });
        }

        private static KeyValuePair<string, TuningStats> Tu(string id, string name, TuningTarget target, TuningStat plus, float plusValue, TuningStat minus, float minusValue)
        {
            Names[id] = name;
            return new KeyValuePair<string, TuningStats>(id, new TuningStats
            {
                id = id, target = target, plus = new StatModifier(plus, plusValue), minus = new StatModifier(minus, minusValue)
            });
        }
    }
}
