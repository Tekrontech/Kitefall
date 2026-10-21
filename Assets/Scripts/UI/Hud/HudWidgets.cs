using Kitefall.App;
using Kitefall.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitefall.UI.Hud
{
    /// <summary>Vertical tension bar: zones, Strength line, marker, moving sweet zone during contests, snap flash.</summary>
    public sealed class TensionBarWidget
    {
        private readonly RectTransform _bar, _marker, _strength, _sweet;
        private readonly Image _markerImg, _flash, _sweetImg;
        private readonly Text _value;
        private readonly Image _safe, _strong, _danger;

        public TensionBarWidget(Transform parent)
        {
            Image bg = UiKit.Image(parent, new Color(0, 0, 0, 0.45f), Sprites.Rounded, "TensionBar");
            _bar = bg.rectTransform;
            // Right edge, from under the radar + emote row (top 396) down to 130 above the bottom: fits 16:9 … 20:9 (audit F1).
            _bar.Band(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-90, 130), new Vector2(-36, -396));
            _flash = UiKit.Image(_bar, new Color(1, 0.2f, 0.15f, 0f), Sprites.Rounded, "Flash");
            _flash.rectTransform.Stretch(-10, -10, -10, -10);
            _flash.transform.SetAsFirstSibling();
            _safe = Zone("Safe");
            _strong = Zone("Strong");
            _danger = Zone("Danger");
            _sweetImg = UiKit.Image(_bar, Theme.Sweet(false), Sprites.RoundedSmall, "Sweet");
            _sweet = _sweetImg.rectTransform;
            _strength = UiKit.Image(_bar, Color.white, Sprites.White, "Strength").rectTransform;
            _strength.anchorMin = new Vector2(-0.25f, 0.95f);
            _strength.anchorMax = new Vector2(1.25f, 0.95f);
            _strength.sizeDelta = new Vector2(0, 4);
            _markerImg = UiKit.Image(_bar, Color.white, Sprites.RoundedSmall, "Marker");
            _marker = _markerImg.rectTransform;
            _marker.anchorMin = new Vector2(-0.4f, 0.4f);
            _marker.anchorMax = new Vector2(1.4f, 0.4f);
            _marker.sizeDelta = new Vector2(0, 12);
            _value = UiKit.Label(_bar, "40", 30, Color.white, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
            _value.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(140, 40));
            Text title = UiKit.Label(_bar, "TENSION", 18, Theme.TextDim, TextAnchor.UpperCenter);
            title.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(160, 26));
        }

        private Image Zone(string name)
        {
            Image z = UiKit.Image(_bar, Color.white, Sprites.White, name);
            return z;
        }

        public void Update(in KiteSnap k, float safeMax, float strongMax, bool colorBlind, bool highlightSweet)
        {
            SetRange(_safe.rectTransform, 0f, safeMax / 100f);
            SetRange(_strong.rectTransform, safeMax / 100f, strongMax / 100f);
            SetRange(_danger.rectTransform, strongMax / 100f, 1f);
            _safe.color = WithA(Theme.ZoneSafe(colorBlind), 0.55f);
            _strong.color = WithA(Theme.ZoneStrong(colorBlind), 0.55f);
            _danger.color = WithA(Theme.ZoneDanger(colorBlind), 0.55f);

            SetY(_marker, k.Tension / 100f);
            SetY(_strength, Mathf.Clamp01(k.Strength / 100f));
            _markerImg.color = k.Zone == TensionZone.Danger ? Theme.ZoneDanger(colorBlind) : k.Zone == TensionZone.Strong ? Theme.ZoneStrong(colorBlind) : Color.white;
            _value.text = Mathf.RoundToInt(k.Tension).ToString();

            bool sweet = k.InContest && k.SweetCenter >= 0f;
            _sweet.gameObject.SetActive(sweet);
            if (sweet)
            {
                float lo = Mathf.Clamp01((k.SweetCenter - k.SweetWidth * 0.5f) / 100f);
                float hi = Mathf.Clamp01((k.SweetCenter + k.SweetWidth * 0.5f) / 100f);
                _sweet.anchorMin = new Vector2(-0.15f, lo);
                _sweet.anchorMax = new Vector2(1.15f, hi);
                _sweet.offsetMin = _sweet.offsetMax = Vector2.zero;
                bool inside = k.Tension >= k.SweetCenter - k.SweetWidth * 0.5f && k.Tension <= k.SweetCenter + k.SweetWidth * 0.5f;
                Color c = Theme.Sweet(colorBlind);
                _sweetImg.color = WithA(c, inside && highlightSweet ? 0.95f : 0.55f);
            }

            bool danger = k.Tension > k.Strength;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(8f, 30f, k.OverStrength01));
            _flash.color = new Color(1f, 0.2f, 0.15f, danger ? Mathf.Lerp(0.3f, 0.95f, pulse) : 0f);
        }

        private static Color WithA(Color c, float a) { c.a = a; return c; }

        private static void SetRange(RectTransform r, float lo, float hi)
        {
            r.anchorMin = new Vector2(0, lo);
            r.anchorMax = new Vector2(1, hi);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static void SetY(RectTransform r, float y)
        {
            r.anchorMin = new Vector2(r.anchorMin.x, y);
            r.anchorMax = new Vector2(r.anchorMax.x, y);
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, 0f);
        }
    }

    /// <summary>Centre contest meter (spec 10): −100 … +100 with your attack vs theirs.</summary>
    public sealed class ContestMeterWidget
    {
        private readonly GameObject _root;
        private readonly RectTransform _fill, _marker;
        private readonly Text _mine, _theirs, _number;

        public ContestMeterWidget(Transform parent)
        {
            Image bg = UiKit.Panel(parent, new Color(0, 0, 0, 0.55f), "ContestMeter", false);
            _root = bg.gameObject;
            bg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(760, 130));
            Image track = UiKit.Image(bg.transform, new Color(1, 1, 1, 0.15f), Sprites.RoundedSmall, "Track");
            track.rectTransform.Band(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(30, -14), new Vector2(-30, 14));
            Image mid = UiKit.Image(track.transform, new Color(1, 1, 1, 0.6f), Sprites.White, "Mid");
            mid.rectTransform.Band(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-2, -6), new Vector2(2, 6));
            Image fill = UiKit.Image(track.transform, Theme.Good, Sprites.RoundedSmall, "Fill");
            _fill = fill.rectTransform;
            Image marker = UiKit.Image(track.transform, Color.white, Sprites.Circle, "Marker");
            _marker = marker.rectTransform;
            _marker.sizeDelta = new Vector2(40, 40);
            _mine = UiKit.Label(bg.transform, "", 24, Color.white, TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            _mine.rectTransform.Band(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(30, -44), new Vector2(0, -6));
            _theirs = UiKit.Label(bg.transform, "", 24, Color.white, TextAnchor.UpperRight, FontStyle.Bold).Shadowed();
            _theirs.rectTransform.Band(new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(-30, -6));
            _number = UiKit.Label(bg.transform, "", 26, Color.white, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
            _number.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4), new Vector2(0, 40));
            _root.SetActive(false);
        }

        public void Update(ContestSnap? contest, int me, bool numbers)
        {
            _root.SetActive(contest.HasValue);
            if (!contest.HasValue) return;
            ContestSnap c = contest.Value;
            bool a = c.A == me;
            float m = a ? c.M : -c.M; // + = toward me winning
            float x = Mathf.InverseLerp(-100f, 100f, m);
            _marker.anchorMin = _marker.anchorMax = new Vector2(x, 0.5f);
            _marker.anchoredPosition = Vector2.zero;
            _fill.anchorMin = new Vector2(Mathf.Min(0.5f, x), 0f);
            _fill.anchorMax = new Vector2(Mathf.Max(0.5f, x), 1f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _fill.GetComponent<Image>().color = m >= 0 ? Theme.Good : Theme.Bad;
            AttackType mine = a ? c.AttackA : c.AttackB, theirs = a ? c.AttackB : c.AttackA;
            bool lockedMine = a ? c.LockedA : c.LockedB, lockedTheirs = a ? c.LockedB : c.LockedA;
            _mine.text = "YOU: " + (lockedMine ? AttackRules.DisplayName(mine) : "…");
            _theirs.text = (lockedTheirs ? AttackRules.DisplayName(theirs) : "…") + " :THEM";
            _number.text = numbers ? $"{m:+0;-0;0}   {c.Elapsed:0.0}s / 3.0s" : (AttackRules.Beats(mine, theirs) ? "COUNTER!" : AttackRules.Beats(theirs, mine) ? "COUNTERED – watch your tension" : "");
        }
    }

    /// <summary>Radar (top right): enemy direction + height arrows, crates, storm edge.</summary>
    public sealed class RadarWidget
    {
        private readonly RectTransform _root;
        private readonly Image[] _dots = new Image[24];
        private readonly Text[] _arrows = new Text[24];
        private readonly Image[] _crates = new Image[6];
        private const float Range = 220f;

        public RadarWidget(Transform parent)
        {
            Image bg = UiKit.Image(parent, new Color(0, 0, 0, 0.45f), Sprites.Circle, "Radar");
            _root = bg.rectTransform;
            _root.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(230, 230));
            UiKit.Image(_root, new Color(1, 1, 1, 0.25f), Sprites.Ring, "Ring").rectTransform.Stretch(4, 4, 4, 4);
            Image me = UiKit.Image(_root, Theme.Accent, Sprites.Triangle, "Me");
            me.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = UiKit.Image(_root, Theme.Bad, Sprites.Circle, "Enemy");
                _dots[i].rectTransform.sizeDelta = new Vector2(16, 16);
                _arrows[i] = UiKit.Label(_dots[i].transform, "", 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                _arrows[i].rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14, 0), new Vector2(24, 24));
                _dots[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < _crates.Length; i++)
            {
                _crates[i] = UiKit.Image(_root, Theme.Warn, Sprites.RoundedSmall, "Crate");
                _crates[i].rectTransform.sizeDelta = new Vector2(14, 14);
                _crates[i].gameObject.SetActive(false);
            }
        }

        public void Update(MatchSnapshot s, int me, float cameraYaw, int[] teams)
        {
            if (me < 0 || me >= s.Kites.Length) return;
            System.Numerics.Vector3 mine = s.Kites[me].IsFlying ? s.Kites[me].WorldPosition : s.Kites[me].Anchor;
            float yaw = -cameraYaw * Mathf.Deg2Rad;
            for (int i = 0; i < _dots.Length; i++)
            {
                bool show = i < s.Kites.Length && i != me && s.Kites[i].IsFlying;
                _dots[i].gameObject.SetActive(show);
                if (!show) continue;
                System.Numerics.Vector3 p = s.Kites[i].WorldPosition;
                float dx = p.X - mine.X, dz = p.Z - mine.Z;
                Vector2 v = Rotate(new Vector2(dx, dz), yaw) / Range * 105f;
                _dots[i].rectTransform.anchoredPosition = Vector2.ClampMagnitude(v, 105f);
                bool ally = teams != null && teams.Length > i && teams.Length > me && teams[i] == teams[me];
                _dots[i].color = ally ? Theme.Accent2 : Theme.Bad;
                float dh = p.Y - mine.Y;
                // Calm phase: direction only, not height (spec 6.1).
                _arrows[i].text = s.RoundPhase == RoundPhase.Calm ? "" : dh > 3f ? "▲" : dh < -3f ? "▼" : "";
            }
            for (int i = 0; i < _crates.Length; i++)
            {
                bool show = i < s.Crates.Count;
                _crates[i].gameObject.SetActive(show);
                if (!show) continue;
                System.Numerics.Vector3 p = s.Crates[i].Position;
                Vector2 v = Rotate(new Vector2(p.X - mine.X, p.Z - mine.Z), yaw) / Range * 105f;
                _crates[i].rectTransform.anchoredPosition = Vector2.ClampMagnitude(v, 105f);
            }
        }

        private static Vector2 Rotate(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
    }

    /// <summary>Wind compass below the score: direction + speed, flashes 1 s before a gust.</summary>
    public sealed class CompassWidget
    {
        private readonly RectTransform _arrow;
        private readonly Image _bg;
        private readonly Text _label;

        public CompassWidget(Transform parent)
        {
            _bg = UiKit.Image(parent, new Color(0, 0, 0, 0.45f), Sprites.Circle, "Compass");
            _bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(96, 96));
            Image arrow = UiKit.Image(_bg.transform, Color.white, Sprites.Triangle, "Arrow");
            _arrow = arrow.rectTransform;
            _arrow.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 64));
            _label = UiKit.Label(_bg.transform, "", 22, Color.white, TextAnchor.UpperCenter, FontStyle.Bold).Shadowed();
            _label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(240, 30));
        }

        public void Update(MatchSnapshot s, float cameraYaw)
        {
            _arrow.localEulerAngles = new Vector3(0, 0, -(s.WindDirection - cameraYaw));
            bool flash = s.GustWarning && Mathf.Repeat(Time.unscaledTime * 6f, 1f) > 0.5f;
            _bg.color = flash ? new Color(1f, 0.6f, 0.1f, 0.8f) : new Color(0, 0, 0, 0.45f);
            _label.text = $"{s.WindSpeed:0.0} m/s" + (s.WindStrong ? " STRONG" : "");
        }
    }

    /// <summary>Swipe down on the right half = Dive (Settings ▸ Controls ▸ Dive gesture).</summary>
    public sealed class DiveGestureArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Vector2 _start;
        private float _startTime;
        private bool _fired;

        public void OnBeginDrag(PointerEventData e) { _start = e.position; _startTime = Time.unscaledTime; _fired = false; }

        public void OnDrag(PointerEventData e)
        {
            if (_fired || Game.Settings == null || !Game.Settings.Data.diveGesture) return;
            Vector2 d = e.position - _start;
            float t = Time.unscaledTime - _startTime;
            if (d.y < -Screen.height * 0.12f && Mathf.Abs(d.x) < Mathf.Abs(d.y) && t < 0.4f)
            {
                _fired = true;
                if (Game.Matches != null) Game.Matches.Input.GestureDive = true;
            }
        }

        public void OnEndDrag(PointerEventData e) { }
    }

    /// <summary>Lets the player drag a control while the HUD is in layout-edit mode.</summary>
    public sealed class DragHandle : MonoBehaviour, IDragHandler
    {
        public bool Editing;
        public System.Action<Vector2> Moved;

        public void OnDrag(PointerEventData e)
        {
            if (!Editing) return;
            var rt = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            rt.anchoredPosition += e.delta / scale;
            Moved?.Invoke(rt.anchoredPosition);
        }
    }
}
