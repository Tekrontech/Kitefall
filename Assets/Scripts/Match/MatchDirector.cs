using System;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
using Kitefall.Input;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>Starts / runs / ends matches and the home showroom. Owns the input reader and presenter.</summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        public IMatchSession Session { get; private set; }
        public MatchPresenter Presenter { get; private set; }
        public KiteInputReader Input { get; private set; }
        public Showroom Showroom { get; private set; }
        public Camera Camera { get; private set; }
        public bool InMatch => Session != null;
        /// <summary>When true, local input is ignored (pause menu open, offline only).</summary>
        public bool InputBlocked { get; set; }

        private Action<IMatchSession> _onFinished;
        private float _endTimer = -1f;

        public void Init(Camera cam)
        {
            Camera = cam;
            Input = gameObject.AddComponent<KiteInputReader>();
            Presenter = gameObject.AddComponent<MatchPresenter>();
            Showroom = new GameObject("ShowroomHost").AddComponent<Showroom>();
            Showroom.transform.SetParent(transform, false);
            if (cam.GetComponent<MatchCamera>() == null) cam.gameObject.AddComponent<MatchCamera>();
        }

        public void ShowShowroom(string kiteId, string skinId, string tailId, string dorId)
        {
            if (InMatch) return;
            Showroom.Show(Camera, kiteId, skinId, tailId, dorId);
        }

        /// <summary>Home showroom with the player's character on the rooftop flying the kite (upgrade F1).</summary>
        public void ShowShowroom(string kiteId, string skinId, string tailId, string dorId, string characterId, string outfitId, string[] accessories, string charkhiId)
        {
            if (InMatch) return;
            Showroom.Show(Camera, kiteId, skinId, tailId, dorId, characterId, outfitId, accessories, charkhiId);
        }

        public void StartSession(IMatchSession session, Action<IMatchSession> onFinished)
        {
            Showroom.Hide();
            Session = session;
            _onFinished = onFinished;
            _endTimer = -1f;
            ArenaData arena = Game.Db != null ? Game.Db.Arena(session.Setup.Arena) : null;
            Presenter.Begin(session, arena, Camera);
            Input.Sensitivity = Game.Settings != null ? Game.Settings.Data.steerSensitivity : 1f;
            Game.Analytics?.Track("match_start", ("mode", session.Setup.Rules.mode.ToString()), ("arena", session.Setup.Arena.ToString()), ("players", session.Setup.Participants.Count));
        }

        public OfflineMatchSession StartOffline(MatchSetup setup, int localIndex, Action<IMatchSession> onFinished)
        {
            var s = new OfflineMatchSession(setup, localIndex);
            StartSession(s, onFinished);
            return s;
        }

        /// <summary>
        /// While walking (warmup plaza, roofs, upgrade F3/F9) the joystick moves the character relative to the camera,
        /// so the server receives a world-space direction (x, z) in SteerX / SteerY.
        /// </summary>
        private KiteInput WalkInput(KiteInput input)
        {
            MatchSnapshot s = Session.Current;
            int me = Session.LocalIndex;
            if (s == null || me < 0 || me >= s.Kites.Length) return input;
            FlyerState f = s.Kites[me].Flyer;
            bool walking = s.Phase == MatchPhase.Warmup || f == FlyerState.Grounded;
            if (!walking || Camera == null) return input;
            float yaw = Camera.transform.eulerAngles.y * Mathf.Deg2Rad;
            float x = input.SteerX, y = input.SteerY;
            input.SteerX = Mathf.Cos(yaw) * x + Mathf.Sin(yaw) * y;
            input.SteerY = -Mathf.Sin(yaw) * x + Mathf.Cos(yaw) * y;
            return input;
        }

        public void QuitMatch()
        {
            Session?.Quit();
        }

        /// <summary>Tears down the match world (after the results screen is shown).</summary>
        public void EndSession()
        {
            Presenter.End();
            Session?.Dispose();
            Session = null;
            InputBlocked = false;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (Session == null) return;
            KiteInput input = InputBlocked ? default : Input.ReadInput();
            input = WalkInput(input);
            Session.SetLocalInput(input);
            Session.Update(Time.deltaTime);

            if (Session.IsOver)
            {
                if (_endTimer < 0f) _endTimer = 1.6f;
                _endTimer -= Time.unscaledDeltaTime;
                if (_endTimer <= 0f && _onFinished != null)
                {
                    Action<IMatchSession> cb = _onFinished;
                    _onFinished = null;
                    Game.Analytics?.Track("match_end", ("mode", Session.Setup.Rules.mode.ToString()), ("winner", Session.Result?.WinnerIndex ?? -1));
                    cb(Session);
                }
            }
        }
    }
}
