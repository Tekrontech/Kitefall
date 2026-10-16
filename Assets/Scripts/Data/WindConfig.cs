using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Wind pattern for one arena.</summary>
    [CreateAssetMenu(fileName = "WindConfig", menuName = "Kitefall/Config/Wind Config", order = 22)]
    public sealed class WindConfig : ScriptableObject
    {
        public WindSettings settings = new WindSettings();
    }
}
