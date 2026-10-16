using System;
using Kitefall.Core;
using Kitefall.Data;
using UnityEditor;
using UnityEngine;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Creates the data assets from the spec tables (sections 7.2, 7.3, 8.1, 8.2, 14.6).
    /// Existing assets are NEVER overwritten by "Create" – your tuned values are safe.
    /// "Reset to spec values" overwrites after a confirmation dialog.
    /// </summary>
    public static class KitefallDefaultData
    {
        public const string Root = "Assets/ScriptableObjects";

        public struct Defaults
        {
            public KiteClassData Guddi;
            public TailData ShortRibbon;
            public DorData Sooti;
            public CharkhiData Lakri;
            public CombatConfig Combat;
            public FlightConfig Flight;
            public WindConfig Wind;
            public CameraConfig Camera;
            public BalanceConfig Balance;
        }

        [MenuItem("Kitefall/Data/Create Missing Data Assets", priority = 10)]
        public static void CreateMissingMenu()
        {
            EnsureDefaults(overwrite: false);
            Debug.Log("[Kitefall] Data assets are ready in " + Root);
        }

        [MenuItem("Kitefall/Data/Reset All Data Assets To Spec Values", priority = 11)]
        public static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset data assets?",
                    "This overwrites every kite, dum, dor, charkhi and config asset in " + Root +
                    " with the starting values from the spec. Your tuning will be lost.", "Reset", "Cancel")) return;
            EnsureDefaults(overwrite: true);
            Debug.Log("[Kitefall] Data assets reset to spec values.");
        }

        public static Defaults EnsureDefaults(bool overwrite)
        {
            EnsureFolder(Root);
            foreach (string sub in new[] { "Kites", "Tails", "Dors", "Charkhis", "Tunings", "Arenas", "Config" }) EnsureFolder(Root + "/" + sub);

            var d = new Defaults();

            // ---- Items from the spec tables (single source: Core.SpecDefaults)
            foreach (SpecDefaults.KiteInfo k in SpecDefaults.Kites)
            {
                KiteClassStats st = k.Stats;
                KiteClassData asset = Kite(k.Name, st.id, st.speed, st.agility, st.stability, st.classBase, st.turnRate, st.sweetZoneBase, overwrite);
                if (st.id == "guddi") d.Guddi = asset;
            }
            foreach (var kv in SpecDefaults.Tails)
            {
                TailStats t = kv.Value;
                TailData asset = Tail(SpecDefaults.NameOf(kv.Key), kv.Key, t.sweetZoneMod, t.turnMod, (int)t.stabilityMod, t.ropeSegments, overwrite);
                if (kv.Key == "short_ribbon") d.ShortRibbon = asset;
            }
            foreach (var kv in SpecDefaults.Dors)
            {
                DorStats x = kv.Value;
                DorData asset = Dor(SpecDefaults.NameOf(kv.Key), kv.Key, x.dorCut, x.dorSpeed, x.strength, x.tensionRiseMult, x.wearMult, x.agilityMod, x.sweetZoneMod, x.dheelCounterBonus, x.longRangeAngleBonus, x.unlockLevel, overwrite);
                if (kv.Key == "sooti") d.Sooti = asset;
            }
            foreach (var kv in SpecDefaults.Charkhis)
            {
                CharkhiStats x = kv.Value;
                CharkhiData asset = Charkhi(SpecDefaults.NameOf(kv.Key), kv.Key, x.capacityMeters, x.reelSpeed, x.releaseSpeed, x.releaseTensionDropMult, x.dheelTensionFloor, x.kheenchPowerMult, x.pullTensionRiseMult, x.launchDelay, x.unlockLevel, overwrite);
                if (kv.Key == "lakri") d.Lakri = asset;
            }
            foreach (var kv in SpecDefaults.Tunings)
            {
                TuningStats t = kv.Value;
                string id = kv.Key;
                Asset<TuningData>($"{Root}/Tunings/{SpecDefaults.NameOf(id).Replace(" ", "")}.asset", overwrite, x =>
                {
                    x.id = id; x.displayName = SpecDefaults.NameOf(id); x.target = t.target; x.plus = t.plus; x.minus = t.minus;
                });
            }

            // ---- Configs (field defaults = spec 14.6 starting values)
            d.Combat = Config<CombatConfig>("CombatConfig", overwrite, null);
            d.Flight = Config<FlightConfig>("FlightConfig", overwrite, null);
            d.Wind = Config<WindConfig>("Wind_OldCity", overwrite, w =>
            {
                w.settings.baseDirectionDeg = 0f;
                w.settings.baseSpeed = 6f;
                w.settings.directionVariationDeg = 20f; // "shifting wind between roofs"
            });
            d.Camera = Config<CameraConfig>("CameraConfig", overwrite, null);
            d.Balance = Config<BalanceConfig>("BalanceConfig", overwrite, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return d;
        }

        private static KiteClassData Kite(string name, string id, float speed, float agility, float stability,
            float classBase, float turnRate, int sweetZone, bool overwrite)
        {
            return Asset<KiteClassData>($"{Root}/Kites/{name}.asset", overwrite, k =>
            {
                k.id = id; k.displayName = name;
                k.speed = speed; k.agility = agility; k.stability = stability;
                k.classBase = classBase; k.turnRate = turnRate; k.sweetZoneBase = sweetZone;
            });
        }

        private static TailData Tail(string name, string id, int sweetZoneMod, float turnMod, float stabilityMod, int segments, bool overwrite)
        {
            return Asset<TailData>($"{Root}/Tails/{name.Replace(" ", "")}.asset", overwrite, t =>
            {
                t.id = id; t.displayName = name;
                t.sweetZoneMod = sweetZoneMod; t.turnMod = turnMod; t.stabilityMod = stabilityMod; t.ropeSegments = segments;
            });
        }

        private static DorData Dor(string name, string id, float cut, float speed, int strength, float rise, float wear,
            float agility, int sweet, float dheelBonus, float longBonus, int level, bool overwrite)
        {
            return Asset<DorData>($"{Root}/Dors/{name.Replace(" ", "")}.asset", overwrite, x =>
            {
                x.id = id; x.displayName = name;
                x.dorCut = cut; x.dorSpeed = speed; x.strength = strength;
                x.tensionRiseMult = rise; x.wearMult = wear; x.agilityMod = agility; x.sweetZoneMod = sweet;
                x.dheelCounterBonus = dheelBonus; x.longRangeAngleBonus = longBonus; x.unlockLevel = level;
            });
        }

        private static CharkhiData Charkhi(string name, string id, int capacity, float reel, float release, float releaseDrop,
            int floor, float kheench, float pullRise, float launchDelay, int level, bool overwrite)
        {
            return Asset<CharkhiData>($"{Root}/Charkhis/{name}.asset", overwrite, c =>
            {
                c.id = id; c.displayName = name;
                c.capacityMeters = capacity; c.reelSpeed = reel; c.releaseSpeed = release;
                c.releaseTensionDropMult = releaseDrop; c.dheelTensionFloor = floor;
                c.kheenchPowerMult = kheench; c.pullTensionRiseMult = pullRise;
                c.launchDelay = launchDelay; c.unlockLevel = level;
            });
        }

        private static T Config<T>(string name, bool overwrite, Action<T> fill) where T : ScriptableObject
        {
            return Asset($"{Root}/Config/{name}.asset", overwrite, fill);
        }

        /// <summary>Loads the asset at <paramref name="path"/>, creating (or resetting) it when needed.</summary>
        private static T Asset<T>(string path, bool overwrite, Action<T> fill) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null && !overwrite) return existing;

            T target = existing;
            if (target == null)
            {
                target = ScriptableObject.CreateInstance<T>();
                fill?.Invoke(target);
                AssetDatabase.CreateAsset(target, path);
            }
            else
            {
                // Reset to the C# field defaults first, then apply the spec values.
                T fresh = ScriptableObject.CreateInstance<T>();
                fresh.name = target.name;
                fill?.Invoke(fresh);
                EditorUtility.CopySerialized(fresh, target);
                UnityEngine.Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(target);
            }
            return target;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
