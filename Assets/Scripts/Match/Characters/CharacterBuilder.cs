using System.Collections.Generic;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>The bones of a generated humanoid. Limbs hang along local −Y in the rest pose.</summary>
    public sealed class CharacterRig
    {
        public Transform Root, Hips, Spine, Chest, Neck, Head;
        public Transform UpperArmL, LowerArmL, HandL, UpperArmR, LowerArmR, HandR;
        public Transform UpperLegL, LowerLegL, FootL, UpperLegR, LowerLegR, FootR;
        public Transform LineAnchor, CharkhiSocket, HeldKiteSocket, BackSocket;
        public Transform Charkhi, CharkhiSpool, HeldKite;
        public float UpperArm = 0.29f, LowerArm = 0.27f, UpperLeg = 0.45f, LowerLeg = 0.43f;
        public float HipHeight = 0.95f;
        public float Scale = 1f;
        public readonly List<Renderer> Renderers = new List<Renderer>();
    }

    /// <summary>
    /// Builds a premium-stylised humanoid from code (upgrade spec 0.1 / W1): body proportions within humanoid limits,
    /// skin and hair colours, hair styles, and clothing as separate meshes (kurta, jacket, hoodie, waistcoat, festival wear,
    /// sports kit, shoes) with cloth-like materials. Accessories attach to bones by slot; a detailed charkhi sits in the right hand.
    /// </summary>
    public static class CharacterBuilder
    {
        public struct Look
        {
            public Color Skin, Hair, Primary, Secondary, Trim, Shoes;
            public int HairStyle, OutfitStyle;
            public float Height, Build;
            public bool Feminine;
            public string[] Accessories;
            public string CharkhiId;
            public string KiteId, KiteSkin;
        }

        public static Look LookFor(string characterId, string outfitId, string[] accessories, string charkhiId, string kiteId = "guddi", string kiteSkin = "")
        {
            CosmeticItem ch = CharacterCatalog.Character(characterId);
            string outfit = string.IsNullOrEmpty(outfitId) ? ch.forItem : outfitId;
            CosmeticItem o = CharacterCatalog.Outfit(outfit) ?? CharacterCatalog.Outfits[0];
            return new Look
            {
                Skin = KiteVisualFactory.Hex(ch.skinHex),
                Hair = KiteVisualFactory.Hex(ch.hairHex),
                HairStyle = ch.style,
                Height = ch.height,
                Build = ch.build,
                Feminine = ch.style >= 3 && ch.style != 6,
                Primary = KiteVisualFactory.Hex(o.primaryHex),
                Secondary = KiteVisualFactory.Hex(o.secondaryHex),
                Trim = Color.Lerp(KiteVisualFactory.Hex(o.secondaryHex), Color.white, 0.25f),
                Shoes = o.style == 5 ? Color.white : new Color(0.32f, 0.22f, 0.15f),
                OutfitStyle = o.style,
                Accessories = accessories ?? new string[4],
                CharkhiId = string.IsNullOrEmpty(charkhiId) ? "lakri" : charkhiId,
                KiteId = kiteId,
                KiteSkin = kiteSkin
            };
        }

        public static CharacterRig Build(Transform parent, Look look)
        {
            var rig = new CharacterRig();
            float h = Mathf.Clamp(look.Height, 0.9f, 1.1f);
            float b = Mathf.Clamp(look.Build, 0.85f, 1.15f);
            rig.Scale = h;
            var root = new GameObject("Character").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * h;
            rig.Root = root;

            Material skin = Materials.Lit(look.Skin, 0.32f);
            Material hair = Materials.Lit(look.Hair, 0.45f);
            Material cloth = Materials.Lit(look.Primary, 0.08f);
            Material cloth2 = Materials.Lit(look.Secondary, 0.1f);
            Material trim = Materials.Lit(look.Trim, 0.2f);
            Material shoes = Materials.Lit(look.Shoes, 0.25f);
            Material dark = Materials.Lit(new Color(0.08f, 0.07f, 0.07f), 0.6f);
            Material eyeWhite = Materials.Lit(new Color(0.96f, 0.95f, 0.92f), 0.5f);

            // ---- Skeleton
            rig.Hips = Bone(root, "Hips", new Vector3(0f, rig.HipHeight, 0f));
            rig.Spine = Bone(rig.Hips, "Spine", new Vector3(0f, 0.1f, 0f));
            rig.Chest = Bone(rig.Spine, "Spine2", new Vector3(0f, 0.17f, 0f));
            rig.Neck = Bone(rig.Chest, "Neck", new Vector3(0f, 0.24f, 0f));
            rig.Head = Bone(rig.Neck, "Head", new Vector3(0f, 0.08f, 0f));
            float sh = 0.185f * b * (look.Feminine ? 0.93f : 1f);
            rig.UpperArmL = Bone(rig.Chest, "UpperArmL", new Vector3(-sh, 0.2f, 0f));
            rig.LowerArmL = Bone(rig.UpperArmL, "LowerArmL", new Vector3(0f, -rig.UpperArm, 0f));
            rig.HandL = Bone(rig.LowerArmL, "LeftHand", new Vector3(0f, -rig.LowerArm, 0f));
            rig.UpperArmR = Bone(rig.Chest, "UpperArmR", new Vector3(sh, 0.2f, 0f));
            rig.LowerArmR = Bone(rig.UpperArmR, "LowerArmR", new Vector3(0f, -rig.UpperArm, 0f));
            rig.HandR = Bone(rig.LowerArmR, "RightHand", new Vector3(0f, -rig.LowerArm, 0f));
            float hip = 0.095f * b;
            rig.UpperLegL = Bone(rig.Hips, "UpperLegL", new Vector3(-hip, -0.02f, 0f));
            rig.LowerLegL = Bone(rig.UpperLegL, "LowerLegL", new Vector3(0f, -rig.UpperLeg, 0f));
            rig.FootL = Bone(rig.LowerLegL, "FootL", new Vector3(0f, -rig.LowerLeg, 0f));
            rig.UpperLegR = Bone(rig.Hips, "UpperLegR", new Vector3(hip, -0.02f, 0f));
            rig.LowerLegR = Bone(rig.UpperLegR, "LowerLegR", new Vector3(0f, -rig.UpperLeg, 0f));
            rig.FootR = Bone(rig.LowerLegR, "FootR", new Vector3(0f, -rig.LowerLeg, 0f));
            rig.LineAnchor = Bone(rig.HandL, "LineAnchor", new Vector3(0f, -0.07f, 0.02f));
            rig.CharkhiSocket = Bone(rig.HandR, "CharkhiSocket", new Vector3(0f, -0.06f, 0.03f));
            rig.BackSocket = Bone(rig.Chest, "BackSocket", new Vector3(0f, 0.08f, -0.14f));
            rig.HeldKiteSocket = Bone(root, "HeldKite", new Vector3(0f, 1.2f, 0.45f));

            // ---- Body (skin)
            Mesh torso = MeshKit.Cached($"torso_{b:0.00}_{look.Feminine}", () => Torso(b, look.Feminine));
            Add(rig, MeshKit.Part(rig.Hips, "Torso", torso, skin));
            Add(rig, MeshKit.Part(rig.Neck, "NeckMesh", MeshKit.Cached("neck", () => MeshKit.Limb(0.1f, 0.05f, 0.058f)), skin, new Vector3(0f, 0.1f, 0f)));
            Head(rig, look, skin, hair, dark, eyeWhite);
            Arm(rig, rig.UpperArmL, rig.LowerArmL, rig.HandL, skin, b, true);
            Arm(rig, rig.UpperArmR, rig.LowerArmR, rig.HandR, skin, b, false);
            Leg(rig, rig.UpperLegL, rig.LowerLegL, rig.FootL, skin, shoes, b);
            Leg(rig, rig.UpperLegR, rig.LowerLegR, rig.FootR, skin, shoes, b);

            // ---- Clothing
            Outfit(rig, look, cloth, cloth2, trim, b);
            foreach (string acc in look.Accessories ?? new string[0]) Accessory(rig, acc);

            // One GPU-skinned mesh per character (draw-call budget, upgrade 12).
            var exclude = new HashSet<Transform> { rig.HeldKiteSocket, rig.LineAnchor, rig.CharkhiSocket };
            SkinnedMeshRenderer body = CharacterSkinner.Bake(rig, exclude);
            if (body != null)
            {
                rig.Renderers.Clear();
                rig.Renderers.Add(body);
            }

            // ---- Charkhi in the right hand, kite for the ritual
            rig.Charkhi = BuildCharkhi(rig.CharkhiSocket, look.CharkhiId, out rig.CharkhiSpool);
            Collect(rig, rig.Charkhi);
            rig.HeldKite = KiteVisualFactory.Build(look.KiteId ?? "guddi", look.KiteSkin ?? "", rig.HeldKiteSocket).transform;
            rig.HeldKite.localScale = Vector3.one * 0.55f;
            Collect(rig, rig.HeldKite);
            return rig;
        }

        private static Transform Bone(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        private static void Add(CharacterRig rig, Transform part)
        {
            Renderer r = part.GetComponent<Renderer>();
            if (r != null) rig.Renderers.Add(r);
        }

        private static void Collect(CharacterRig rig, Transform t)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) rig.Renderers.Add(r);
        }

        // ---- Body parts --------------------------------------------------------

        private static Mesh Torso(float b, bool fem)
        {
            float w = b, hipW = fem ? 1.08f : 1f, chestW = fem ? 0.94f : 1f;
            return MeshKit.Loft(new[]
            {
                new MeshKit.Ring(-0.1f, 0.13f * w * hipW, 0.09f * w),
                new MeshKit.Ring(-0.02f, 0.15f * w * hipW, 0.1f * w),
                new MeshKit.Ring(0.08f, 0.14f * w * hipW, 0.095f * w),
                new MeshKit.Ring(0.18f, 0.125f * w, 0.088f * w, 0.005f),
                new MeshKit.Ring(0.3f, 0.15f * w * chestW, 0.1f * w, fem ? 0.02f : 0.012f),
                new MeshKit.Ring(0.4f, 0.17f * w * chestW, 0.1f * w, 0.008f),
                new MeshKit.Ring(0.47f, 0.15f * w, 0.085f * w),
                new MeshKit.Ring(0.52f, 0.07f, 0.06f)
            }, 18);
        }

        private static void Head(CharacterRig rig, Look look, Material skin, Material hair, Material dark, Material eyeWhite)
        {
            Transform head = rig.Head;
            float hs = look.Feminine ? 0.97f : 1f;
            Add(rig, MeshKit.Part(head, "Skull", MeshKit.Cached($"skull{hs}", () => Skull(hs)), skin, new Vector3(0f, 0.11f, 0f)));
            // Eyes, brows, nose, ears – simple, readable at phone distance.
            foreach (float side in new[] { -1f, 1f })
            {
                Add(rig, MeshKit.Part(head, "EyeWhite", MeshKit.Cached("eyew", () => MeshKit.Ellipsoid(0.021f, 0.014f, 0.01f, 10, 6)), eyeWhite, new Vector3(side * 0.04f, 0.125f, 0.092f)));
                Add(rig, MeshKit.Part(head, "Pupil", MeshKit.Cached("pupil", () => MeshKit.Ellipsoid(0.011f, 0.012f, 0.006f, 8, 5)), dark, new Vector3(side * 0.04f, 0.124f, 0.1f)));
                Add(rig, MeshKit.Part(head, "Brow", MeshKit.Cached("brow", () => MeshKit.Box(new Vector3(0.038f, 0.008f, 0.01f))), MaterialsHair(look), new Vector3(side * 0.041f, 0.152f, 0.095f), Quaternion.Euler(0f, 0f, side * -8f)));
                Add(rig, MeshKit.Part(head, "Ear", MeshKit.Cached("ear", () => MeshKit.Ellipsoid(0.012f, 0.026f, 0.018f, 8, 6)), skin, new Vector3(side * 0.098f, 0.115f, 0f)));
            }
            Add(rig, MeshKit.Part(head, "Nose", MeshKit.Cached("nose", () => MeshKit.Ellipsoid(0.014f, 0.024f, 0.018f, 8, 6)), skin, new Vector3(0f, 0.1f, 0.1f), Quaternion.Euler(-10f, 0f, 0f)));
            Add(rig, MeshKit.Part(head, "Mouth", MeshKit.Cached("mouth", () => MeshKit.Box(new Vector3(0.04f, 0.007f, 0.006f))), Materials.Lit(Color.Lerp(look.Skin, new Color(0.5f, 0.15f, 0.15f), 0.55f), 0.3f), new Vector3(0f, 0.066f, 0.094f)));
            Hair(rig, look, hair);
        }

        private static Material MaterialsHair(Look look) => Materials.Lit(Color.Lerp(look.Hair, Color.black, 0.2f), 0.4f);

        private static Mesh Skull(float s) => MeshKit.Loft(new[]
        {
            new MeshKit.Ring(-0.095f, 0.025f * s, 0.03f * s, 0.035f),
            new MeshKit.Ring(-0.085f, 0.05f * s, 0.055f * s, 0.03f),
            new MeshKit.Ring(-0.06f, 0.075f * s, 0.08f * s, 0.015f),
            new MeshKit.Ring(-0.02f, 0.09f * s, 0.095f * s, 0.005f),
            new MeshKit.Ring(0.03f, 0.097f * s, 0.105f * s),
            new MeshKit.Ring(0.07f, 0.092f * s, 0.103f * s, -0.004f),
            new MeshKit.Ring(0.1f, 0.072f * s, 0.085f * s, -0.008f),
            new MeshKit.Ring(0.12f, 0.04f * s, 0.05f * s, -0.01f),
            new MeshKit.Ring(0.13f, 0.005f, 0.005f, -0.01f)
        }, 18);

        private static void Hair(CharacterRig rig, Look look, Material hair)
        {
            Transform head = rig.Head;
            Vector3 c = new Vector3(0f, 0.115f, -0.004f);
            switch (look.HairStyle)
            {
                case 0: // short crop
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair0", () => MeshKit.Ellipsoid(0.103f, 0.11f, 0.112f, 18, 8, -5f, 90f)), hair, c + new Vector3(0f, 0.012f, -0.006f)));
                    break;
                case 1: // side part, a bit longer
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair1", () => MeshKit.Ellipsoid(0.106f, 0.118f, 0.116f, 18, 8, -15f, 90f)), hair, c + new Vector3(0.004f, 0.014f, -0.008f), Quaternion.Euler(0f, 0f, -4f)));
                    Add(rig, MeshKit.Part(head, "Fringe", MeshKit.Cached("fringe", () => MeshKit.Ellipsoid(0.06f, 0.025f, 0.03f, 10, 6)), hair, new Vector3(0.03f, 0.205f, 0.075f), Quaternion.Euler(20f, 0f, -15f)));
                    break;
                case 2: // curly
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair0", () => MeshKit.Ellipsoid(0.103f, 0.11f, 0.112f, 18, 8, -5f, 90f)), hair, c + new Vector3(0f, 0.012f, -0.006f)));
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i / 9f * Mathf.PI * 2f;
                        Add(rig, MeshKit.Part(head, "Curl", MeshKit.Cached("curl", () => MeshKit.Ellipsoid(0.035f, 0.032f, 0.035f, 8, 6)), hair, c + new Vector3(Mathf.Sin(a) * 0.075f, 0.075f + Mathf.Cos(a * 2f) * 0.01f, Mathf.Cos(a) * 0.075f - 0.01f)));
                    }
                    break;
                case 3: // long straight
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair3", () => MeshKit.Ellipsoid(0.108f, 0.118f, 0.118f, 18, 8, -25f, 90f)), hair, c + new Vector3(0f, 0.012f, -0.008f)));
                    Add(rig, MeshKit.Part(head, "HairBack", MeshKit.Cached("hairback", () => MeshKit.Loft(new[] { new MeshKit.Ring(-0.3f, 0.09f, 0.035f, -0.02f), new MeshKit.Ring(-0.12f, 0.11f, 0.05f, -0.03f), new MeshKit.Ring(0f, 0.1f, 0.06f, -0.03f) }, 14)), hair, c + new Vector3(0f, 0f, -0.06f)));
                    break;
                case 4: // bun
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair0", () => MeshKit.Ellipsoid(0.103f, 0.11f, 0.112f, 18, 8, -5f, 90f)), hair, c + new Vector3(0f, 0.012f, -0.006f)));
                    Add(rig, MeshKit.Part(head, "Bun", MeshKit.Cached("bun", () => MeshKit.Ellipsoid(0.05f, 0.045f, 0.05f, 12, 8)), hair, c + new Vector3(0f, 0.1f, -0.07f)));
                    break;
                case 5: // bob
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair5", () => MeshKit.Ellipsoid(0.115f, 0.122f, 0.122f, 18, 9, -40f, 90f)), hair, c + new Vector3(0f, 0.008f, -0.006f)));
                    break;
                case 6: // short + beard
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair0", () => MeshKit.Ellipsoid(0.103f, 0.11f, 0.112f, 18, 8, -5f, 90f)), hair, c + new Vector3(0f, 0.012f, -0.006f)));
                    Add(rig, MeshKit.Part(head, "Beard", MeshKit.Cached("beard", () => MeshKit.Ellipsoid(0.085f, 0.06f, 0.07f, 14, 8, -90f, 20f)), hair, new Vector3(0f, 0.065f, 0.03f)));
                    break;
                default: // ponytail
                    Add(rig, MeshKit.Part(head, "Hair", MeshKit.Cached("hair0", () => MeshKit.Ellipsoid(0.103f, 0.11f, 0.112f, 18, 8, -5f, 90f)), hair, c + new Vector3(0f, 0.012f, -0.006f)));
                    Add(rig, MeshKit.Part(head, "Ponytail", MeshKit.Cached("pony", () => MeshKit.Limb(0.22f, 0.035f, 0.02f, 10)), hair, c + new Vector3(0f, 0.06f, -0.1f), Quaternion.Euler(-25f, 0f, 0f)));
                    break;
            }
        }

        private static void Arm(CharacterRig rig, Transform upper, Transform lower, Transform hand, Material skin, float b, bool left)
        {
            Add(rig, MeshKit.Part(upper, "UpperArmMesh", MeshKit.Cached($"uarm{b:0.00}", () => MeshKit.Limb(rig.UpperArm, 0.046f * b, 0.038f * b)), skin));
            Add(rig, MeshKit.Part(lower, "LowerArmMesh", MeshKit.Cached($"larm{b:0.00}", () => MeshKit.Limb(rig.LowerArm, 0.037f * b, 0.028f * b)), skin));
            Add(rig, MeshKit.Part(hand, "Palm", MeshKit.Cached("palm", () => MeshKit.Ellipsoid(0.03f, 0.05f, 0.017f, 10, 7)), skin, new Vector3(0f, -0.045f, 0f)));
            Add(rig, MeshKit.Part(hand, "Thumb", MeshKit.Cached("thumb", () => MeshKit.Limb(0.04f, 0.011f, 0.009f, 6)), skin, new Vector3(left ? 0.025f : -0.025f, -0.02f, 0.012f), Quaternion.Euler(0f, 0f, left ? 35f : -35f)));
        }

        private static void Leg(CharacterRig rig, Transform upper, Transform lower, Transform foot, Material skin, Material shoes, float b)
        {
            Add(rig, MeshKit.Part(upper, "UpperLegMesh", MeshKit.Cached($"uleg{b:0.00}", () => MeshKit.Limb(rig.UpperLeg, 0.075f * b, 0.052f * b)), skin));
            Add(rig, MeshKit.Part(lower, "LowerLegMesh", MeshKit.Cached($"lleg{b:0.00}", () => MeshKit.Limb(rig.LowerLeg, 0.052f * b, 0.036f * b)), skin));
            Add(rig, MeshKit.Part(foot, "Shoe", MeshKit.Cached("shoe", () => MeshKit.Ellipsoid(0.048f, 0.04f, 0.115f, 12, 8, -10f, 90f)), shoes, new Vector3(0f, -0.035f, 0.05f)));
            Add(rig, MeshKit.Part(foot, "Sole", MeshKit.Cached("sole", () => MeshKit.Box(new Vector3(0.095f, 0.018f, 0.23f))), Materials.Lit(new Color(0.15f, 0.13f, 0.12f), 0.2f), new Vector3(0f, -0.04f, 0.05f)));
        }

        // ---- Clothing ----------------------------------------------------------

        private static void Outfit(CharacterRig rig, Look look, Material cloth, Material cloth2, Material trim, float b)
        {
            int style = look.OutfitStyle;
            bool longTunic = style == 0 || style == 3 || style == 4;
            bool shortSleeve = style == 5;
            // Upper garment (shell slightly larger than the torso).
            float s = 1.07f;
            Mesh shirt = MeshKit.Cached($"shirt{b:0.00}_{look.Feminine}_{longTunic}_{style}", () => MeshKit.Loft(new[]
            {
                new MeshKit.Ring(longTunic ? -0.62f : -0.14f, (longTunic ? 0.26f : 0.165f) * b, (longTunic ? 0.2f : 0.11f) * b),
                new MeshKit.Ring(longTunic ? -0.3f : -0.06f, (longTunic ? 0.2f : 0.16f) * b * s, (longTunic ? 0.14f : 0.108f) * b),
                new MeshKit.Ring(0.08f, 0.15f * b * s, 0.102f * b * s),
                new MeshKit.Ring(0.18f, 0.135f * b * s, 0.095f * b * s, 0.005f),
                new MeshKit.Ring(0.3f, 0.158f * b * s, 0.108f * b * s, 0.012f),
                new MeshKit.Ring(0.4f, 0.178f * b * s, 0.108f * b * s, 0.008f),
                new MeshKit.Ring(0.47f, 0.158f * b * s, 0.092f * b * s),
                new MeshKit.Ring(0.515f, 0.075f, 0.066f)
            }, 20, longTunic, true));
            Add(rig, MeshKit.Part(rig.Hips, "Shirt", shirt, cloth));
            // Collar / neckline trim.
            Add(rig, MeshKit.Part(rig.Chest, "Collar", MeshKit.Cached("collar", () => MeshKit.Torus(0.07f, 0.012f, 18, 6)), trim, new Vector3(0f, 0.235f, 0.004f)));
            if (longTunic)
            {
                Add(rig, MeshKit.Part(rig.Hips, "Hem", MeshKit.Cached($"hem{b:0.00}", () => MeshKit.Loft(new[] { new MeshKit.Ring(-0.625f, 0.262f * b, 0.202f * b), new MeshKit.Ring(-0.59f, 0.25f * b, 0.19f * b) }, 20, false, false)), trim));
                Add(rig, MeshKit.Part(rig.Chest, "Placket", MeshKit.Cached("placket", () => MeshKit.Box(new Vector3(0.03f, 0.2f, 0.01f))), trim, new Vector3(0f, 0.13f, 0.11f)));
            }
            // Sleeves.
            foreach ((Transform upper, Transform lower) in new[] { (rig.UpperArmL, rig.LowerArmL), (rig.UpperArmR, rig.LowerArmR) })
            {
                Add(rig, MeshKit.Part(upper, "Sleeve", MeshKit.Cached($"sleeveU{b:0.00}{shortSleeve}", () => MeshKit.Limb(shortSleeve ? 0.13f : rig.UpperArm, 0.055f * b, 0.048f * b)), cloth));
                if (!shortSleeve)
                {
                    Add(rig, MeshKit.Part(lower, "SleeveL", MeshKit.Cached($"sleeveL{b:0.00}", () => MeshKit.Limb(rig.LowerArm * 0.82f, 0.047f * b, 0.042f * b)), cloth));
                    Add(rig, MeshKit.Part(lower, "Cuff", MeshKit.Cached("cuff", () => MeshKit.Torus(0.041f, 0.008f, 14, 5)), trim, new Vector3(0f, -rig.LowerArm * 0.8f, 0f)));
                }
            }
            // Lower garment.
            bool wide = style == 0 || style == 3 || style == 4; // shalwar
            Material pants = style == 1 ? Materials.Lit(new Color(0.18f, 0.22f, 0.32f), 0.1f) : style == 2 ? Materials.Lit(new Color(0.2f, 0.2f, 0.22f), 0.08f) : wide ? cloth2 : cloth;
            foreach ((Transform upper, Transform lower) in new[] { (rig.UpperLegL, rig.LowerLegL), (rig.UpperLegR, rig.LowerLegR) })
            {
                if (style == 5)
                {
                    Add(rig, MeshKit.Part(upper, "Shorts", MeshKit.Cached($"shorts{b:0.00}", () => MeshKit.Limb(0.24f, 0.088f * b, 0.075f * b)), cloth2));
                    Add(rig, MeshKit.Part(lower, "Sock", MeshKit.Cached($"sock{b:0.00}", () => MeshKit.Limb(0.18f, 0.044f * b, 0.04f * b)), Materials.Lit(Color.white, 0.1f), new Vector3(0f, -rig.LowerLeg + 0.18f, 0f)));
                    continue;
                }
                float wu = wide ? 1.35f : 1.08f, wl = wide ? 1.55f : 1.1f;
                Add(rig, MeshKit.Part(upper, "Trouser", MeshKit.Cached($"trU{b:0.00}{wide}", () => MeshKit.Limb(rig.UpperLeg, 0.08f * b * wu, 0.058f * b * wu)), pants));
                Add(rig, MeshKit.Part(lower, "TrouserL", MeshKit.Cached($"trL{b:0.00}{wide}", () => MeshKit.Limb(rig.LowerLeg * 0.9f, 0.058f * b * wl, (wide ? 0.042f : 0.044f) * b * wl)), pants));
            }
            // Style extras.
            switch (style)
            {
                case 1: // jacket: open front panels + zip line
                    Add(rig, MeshKit.Part(rig.Chest, "JacketZip", MeshKit.Cached("zip", () => MeshKit.Box(new Vector3(0.012f, 0.42f, 0.012f))), Materials.Lit(new Color(0.75f, 0.75f, 0.78f), 0.7f), new Vector3(0f, 0.02f, 0.115f)));
                    Add(rig, MeshKit.Part(rig.Chest, "JacketCollar", MeshKit.Cached("jcollar", () => MeshKit.Torus(0.085f, 0.022f, 18, 6)), cloth2, new Vector3(0f, 0.23f, -0.005f)));
                    break;
                case 2: // hoodie: hood + pocket
                    Add(rig, MeshKit.Part(rig.Chest, "Hood", MeshKit.Cached("hood", () => MeshKit.Ellipsoid(0.12f, 0.1f, 0.09f, 16, 8, -60f, 70f)), cloth, new Vector3(0f, 0.3f, -0.11f)));
                    Add(rig, MeshKit.Part(rig.Spine, "Pocket", MeshKit.Cached("pocket", () => MeshKit.Box(new Vector3(0.2f, 0.1f, 0.02f))), cloth2, new Vector3(0f, 0.02f, 0.115f)));
                    Add(rig, MeshKit.Part(rig.Chest, "Strings", MeshKit.Cached("strings", () => MeshKit.Box(new Vector3(0.06f, 0.12f, 0.008f))), Materials.Lit(Color.white, 0.2f), new Vector3(0f, 0.15f, 0.115f)));
                    break;
                case 3: // waistcoat over kurta
                    Add(rig, MeshKit.Part(rig.Hips, "Waistcoat", MeshKit.Cached($"vest{b:0.00}", () => MeshKit.Loft(new[]
                    {
                        new MeshKit.Ring(0.02f, 0.165f * b, 0.112f * b), new MeshKit.Ring(0.18f, 0.148f * b, 0.105f * b, 0.006f),
                        new MeshKit.Ring(0.3f, 0.17f * b, 0.118f * b, 0.012f), new MeshKit.Ring(0.44f, 0.17f * b, 0.1f * b)
                    }, 20, false, false, 200f, 320f)), cloth2));
                    for (int i = 0; i < 4; i++)
                        Add(rig, MeshKit.Part(rig.Chest, "Button", MeshKit.Cached("button", () => MeshKit.Ellipsoid(0.008f, 0.008f, 0.004f, 6, 4)), Materials.Lit(new Color(0.85f, 0.7f, 0.3f), 0.8f), new Vector3(0f, 0.03f + i * 0.055f, 0.118f)));
                    break;
                case 4: // festival: sash + embroidered bands
                    Add(rig, MeshKit.Part(rig.Chest, "Sash", MeshKit.Cached("sash", () => MeshKit.Box(new Vector3(0.06f, 0.55f, 0.012f))), cloth2, new Vector3(0f, 0.05f, 0.11f), Quaternion.Euler(0f, 0f, 35f)));
                    for (int i = 0; i < 2; i++)
                        Add(rig, MeshKit.Part(rig.Hips, "Band", MeshKit.Cached($"band{b:0.00}{i}", () => MeshKit.Loft(new[] { new MeshKit.Ring(-0.5f + i * 0.12f, (0.245f - i * 0.012f) * b, (0.185f - i * 0.01f) * b), new MeshKit.Ring(-0.47f + i * 0.12f, (0.24f - i * 0.012f) * b, (0.182f - i * 0.01f) * b) }, 20, false, false)), trim));
                    break;
                case 5: // sports kit: number panel
                    Add(rig, MeshKit.Part(rig.Chest, "Number", MeshKit.Cached("number", () => MeshKit.Box(new Vector3(0.12f, 0.12f, 0.008f))), cloth2, new Vector3(0f, 0.1f, 0.114f)));
                    break;
            }
        }

        private static void Accessory(CharacterRig rig, string id)
        {
            CosmeticItem item = CharacterCatalog.Accessory(id);
            if (item == null) return;
            Material m = Materials.Lit(KiteVisualFactory.Hex(item.primaryHex), 0.25f);
            Transform head = rig.Head;
            switch ((AccessorySlot)item.slot)
            {
                case AccessorySlot.Head:
                    if (item.style == 0) Add(rig, MeshKit.Part(head, "Topi", MeshKit.Cached("topi", () => MeshKit.Cylinder(0.098f, 0.07f, 18, 0.092f)), m, new Vector3(0f, 0.2f, -0.005f)));
                    else if (item.style == 1)
                    {
                        Add(rig, MeshKit.Part(head, "Cap", MeshKit.Cached("cap", () => MeshKit.Ellipsoid(0.108f, 0.07f, 0.115f, 16, 7, 0f, 90f)), m, new Vector3(0f, 0.17f, -0.005f)));
                        Add(rig, MeshKit.Part(head, "Visor", MeshKit.Cached("visor", () => MeshKit.Ellipsoid(0.08f, 0.008f, 0.07f, 12, 4)), m, new Vector3(0f, 0.172f, 0.1f), Quaternion.Euler(-8f, 0f, 0f)));
                    }
                    else if (item.style == 2)
                        for (int i = 0; i < 4; i++)
                            Add(rig, MeshKit.Part(head, "Turban", MeshKit.Cached("turbanring", () => MeshKit.Torus(0.1f, 0.03f, 20, 7)), m, new Vector3(0f, 0.15f + i * 0.03f, -0.005f), Quaternion.Euler(i * 6f - 8f, i * 25f, 0f), Vector3.one * (1f - i * 0.12f)));
                    else Add(rig, MeshKit.Part(head, "Headband", MeshKit.Cached("headband", () => MeshKit.Torus(0.104f, 0.014f, 20, 6)), m, new Vector3(0f, 0.16f, -0.004f), Quaternion.Euler(-6f, 0f, 0f)));
                    break;
                case AccessorySlot.Eyes:
                    foreach (float side in new[] { -1f, 1f })
                        Add(rig, MeshKit.Part(head, "Lens", MeshKit.Cached(item.style == 0 ? "lens" : "goggle", () => item.style == 0 ? MeshKit.Ellipsoid(0.03f, 0.022f, 0.008f, 12, 6) : MeshKit.Ellipsoid(0.034f, 0.028f, 0.02f, 12, 6)), m, new Vector3(side * 0.042f, 0.127f, 0.108f)));
                    Add(rig, MeshKit.Part(head, "Bridge", MeshKit.Cached("bridge", () => MeshKit.Box(new Vector3(0.03f, 0.006f, 0.006f))), m, new Vector3(0f, 0.135f, 0.11f)));
                    if (item.style == 1) Add(rig, MeshKit.Part(head, "Strap", MeshKit.Cached("strap", () => MeshKit.Torus(0.104f, 0.008f, 20, 5)), m, new Vector3(0f, 0.13f, -0.004f)));
                    break;
                case AccessorySlot.Back:
                    if (item.style == 0) Add(rig, MeshKit.Part(rig.BackSocket, "KiteBag", MeshKit.Cached("kitebag", () => MeshKit.Box(new Vector3(0.26f, 0.36f, 0.08f))), m, new Vector3(0f, -0.05f, 0f), Quaternion.Euler(8f, 0f, 0f)));
                    else if (item.style == 1) Add(rig, MeshKit.Part(rig.Chest, "Scarf", MeshKit.Cached("scarf", () => MeshKit.Torus(0.085f, 0.03f, 18, 7)), m, new Vector3(0f, 0.24f, 0f)));
                    else
                    {
                        Transform spare = KiteVisualFactory.Build("guddi", "", rig.BackSocket).transform;
                        spare.localPosition = new Vector3(0f, 0f, -0.04f);
                        spare.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        spare.localScale = Vector3.one * 0.4f;
                        Collect(rig, spare);
                    }
                    break;
                case AccessorySlot.Waist:
                    if (item.style == 0) Add(rig, MeshKit.Part(rig.Hips, "Pouch", MeshKit.Cached("pouch", () => MeshKit.Box(new Vector3(0.1f, 0.08f, 0.05f))), m, new Vector3(0.14f, 0.02f, 0.06f)));
                    else if (item.style == 1) Add(rig, MeshKit.Part(rig.Hips, "WaistSash", MeshKit.Cached("wsash", () => MeshKit.Torus(0.165f, 0.022f, 20, 6)), m, new Vector3(0f, 0.05f, 0f)));
                    else Add(rig, MeshKit.Part(rig.Hips, "Holster", MeshKit.Cached("holster", () => MeshKit.Cylinder(0.05f, 0.12f, 12)), m, new Vector3(-0.16f, -0.02f, 0.02f), Quaternion.Euler(0f, 0f, 90f)));
                    break;
            }
        }

        /// <summary>A detailed charkhi (spool) per type: two rims, spokes, a wound line core and a handle.</summary>
        public static Transform BuildCharkhi(Transform socket, string charkhiId, out Transform spool)
        {
            Color wood, rim;
            switch (charkhiId)
            {
                case "gear": wood = new Color(0.55f, 0.38f, 0.2f); rim = new Color(0.85f, 0.68f, 0.3f); break;
                case "speed": wood = new Color(0.3f, 0.32f, 0.38f); rim = new Color(0.85f, 0.25f, 0.2f); break;
                case "bari": wood = new Color(0.35f, 0.22f, 0.12f); rim = new Color(0.25f, 0.18f, 0.1f); break;
                case "brake": wood = new Color(0.62f, 0.45f, 0.25f); rim = new Color(0.2f, 0.45f, 0.75f); break;
                default: wood = new Color(0.7f, 0.5f, 0.28f); rim = new Color(0.5f, 0.33f, 0.18f); break;
            }
            var root = new GameObject("Charkhi").transform;
            root.SetParent(socket, false);
            root.localRotation = Quaternion.Euler(0f, 0f, 90f);
            spool = new GameObject("Spool").transform;
            spool.SetParent(root, false);
            Material mWood = Materials.Lit(wood, 0.3f), mRim = Materials.Lit(rim, 0.45f), mLine = Materials.Lit(new Color(0.95f, 0.93f, 0.85f), 0.2f);
            MeshKit.Part(spool, "RimA", MeshKit.Cached("ck_rim", () => MeshKit.Torus(0.085f, 0.012f, 20, 6)), mRim, new Vector3(0f, 0.06f, 0f));
            MeshKit.Part(spool, "RimB", MeshKit.Cached("ck_rim", () => MeshKit.Torus(0.085f, 0.012f, 20, 6)), mRim, new Vector3(0f, -0.06f, 0f));
            MeshKit.Part(spool, "Core", MeshKit.Cached("ck_core", () => MeshKit.Cylinder(0.062f, 0.11f, 16)), mLine);
            for (int i = 0; i < 6; i++)
                MeshKit.Part(spool, "Spoke", MeshKit.Cached("ck_spoke", () => MeshKit.Box(new Vector3(0.012f, 0.13f, 0.012f))), mWood,
                    Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0.08f, 0f, 0f));
            MeshKit.Part(root, "Axle", MeshKit.Cached("ck_axle", () => MeshKit.Cylinder(0.013f, 0.24f, 10)), mWood);
            MeshKit.Part(root, "Handle", MeshKit.Cached("ck_handle", () => MeshKit.Limb(0.12f, 0.018f, 0.016f, 8)), mWood, new Vector3(0f, -0.12f, 0f));
            return root;
        }
    }
}
