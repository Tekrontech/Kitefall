namespace Kitefall
{
    /// <summary>Converts between the Core math type (System.Numerics) and UnityEngine.</summary>
    public static class VectorConversions
    {
        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 v) => new UnityEngine.Vector3(v.X, v.Y, v.Z);
        public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
    }
}
