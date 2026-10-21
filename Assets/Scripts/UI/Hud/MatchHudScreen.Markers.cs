using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Hud
{
    /// <summary>
    /// Kite markers (audit E1/E2): your own kite always has a ring + arrow on it, and when it leaves the screen the arrow
    /// waits at the screen edge pointing toward it. Other kites get a small team-coloured marker (triangle = rival,
    /// dot = teammate) with the name tag above it. Colour-blind mode uses the colour-blind palette. Visual only.
    /// </summary>
    public sealed partial class MatchHudScreen
    {
        private RectTransform _markers, _myMarker, _myArrowPivot, _myArrow;
        private Image _myRing, _myArrowImg;
        private readonly List<Image> _enemyMarkers = new List<Image>();
        private const float EdgeMargin = 64f;

        private void BuildMarkers()
        {
            _enemyMarkers.Clear();
            _markers = UiKit.Rect("KiteMarkers", Root).Stretch();
            _myMarker = UiKit.Rect("MyKite", _markers);
            _myMarker.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            _myRing = UiKit.Image(_myMarker, Theme.Accent, Sprites.Ring, "Ring");
            _myRing.rectTransform.Stretch();
            _myArrowPivot = UiKit.Rect("ArrowPivot", _myMarker);
            _myArrowPivot.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _myArrowImg = UiKit.Image(_myArrowPivot, Theme.Accent, Sprites.Triangle, "Arrow");
            _myArrow = _myArrowImg.rectTransform;
            _myArrow.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 46), new Vector2(30, 26));
            _myMarker.gameObject.SetActive(false);
        }

        /// <summary>Marker colour of kite <paramref name="i"/>: own orange; teams ally / rival; free-for-all = the flyer's ring colour.</summary>
        private Color MarkerColor(int i, int me, bool cb)
        {
            if (i == me) return Theme.Accent;
            if (S.Setup.Rules.teamCount > 1 && _teams != null && i < _teams.Length && me < _teams.Length)
                return _teams[i] == _teams[me] ? (cb ? Theme.ZoneSafe(true) : Theme.Accent2) : Theme.ZoneDanger(cb);
            if (cb) return Theme.ZoneDanger(true);
            CharacterView[] cv = Game.Matches.Presenter.Characters;
            return cv != null && i < cv.Length && cv[i] != null ? cv[i].TeamColor : Theme.Bad;
        }

        private bool IsAlly(int i, int me) =>
            S.Setup.Rules.teamCount > 1 && _teams != null && i < _teams.Length && me < _teams.Length && _teams[i] == _teams[me];

        /// <summary>How tall kite <paramref name="view"/> looks on screen, in canvas units.</summary>
        private float KiteCanvasHeight(Camera cam, KiteView view)
        {
            float d = Vector3.Distance(cam.transform.position, view.KitePosition);
            float world = cam.orthographic ? cam.orthographicSize * 2f : 2f * Mathf.Max(0.01f, d) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return view.VisualHeight / Mathf.Max(0.01f, world) * _markers.rect.height;
        }

        /// <summary>Screen point → marker-layer point. Returns false when the point is behind the camera.</summary>
        private bool ToMarkerSpace(Vector3 sp, out Vector2 local)
        {
            bool front = sp.z > 0f;
            if (!front)
            {
                // Behind the camera: mirror through the screen centre so the edge arrow still points the right way round.
                sp.x = Screen.width - sp.x;
                sp.y = Screen.height - sp.y;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_markers, sp, null, out local);
            local -= _markers.rect.center;
            return front;
        }

        private void UpdateMarkers(MatchSnapshot snap, int me, Camera cam, KiteView[] views)
        {
            if (_markers == null) return;
            bool cb = Game.ColorBlind;
            Vector2 half = _markers.rect.size * 0.5f - new Vector2(EdgeMargin, EdgeMargin);

            // ---- Own kite: ring on it, or an arrow at the screen edge ----
            bool mine = me >= 0 && me < views.Length && me < snap.Kites.Length && snap.Kites[me].IsFlying && views[me].Visible
                        && !Game.Matches.Presenter.Replaying;
            _myMarker.gameObject.SetActive(mine);
            if (mine)
            {
                bool front = ToMarkerSpace(cam.WorldToScreenPoint(views[me].KitePosition), out Vector2 p);
                bool onScreen = front && Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y;
                Color c = MarkerColor(me, me, cb);
                if (onScreen)
                {
                    float h = KiteCanvasHeight(cam, views[me]);
                    float size = Mathf.Clamp(h * 1.4f, 56f, 150f);
                    _myMarker.sizeDelta = new Vector2(size, size);
                    _myMarker.anchoredPosition = p + _markers.rect.center;
                    _myRing.gameObject.SetActive(true);
                    // A big, close kite needs no ring – keep it faint so it never hides the sail.
                    _myRing.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.9f, 0.25f, Mathf.InverseLerp(90f, 200f, h)));
                    _myArrowPivot.localRotation = Quaternion.identity;
                    _myArrow.anchoredPosition = new Vector2(0f, size * 0.5f + 16f);
                    _myArrow.localRotation = Quaternion.Euler(0f, 0f, 180f); // points down at the kite
                    _myArrowImg.color = c;
                }
                else
                {
                    Vector2 dir = p.sqrMagnitude > 1e-4f ? p : Vector2.down;
                    float t = Mathf.Min(half.x / Mathf.Max(1e-4f, Mathf.Abs(dir.x)), half.y / Mathf.Max(1e-4f, Mathf.Abs(dir.y)));
                    _myMarker.sizeDelta = new Vector2(56f, 56f);
                    _myMarker.anchoredPosition = dir * t + _markers.rect.center;
                    _myRing.gameObject.SetActive(true);
                    _myRing.color = new Color(c.r, c.g, c.b, 0.9f);
                    _myArrowPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                    _myArrow.anchoredPosition = new Vector2(0f, 42f);
                    _myArrow.localRotation = Quaternion.identity; // points out, toward the kite
                    float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 8f);
                    _myArrowImg.color = new Color(c.r, c.g, c.b, pulse);
                }
            }

            // ---- Other kites: small team-coloured marker on the kite (on screen only – the radar covers the rest) ----
            while (_enemyMarkers.Count < views.Length)
            {
                Image m = UiKit.Image(_markers, Color.white, Sprites.Triangle, "KiteMark");
                m.rectTransform.sizeDelta = new Vector2(24, 20);
                m.gameObject.SetActive(false);
                _enemyMarkers.Add(m);
            }
            for (int i = 0; i < views.Length; i++)
            {
                Image m = _enemyMarkers[i];
                bool show = i != me && i < snap.Kites.Length && snap.Kites[i].IsFlying && views[i].Visible;
                Vector2 p = Vector2.zero;
                if (show) show = ToMarkerSpace(cam.WorldToScreenPoint(views[i].KitePosition), out p) && Mathf.Abs(p.x) <= half.x + EdgeMargin && Mathf.Abs(p.y) <= half.y + EdgeMargin;
                m.gameObject.SetActive(show);
                if (!show) continue;
                bool ally = IsAlly(i, me);
                m.sprite = ally ? Sprites.Circle : Sprites.Triangle;
                m.rectTransform.sizeDelta = ally ? new Vector2(18, 18) : new Vector2(24, 20);
                m.rectTransform.localRotation = ally ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
                m.color = MarkerColor(i, me, cb);
                float h = KiteCanvasHeight(cam, views[i]);
                m.rectTransform.anchoredPosition = p + _markers.rect.center + new Vector2(0f, h * 0.5f + 14f);
            }
        }
    }
}
