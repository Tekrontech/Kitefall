using System.Text;
using Kitefall.Core;
using Kitefall.Kite;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// Milestone 1 debug read-out: every number the tester needs to check the rules.
    /// The real HUD (spec 10) is built in milestone 9.
    /// </summary>
    public sealed class FlightDebugHud : MonoBehaviour
    {
        [SerializeField] private KiteController kite;
        [SerializeField] private Text text;
        [SerializeField] private Text statusBanner;
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private readonly StringBuilder _sb = new StringBuilder(512);
        private float _nextRefresh;

        private void Update()
        {
            if (kite == null || kite.Simulation == null) return;

            UpdateBanner();

            if (Time.unscaledTime < _nextRefresh || text == null) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            KiteSimulation sim = kite.Simulation;
            WindSample wind = kite.LastWind;

            _sb.Clear();
            _sb.Append("<b>KITEFALL · Milestone 1 – Flight test</b>\n");
            _sb.Append("Loadout: ").Append(kite.KiteClass.displayName)
               .Append(" · ").Append(kite.Tail != null ? kite.Tail.displayName : "No dum")
               .Append(" · ").Append(kite.Dor.displayName)
               .Append(" · ").Append(kite.Charkhi.displayName).Append('\n');
            _sb.Append("Status: ").Append(sim.Status).Append("   Action: ").Append(sim.CurrentAction).Append('\n');
            _sb.Append("Tension: ").Append(sim.Tension.Tension.ToString("0.0"))
               .Append(" (").Append(sim.Tension.Zone).Append(")   Strength: ").Append(sim.Tension.Strength.ToString("0")).Append('\n');
            _sb.Append("Height: ").Append(sim.Flight.Height.ToString("0.0")).Append(" m   Speed: ")
               .Append(sim.Flight.Speed.ToString("0.0")).Append(" m/s\n");
            _sb.Append("Line out: ").Append(sim.Spool.LineOut.ToString("0")).Append(" / ")
               .Append(sim.Spool.MaxLineOut.ToString("0")).Append(" m\n");
            _sb.Append("Elevation: ").Append(sim.Flight.ElevationDeg.ToString("0")).Append("°   Window: ")
               .Append(KMath.DeltaAngle(wind.DirectionDeg, sim.Flight.AzimuthDeg).ToString("0")).Append("°   Heading: ")
               .Append(sim.Flight.HeadingDeg.ToString("0")).Append("°\n");
            _sb.Append("Wind: ").Append(wind.Speed.ToString("0.0")).Append(" m/s toward ")
               .Append(wind.DirectionDeg.ToString("0")).Append('°').Append(wind.IsStrong ? "  STRONG" : "").Append('\n');
            _sb.Append("Turn rate: ").Append(sim.Loadout.TurnRate.ToString("0")).Append("°/s   Sweet zone: ")
               .Append(sim.Loadout.SweetZone).Append('\n');
            _sb.Append("\n<size=20>Keys: WASD/Arrows steer · Space PULL · Left Shift REEL · Backspace reset</size>");
            text.text = _sb.ToString();
        }

        private void UpdateBanner()
        {
            if (statusBanner == null) return;
            KiteStatus status = kite.Simulation.Status;
            if (status == KiteStatus.Flying)
            {
                statusBanner.text = string.Empty;
                return;
            }

            string reason = status == KiteStatus.SelfSnapped ? "SELF-SNAP!  Line broke from over-tension" : "CRASH!  Kite hit the ground";
            statusBanner.text = $"{reason}\n<size=28>Relaunching in {Mathf.Max(0f, kite.RespawnCountdown):0.0} s</size>";
        }
    }
}
