using System.Threading.Tasks;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.UI;
using Kitefall.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.App
{
    /// <summary>
    /// 2-minute offline tutorial (spec 3.1 step 5): (a) launch, (b) steer + pull/release, (c) read the wind,
    /// (d) cut a dummy kite's line. 20–30 s per step; can be skipped.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private enum Step { Launch, Steer, PullRelease, Wind, Cut, Done }

        public bool Active { get; private set; }
        private Step _step;
        private float _stepTime;
        private bool _up, _left, _right, _pulled, _released;
        private Text _title, _body;
        private Image[] _dots;
        private Image _picture;
        private GameObject _panel;

        public void Begin()
        {
            MatchSetup setup = MatchFactory.NewSetup(GameMode.Tutorial, ArenaType.OldCityRooftops, 12345);
            Game.Db.Configure(setup);
            PlayerProfile p = Game.Profile;
            var me = new ParticipantSetup { playerId = p.playerId, displayName = p.displayName, country = p.country };
            me.stack.Add(SpecDefaults.Loadout("guddi", "short_ribbon", "sooti", "lakri"));
            setup.Participants.Add(me);
            ParticipantSetup dummy = MatchFactory.Bot(1, BotDifficulty.Dummy, BotBuilds.Get(BuildArchetype.AllRounder), 0, 1);
            dummy.displayName = "Dummy kite";
            setup.Participants.Add(dummy);
            // Dummy is fragile so the first cut is achievable.
            ParticipantLoadout l = dummy.stack[0];
            l.durability = 25f;
            dummy.stack[0] = l;

            Active = true;
            _step = Step.Launch;
            _stepTime = 0f;
            Game.UI.CloseAllPopups();
            Game.Matches.StartOffline(setup, 0, session => _ = FinishAsync(session.Result));
            MatchHudScreen hud = Game.UI.Show(new MatchHudScreen());
            Game.Flow.OnTutorialMatchStarted(hud);
            Game.Matches.Presenter.EventRaised += OnEvent;
            BuildPanel(hud.Root);
            Show();
        }

        private void BuildPanel(RectTransform root)
        {
            Image panel = UiKit.Panel(root, new Color(0.05f, 0.08f, 0.14f, 0.92f), "Tutorial", false);
            _panel = panel.gameObject;
            // Onboarding illustrations (onboard_01_welcome …) on the left of the panel when installed; text-only otherwise.
            bool art = Art.Has("onboard_01_welcome") || Art.Has("onboard_02_fly") || Art.Has("onboard_03_cut");
            float pic = art ? 320f : 0f;
            panel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(1100 + pic, 200));
            if (art)
            {
                _picture = UiKit.Image(panel.transform, Color.white, null, "Picture");
                _picture.preserveAspect = true;
                _picture.rectTransform.Band(new Vector2(0, 0), new Vector2(0, 1), new Vector2(12, 12), new Vector2(pic, -12));
            }
            _title = UiKit.Label(panel.transform, "", 36, Theme.Accent, TextAnchor.UpperCenter, FontStyle.Bold);
            _title.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(20 + pic, -60), new Vector2(-20, -12));
            _body = UiKit.Label(panel.transform, "", 28, Color.white, TextAnchor.UpperCenter);
            _body.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 1), new Vector2(30 + pic, 40), new Vector2(-30, -66));
            RectTransform dots = UiKit.Row(panel.transform, 12, 0, TextAnchor.MiddleCenter);
            dots.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(300, 24));
            _dots = new Image[4];
            for (int i = 0; i < 4; i++) { _dots[i] = UiKit.Image(dots, Theme.Disabled, Sprites.Circle, "Dot"); _dots[i].Size(20, 20); }
            Button skip = UiKit.Button(panel.transform, "Skip", Skip, Theme.PanelLight, 22);
            skip.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 10), new Vector2(120, 50));
        }

        private void Show()
        {
            if (_title == null) return;
            switch (_step)
            {
                case Step.Launch: Set("1 · Launch", "Hold PULL (Space) and let go while the bar is in the GOLD zone for a higher start."); break;
                case Step.Steer: Set("2 · Steer", "Use the joystick (WASD) to fly UP, LEFT and RIGHT. The kite flies where its nose points."); break;
                case Step.PullRelease: Set("2 · Pull & release", $"Hold {Terms.PullUpper} to raise tension and speed. Let go to {Terms.ReleaseUpper}: tension falls and line goes out. REEL (Shift) stops it."); break;
                case Step.Wind: Set("3 · Read the wind", "The arrow under the score shows where the wind blows. Fly with it for power – and watch for GUST warnings."); break;
                case Step.Cut: Set("4 · Cut the dummy", "Fly across the dummy's line. When they touch, keep your tension marker inside the BLUE sweet zone to cut it!"); break;
            }
            if (_picture != null)
            {
                string key = _step == Step.Launch ? "onboard_01_welcome" : _step == Step.Cut ? "onboard_03_cut" : "onboard_02_fly";
                Sprite s = Art.Sprite(key) ?? Art.Sprite("onboard_02_fly") ?? Art.Sprite("onboard_01_welcome") ?? Art.Sprite("onboard_03_cut");
                _picture.sprite = s;
                _picture.enabled = s != null;
            }
            for (int i = 0; i < _dots.Length; i++)
            {
                int stepIndex = _step >= Step.Wind ? (int)_step - 1 : _step >= Step.PullRelease ? 1 : (int)_step;
                _dots[i].color = i < stepIndex ? Theme.Good : i == stepIndex ? Theme.Accent : Theme.Disabled;
            }
        }

        private void Set(string title, string body)
        {
            _title.text = title;
            _body.text = body;
        }

        private void Next()
        {
            _step++;
            _stepTime = 0f;
            Game.Audio?.Play(Services.Sfx.Click);
            if (_step == Step.Done) { Complete(); return; }
            Show();
        }

        private void Update()
        {
            if (!Active || Game.Matches.Session == null) return;
            _stepTime += Time.deltaTime;
            MatchSnapshot s = Game.Matches.Session.Current;
            KiteSnap me = s.Kites[0];
            KiteInput input = Game.Matches.Input.ReadInput();
            switch (_step)
            {
                case Step.Steer:
                    if (input.SteerY > 0.6f) _up = true;
                    if (input.SteerX < -0.6f) _left = true;
                    if (input.SteerX > 0.6f) _right = true;
                    if (_up && _left && _right || _stepTime > 30f) Next();
                    break;
                case Step.PullRelease:
                    if (me.Action == LineAction.Pull) _pulled = true;
                    if (_pulled && me.Action == LineAction.Dheel) _released = true;
                    if (_pulled && _released && _stepTime > 4f || _stepTime > 30f) Next();
                    break;
                case Step.Wind:
                    if (_stepTime > 8f) Next();
                    break;
            }
        }

        private void OnEvent(MatchEvent e)
        {
            if (!Active) return;
            if (_step == Step.Launch && e.Type == MatchEventType.Launched && e.A == 0) Next();
            if (_step == Step.Cut && e.Type == MatchEventType.KiteCut && e.B == 0) Next();
            if (_step == Step.Cut && e.Type == MatchEventType.KiteCut && e.A == 0) Game.Flow.Hud?.Announce("Try again!", "Keep the marker in the blue band");
        }

        private void Skip()
        {
            Game.UI.Confirm("Skip tutorial?", "You can replay it any time from Settings.", "Skip", Complete);
        }

        private void Complete()
        {
            if (!Active) return;
            if (Game.Matches.Session is OfflineMatchSession o) o.End();
        }

        /// <summary>Called by AppFlow when the tutorial match ended.</summary>
        public async Task FinishAsync(MatchResult result)
        {
            Active = false;
            Game.Matches.Presenter.EventRaised -= OnEvent;
            if (_panel != null) Destroy(_panel);
            if (result != null)
                await Game.Backend.SubmitMatchAsync(result, new MatchRewardContext { MatchId = "tutorial_" + Game.Now, Mode = GameMode.Tutorial, MyIndex = 0 });
            Game.Matches.EndSession();
            Game.UI.Message("Tutorial complete!", "Great flying. Now a real duel against a practice opponent.", () => _ = Game.Flow.ContinueOnboardingAsync());
        }
    }
}
