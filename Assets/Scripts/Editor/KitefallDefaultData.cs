using System;
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
            foreach (string sub in new[] { "Kites", "Tails", "Dors", "Charkhis", "Config" }) EnsureFolder(Root + "/" + sub);

            var d = new Defaults();

            // ---- Kite classes (spec 8.1 – launch classes only; the others arrive with their update).
            d.Guddi = Kite("Guddi", "guddi", 3, 4, 3, 1.00f, 180, 15, overwrite);
            Kite("Pipa", "pipa", 5, 4, 2, 0.92f, 240, 12, overwrite);
            Kite("Rokkaku", "rokkaku", 2, 2, 5, 1.05f, 130, 18, overwrite);

            // ---- Dum (spec 8.2)
            Tail("None", "none", -5, 0.15f, -2, 0, overwrite);
            d.ShortRibbon = Tail("Short ribbon", "short_ribbon", 0, 0f, 1, 10, overwrite);
            Tail("Double ribbon", "double_ribbon", 3, -0.05f, 2, 10, overwrite);
            Tail("Long tail", "long_tail", 5, -0.15f, 3, 14, overwrite);
            Tail("Tassel", "tassel", 2, -0.05f, 1, 6, overwrite);

            // ---- Dor (spec 7.2): name, id, cut, speed, strength, rise, wear, agility, sweet, dheelBonus, longBonus, level
            d.Sooti = Dor("Sooti", "sooti", 1.00f, 1.00f, 95, 1.00f, 1.00f, 0f, 0, 1.15f, 1.10f, 1, overwrite);
            Dor("Reshmi", "reshmi", 0.95f, 1.20f, 93, 1.10f, 1.00f, 0.05f, 0, 1.15f, 1.10f, 5, overwrite);
            Dor("Tez Dhaar", "tez_dhaar", 1.10f, 1.00f, 92, 1.15f, 1.30f, 0f, 0, 1.15f, 1.10f, 10, overwrite);
            Dor("Mazboot", "mazboot", 1.00f, 0.85f, 98, 1.00f, 1.00f, -0.08f, 0, 1.15f, 1.10f, 15, overwrite);
            Dor("Lambi", "lambi", 0.95f, 0.95f, 94, 1.00f, 1.00f, 0f, 0, 1.15f, 1.15f, 20, overwrite);
            Dor("Lachakdaar", "lachakdaar", 0.97f, 0.95f, 96, 1.00f, 1.00f, 0f, 4, 1.20f, 1.10f, 25, overwrite);

            // ---- Charkhi (spec 7.3): name, id, capacity, reel, release, releaseDrop, floor, kheench, pullRise, launchDelay, level
            d.Lakri = Charkhi("Lakri", "lakri", 600, 8, 10, 1.00f, 0, 1.00f, 1.00f, 0f, 1, overwrite);
            Charkhi("Speed", "speed", 450, 14, 14, 1.20f, 0, 1.00f, 1.00f, 0f, 3, overwrite);
            Charkhi("Bari", "bari", 1000, 6, 9, 1.00f, 0, 1.00f, 1.00f, 1f, 8, overwrite);
            Charkhi("Brake", "brake", 600, 8, 7, 1.00f, 55, 1.00f, 1.00f, 0f, 12, overwrite);
            Charkhi("Gear", "gear", 550, 10, 9, 1.00f, 0, 1.05f, 1.15f, 0f, 18, overwrite);

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
