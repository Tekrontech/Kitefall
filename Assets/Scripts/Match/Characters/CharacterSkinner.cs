using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Bakes a generated character (one MeshRenderer per body part / garment) into a single GPU-skinned mesh with one sub-mesh
    /// per material (upgrade spec 12 performance budget: ~10 draw calls per character instead of ~60). Every vertex is weighted
    /// 100 % to the bone its part hangs from, so the procedural animation keeps working by moving bone transforms.
    /// Moving props (spinning charkhi spool, held kite) are left as separate renderers.
    /// </summary>
    public static class CharacterSkinner
    {
        public static SkinnedMeshRenderer Bake(CharacterRig rig, ICollection<Transform> exclude)
        {
            Transform[] bones =
            {
                rig.Hips, rig.Spine, rig.Chest, rig.Neck, rig.Head,
                rig.UpperArmL, rig.LowerArmL, rig.HandL, rig.UpperArmR, rig.LowerArmR, rig.HandR,
                rig.UpperLegL, rig.LowerLegL, rig.FootL, rig.UpperLegR, rig.LowerLegR, rig.FootR
            };
            var boneIndex = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++) boneIndex[bones[i]] = i;

            Transform root = rig.Root;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var byMaterial = new Dictionary<Material, List<int>>();
            var materials = new List<Material>();
            var bakedRenderers = new List<MeshRenderer>();

            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (Excluded(mr.transform, exclude, root)) continue;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                int bone = BoneOf(mr.transform, boneIndex);
                if (bone < 0) continue;
                Mesh m = mf.sharedMesh;
                Matrix4x4 local = toRoot * mr.transform.localToWorldMatrix;
                int start = verts.Count;
                Vector3[] mv = m.vertices;
                Vector3[] mn = m.normals;
                Vector2[] mu = m.uv;
                for (int v = 0; v < mv.Length; v++)
                {
                    verts.Add(local.MultiplyPoint3x4(mv[v]));
                    normals.Add(mn.Length == mv.Length ? local.MultiplyVector(mn[v]).normalized : Vector3.up);
                    uvs.Add(mu.Length == mv.Length ? mu[v] : Vector2.zero);
                    weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                }
                bool flip = local.determinant < 0f;
                if (!byMaterial.TryGetValue(mr.sharedMaterial, out List<int> tris))
                {
                    tris = new List<int>();
                    byMaterial[mr.sharedMaterial] = tris;
                    materials.Add(mr.sharedMaterial);
                }
                int[] mt = m.triangles;
                for (int t = 0; t < mt.Length; t += 3)
                {
                    if (flip) { tris.Add(start + mt[t]); tris.Add(start + mt[t + 2]); tris.Add(start + mt[t + 1]); }
                    else { tris.Add(start + mt[t]); tris.Add(start + mt[t + 1]); tris.Add(start + mt[t + 2]); }
                }
                bakedRenderers.Add(mr);
            }
            if (verts.Count == 0) return null;

            var mesh = new Mesh { name = "CharacterSkinned" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.subMeshCount = materials.Count;
            for (int i = 0; i < materials.Count; i++) mesh.SetTriangles(byMaterial[materials[i]], i);
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh.RecalculateBounds();

            var go = new GameObject("SkinnedBody");
            go.transform.SetParent(root, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = rig.Hips;
            smr.sharedMaterials = materials.ToArray();
            smr.quality = SkinQuality.Bone1;
            smr.updateWhenOffscreen = false;
            Bounds b = mesh.bounds;
            b.Expand(1.2f);
            smr.localBounds = b;

            foreach (MeshRenderer mr in bakedRenderers)
            {
                GameObject part = mr.gameObject;
                Object.Destroy(mr.GetComponent<MeshFilter>());
                Object.Destroy(mr);
                if (part.transform.childCount == 0 && part.GetComponents<Component>().Length <= 3) Object.Destroy(part);
            }
            return smr;
        }

        private static bool Excluded(Transform t, ICollection<Transform> exclude, Transform root)
        {
            for (Transform x = t; x != null && x != root; x = x.parent)
                if (exclude.Contains(x) || x.name.StartsWith("Kite_")) return true;
            return false;
        }

        private static int BoneOf(Transform t, Dictionary<Transform, int> bones)
        {
            for (Transform x = t; x != null; x = x.parent)
                if (bones.TryGetValue(x, out int i)) return i;
            return -1;
        }
    }
}
