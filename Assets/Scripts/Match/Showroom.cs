using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Home / Hangar / Wardrobe background (spec 4.1, upgrade F1): the player's character on a rooftop flying the selected kite
    /// with its tail, or a close turntable of the character for the wardrobe (outfits, accessories, emote previews).
    /// </summary>
    public sealed class Showroom : MonoBehaviour
    {
        private GameObject _world;
        private KiteSimulation _kite;
        private KiteView _view;
        private CharacterView _character;
        private WindModel _wind;
        private float _time;
        private string _key, _characterKey;
        private MatchCamera _cam;
        private Vector3 _anchor;
        private bool _turntable;
        private float _spin;
        private string _kiteId = "guddi", _skinId = "", _charkhiId = "lakri";
        private string _characterId = CharacterCatalog.DefaultCharacter, _outfitId = "";
        private string[] _accessories = new string[4];

        public CharacterView Character => _character;

        /// <summary>Settings ▸ Home scene: world harbour (default, international) or the South Asian old city rooftops.</summary>
        public static ArenaType HomeArena => Game.Settings != null && Game.Settings.Data.homeTheme == 1 ? ArenaType.OldCityRooftops : ArenaType.MediterraneanHarbour;

        public void Show(Camera cam, string kiteId, string skinId, string tailId, string dorId) =>
            Show(cam, kiteId, skinId, tailId, dorId, _characterId, _outfitId, _accessories, _charkhiId);

        public void Show(Camera cam, string kiteId, string skinId, string tailId, string dorId, string characterId, string outfitId, string[] accessories, string charkhiId)
        {
            ArenaType theme = HomeArena;
            string key = kiteId + skinId + tailId + dorId + theme;
            _kiteId = kiteId; _skinId = skinId; _charkhiId = string.IsNullOrEmpty(charkhiId) ? "lakri" : charkhiId;
            if (_world != null && key == _key)
            {
                gameObject.SetActive(true);
                SetCharacter(characterId, outfitId, accessories);
                Turntable(false);
                return;
            }
            Hide();
            _key = key;
            _world = new GameObject("Showroom");
            ArenaLayout layout = ArenaLayout.Generate(theme, 7, 1);
            ArenaBuilder.Build(layout, Game.Db != null ? Game.Db.Arena(theme) : null, 0.78f, _world.transform);
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

            _cam = cam.GetComponent<MatchCamera>() ?? cam.gameObject.AddComponent<MatchCamera>();
            _characterKey = null;
            SetCharacter(characterId, outfitId, accessories);
            Turntable(false);
            _time = 0f;
            gameObject.SetActive(true);
        }

        /// <summary>Rebuilds the character when the look changed (wardrobe preview, equip).</summary>
        public void SetCharacter(string characterId, string outfitId, string[] accessories)
        {
            _characterId = string.IsNullOrEmpty(characterId) ? CharacterCatalog.DefaultCharacter : characterId;
            _outfitId = outfitId ?? "";
            _accessories = accessories ?? new string[4];
            string key = _characterId + "|" + _outfitId + "|" + string.Join(",", _accessories) + "|" + _charkhiId + _kiteId;
            if (_world == null || key == _characterKey) return;
            _characterKey = key;
            if (_character != null) Destroy(_character.gameObject);
            _character = new GameObject("ShowroomCharacter").AddComponent<CharacterView>();
            _character.transform.SetParent(_world.transform, false);
            _character.transform.position = _anchor - Vector3.up * 1.35f;
            _character.Init(CharacterBuilder.LookFor(_characterId, _outfitId, _accessories, _charkhiId, _kiteId, _skinId), "", new Color(0.98f, 0.58f, 0.15f), true, false);
            _view.LineStart = _character.LineAnchor;
        }

        /// <summary>Wardrobe: close orbit around the character (on) or the flying kite (off).</summary>
        public void Turntable(bool on)
        {
            _turntable = on;
            if (_cam == null) return;
            _cam.Mode = CameraMode.Showroom;
            if (on && _character != null)
            {
                _cam.Target = _character.Rig.Chest;
                _cam.OrbitRadius = 3.3f;
                _cam.OrbitHeight = 0.5f;
                _cam.OrbitSpeed = 0f;
            }
            else
            {
                _cam.Target = _view != null ? _view.transform.GetChild(0) : null;
                _cam.OrbitRadius = 22f;
                _cam.OrbitHeight = 4f;
                _cam.OrbitSpeed = 6f;
            }
        }

        public void PreviewEmote(string emoteId) => _character?.PlayEmote(emoteId);

        public void Hide()
        {
            if (_world != null) Destroy(_world);
            _world = null;
            _kite = null;
            _character = null;
            _key = null;
            _characterKey = null;
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

            if (_character != null)
            {
                Vector3 feet = _anchor - Vector3.up * 1.35f;
                if (_turntable) _spin += dt * 25f;
                var f = new CharacterFrame
                {
                    State = _turntable ? FlyerState.OnRoof : FlyerState.Flying,
                    Action = _kite.CurrentAction,
                    Position = feet,
                    KitePosition = _view.KitePosition,
                    KiteValid = !_turntable,
                    WindDirection = _turntable ? _spin + 180f : w.DirectionDeg,
                    Time = _time
                };
                _character.Apply(f, dt);
            }
            if (_cam != null && _cam.Mode == CameraMode.Showroom && !_turntable) _cam.Target = _view.transform.GetChild(0);
        }
    }
}
