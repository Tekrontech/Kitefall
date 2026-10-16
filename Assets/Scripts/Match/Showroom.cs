using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>Home / Hangar background: the selected kite flying in 3D with its tail over a rooftop (spec 4.1).</summary>
    public sealed class Showroom : MonoBehaviour
    {
        private GameObject _world;
        private KiteSimulation _kite;
        private KiteView _view;
        private WindModel _wind;
        private float _time;
        private string _key;
        private MatchCamera _cam;
        private Vector3 _anchor;

        public void Show(Camera cam, string kiteId, string skinId, string tailId, string dorId)
        {
            string key = kiteId + skinId + tailId + dorId;
            if (_world != null && key == _key) { gameObject.SetActive(true); return; }
            Hide();
            _key = key;
            _world = new GameObject("Showroom");
            ArenaLayout layout = ArenaLayout.Generate(ArenaType.OldCityRooftops, 7, 1);
            ArenaBuilder.Build(layout, Game.Db != null ? Game.Db.Arena(ArenaType.OldCityRooftops) : null, 0.78f, _world.transform);
            _anchor = ArenaBuilder.ToUnity(layout.Anchors[0]);

            var loadout = SpecDefaults.Loadout(kiteId, tailId, dorId, "lakri");
            var flight = Game.Db != null && Game.Db.flight != null ? Game.Db.flight.settings : new FlightSettings();
            var line = new LineSettings();
            _kite = new KiteSimulation(loadout.Compose(), new TensionSettings(), flight, line, layout.Anchors[0], 0f, 600f, false, 3);
            _kite.Reset(0f, 8f);
            _wind = new WindModel(new WindSettings { baseSpeed = 5f, directionVariationDeg = 6f });
            _view = new GameObject("ShowroomKite").AddComponent<KiteView>();
            _view.transform.SetParent(_world.transform, false);
            _view.Init(0, kiteId, skinId, tailId, new Color(0.97f, 0.96f, 0.92f), Game.Settings != null ? Game.Settings.TailSegments : 10, new TailRopeSettings());
            var flyer = new GameObject("ShowroomFlyer").AddComponent<FlyerView>();
            flyer.transform.SetParent(_world.transform, false);
            flyer.Init(_anchor, new Color(0.95f, 0.55f, 0.15f));

            _cam = cam.GetComponent<MatchCamera>() ?? cam.gameObject.AddComponent<MatchCamera>();
            _cam.Mode = CameraMode.Showroom;
            _cam.Target = _view.transform.GetChild(0);
            _time = 0f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_world != null) Destroy(_world);
            _world = null;
            _kite = null;
            _key = null;
        }

        private void Update()
        {
            if (_kite == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _time += dt;
            // Gentle figure-eight with light tension pulses.
            var input = new KiteInput
            {
                SteerX = Mathf.Sin(_time * 0.5f),
                SteerY = _kite.Flight.ElevationDeg < 40f ? 1f : Mathf.Cos(_time * 0.9f) * 0.6f,
                PullHeld = _kite.Tension.Tension < 48f
            };
            WindSample w = _wind.Sample(_time, _kite.Flight.Height);
            _kite.Step(input, w, _time, dt);
            if (!_kite.IsFlying) _kite.Reset(w.DirectionDeg, 8f);
            if (_kite.Spool.LineOut > 45f) _kite.Spool.Reset(45f);

            var snap = new KiteSnap
            {
                Status = _kite.Status, Anchor = _kite.Anchor, Azimuth = _kite.Flight.AzimuthDeg, Elevation = _kite.Flight.ElevationDeg,
                Radius = _kite.Flight.Radius, Heading = _kite.Flight.HeadingDeg, Tension = _kite.Tension.Tension,
                Zone = _kite.Tension.Zone, Action = _kite.CurrentAction, Durability = 100f
            };
            Vector3 wind = new Vector3(w.DirX, 0f, w.DirZ) * w.Speed;
            _view.Apply(snap, wind, false, false, _time, dt);
            if (_cam != null && _cam.Mode == CameraMode.Showroom) _cam.Target = _view.transform.GetChild(0);
        }
    }
}
