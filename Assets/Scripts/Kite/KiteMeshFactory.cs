using UnityEngine;

namespace Kitefall.Kite
{
    /// <summary>
    /// Builds simple original kite meshes in code (placeholder art until real models exist).
    /// Meshes are double-sided so the sail is visible from both sides with any shader.
    /// Local axes: +Y = nose, +X = right wing, +Z = back of the sail.
    /// </summary>
    public static class KiteMeshFactory
    {
        /// <summary>Diamond fighter kite (guddi / patang shape).</summary>
        /// <param name="width">Wing tip to wing tip, m.</param>
        /// <param name="height">Nose to tail, m.</param>
        /// <param name="spineRatio">Where the wings meet the spine, 0 = bottom, 1 = top.</param>
        /// <param name="bow">How far the wing tips bend back (+Z), m – gives the sail some shape.</param>
        public static Mesh CreateDiamond(float width = 1.6f, float height = 1.8f, float spineRatio = 0.62f, float bow = 0.12f)
        {
            float top = height * (1f - spineRatio);
            float bottom = -height * spineRatio;
            float half = width * 0.5f;

            // 0 nose, 1 right, 2 tail, 3 left, 4 centre (slightly forward for a curved look)
            var front = new[]
            {
                new Vector3(0f, top, 0f),
                new Vector3(half, 0f, bow),
                new Vector3(0f, bottom, 0f),
                new Vector3(-half, 0f, bow),
                new Vector3(0f, 0f, -bow * 0.5f)
            };
            int[] frontTris = { 4, 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0 };

            var vertices = new Vector3[front.Length * 2];
            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < front.Length; i++)
            {
                vertices[i] = front[i];
                vertices[i + front.Length] = front[i];
                var uv = new Vector2(front[i].x / width + 0.5f, (front[i].y - bottom) / height);
                uvs[i] = uv;
                uvs[i + front.Length] = uv;
            }

            var triangles = new int[frontTris.Length * 2];
            for (int i = 0; i < frontTris.Length; i += 3)
            {
                triangles[i] = frontTris[i];
                triangles[i + 1] = frontTris[i + 1];
                triangles[i + 2] = frontTris[i + 2];
                // Back face: reversed winding, offset vertices.
                triangles[frontTris.Length + i] = frontTris[i] + front.Length;
                triangles[frontTris.Length + i + 1] = frontTris[i + 2] + front.Length;
                triangles[frontTris.Length + i + 2] = frontTris[i + 1] + front.Length;
            }

            var mesh = new Mesh { name = "KiteDiamond" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
