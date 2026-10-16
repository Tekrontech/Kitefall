using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Arcade flight tunables (see FlightSettings for what each number does).</summary>
    [CreateAssetMenu(fileName = "FlightConfig", menuName = "Kitefall/Config/Flight Config", order = 21)]
    public sealed class FlightConfig : ScriptableObject
    {
        public FlightSettings settings = new FlightSettings();
    }
}
