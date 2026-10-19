using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Splash (spec 3.1 step 1): logo, wind sound, preload.</summary>
    public sealed class SplashScreen : UIScreen
    {
        private RectTransform _pulse;

        protected override void Build()
        {
            Image bg = UiKit.Image(Root, Theme.Background, Sprites.White, "Bg", true);
            bg.rectTransform.Stretch();
            Sprite splash = Art.Sprite("splash_screen");
            if (splash != null) MenuScreen.ArtBackground(Root, splash);
            else
            {
                Image glow = UiKit.Image(Root, new Color(1f, 0.55f, 0.1f, 0.35f), Sprites.VerticalFade, "Glow");
                glow.rectTransform.Band(Vector2.zero, new Vector2(1, 0.6f), Vector2.zero, Vector2.zero);
            }
            // Logo artwork is the ornamental frame (AI cannot letter text); the word is drawn on top of it.
            Sprite logoArt = Art.Sprite("logo_kitefall");
            RectTransform holder = UiKit.Rect("Logo", Root);
            holder.Place(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 550));
            if (logoArt != null)
            {
                Image frame = UiKit.Image(holder, Color.white, logoArt, "Frame");
                frame.preserveAspect = true;
                frame.rectTransform.Stretch();
            }
            Text logoText = UiKit.Label(holder, "KITEFALL", logoArt != null ? 110 : 140, logoArt != null ? new Color(0.36f, 0.16f, 0.05f) : Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            if (logoArt == null) logoText.Shadowed();
            logoText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 200));
            _pulse = holder;
            Text tag = UiKit.Label(Root, "Read the wind, win the cross, cut the line.", 36, Theme.TextDim, TextAnchor.MiddleCenter);
            tag.rectTransform.Place(new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 60));
            Text ver = UiKit.Label(Root, "v" + Application.version, 22, Theme.TextDim, TextAnchor.LowerRight);
            ver.rectTransform.Stretch(0, 0, 30, 24);
            Game.Audio?.SetAmbience(0.6f, 0f, 0f);
        }

        public override void Tick()
        {
            if (_pulse != null) _pulse.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(Time.unscaledTime * 2f));
        }

        public override bool OnBack() => false;
    }

    public sealed class ForceUpdateScreen : UIScreen
    {
        protected override void Build()
        {
            UiKit.Image(Root, Theme.Background, Sprites.White, "Bg", true).rectTransform.Stretch();
            Text t = UiKit.Label(Root, "Update required\n\n<size=32>A new version of Kitefall is available. Please update to keep playing.</size>", 56, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 400));
            UiKit.Button(Root, "Update", () => Application.OpenURL(Game.Balance.storeUrl)).GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 110));
        }

        public override bool OnBack() => false;
    }

    /// <summary>Login (spec 3.1 step 2): Google (Android) · Apple (iOS) · Guest.</summary>
    public sealed class LoginScreen : UIScreen
    {
        protected override void Build()
        {
            UiKit.Image(Root, new Color(0.04f, 0.07f, 0.12f, 0.92f), Sprites.White, "Bg", true).rectTransform.Stretch();
            Text logo = UiKit.Label(Root, "KITEFALL", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            logo.rectTransform.Place(new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 160));
            RectTransform col = UiKit.Column(Root, 22, 0, TextAnchor.UpperCenter);
            col.Place(new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 420));
#if UNITY_IOS
            UiKit.Button(col, "Sign in with Apple", () => _ = Game.Flow.SignInAsync(SignInMethod.Apple), Color.black, 34).Size(-1, 110);
#else
            UiKit.Button(col, "Continue with Google", () => _ = Game.Flow.SignInAsync(SignInMethod.Google), Color.white, 34).Size(-1, 110);
            col.GetChild(col.childCount - 1).GetComponentInChildren<Text>().color = new Color(0.15f, 0.15f, 0.2f);
#endif
            UiKit.Button(col, "Play as Guest", () =>
                UI.Confirm("Play as Guest?", "Guest progress can be lost if you change phones or reinstall. You can link an account later in Settings ▸ Account.",
                    "Play as Guest", () => _ = Game.Flow.SignInAsync(SignInMethod.Guest)), Theme.PanelLight, 34).Size(-1, 110);
            Text note = UiKit.Label(col, Game.Backend.IsOnline ? "" : "Offline mode: progress is saved on this device.", 24, Theme.TextDim, TextAnchor.MiddleCenter);
            note.Size(-1, 60);
        }

        public override bool OnBack() => false;
    }

    /// <summary>Terms + age gate + ads consent (spec 3.1 step 3, 13.4).</summary>
    public sealed class TermsScreen : UIScreen
    {
        private bool _adult = true;
        private bool _personalised;

        protected override void Build()
        {
            UiKit.Image(Root, new Color(0.04f, 0.07f, 0.12f, 0.95f), Sprites.White, "Bg", true).rectTransform.Stretch();
            Image panel = UiKit.Panel(Root, Theme.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 860));
            RectTransform col = UiKit.Column(panel.transform, 18, 40);
            col.Stretch();
            UiKit.Label(col, "Before you fly", Theme.TitleSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Size(-1, 80);
            UiKit.Label(col, "Kitefall is a game. String cutting is a fictional game mechanic – never use coated, metal or chemical string in real life.", Theme.BodySize, Theme.Warn, TextAnchor.MiddleCenter).Size(-1, 90);
            RectTransform links = UiKit.Row(col, 20, 0, TextAnchor.MiddleCenter);
            links.Size(-1, 80);
            UiKit.Button(links, "Terms of Service", () => Application.OpenURL(Game.Balance.termsUrl), Theme.PanelLight, 26).Size(360, 72);
            UiKit.Button(links, "Privacy Policy", () => Application.OpenURL(Game.Balance.privacyUrl), Theme.PanelLight, 26).Size(360, 72);
            UiKit.Choice(col, "Your age", new[] { "13 or older", "Under 13" }, _adult ? 0 : 1, i => { _adult = i == 0; if (!_adult) _personalised = false; });
            UiKit.Toggle(col, "Personalised ads (optional)", _personalised, v => _personalised = v && _adult);
            UiKit.Label(col, "Under-13 players only get non-personalised ads. You can change this later in Settings.", Theme.SmallSize, Theme.TextDim, TextAnchor.MiddleCenter).Size(-1, 50);
            UiKit.Spacer(col, -1, 10);
            UiKit.Button(col, "I accept the Terms and Privacy Policy", () => _ = Game.Flow.AcceptTermsAsync(!_adult, _personalised), Theme.Good, 32).Size(-1, 100);
        }

        public override bool OnBack() => false;
    }

    /// <summary>Region + name (spec 3.1 step 4): region auto-detected, 3–16 chars, profanity filter, country flag.</summary>
    public sealed class ProfileSetupScreen : UIScreen
    {
        private string _name;
        private int _region;
        private int _country;
        private Text _error;

        private static readonly string[] Countries = { "PK", "IN", "AF", "JP", "TH", "BR", "ID", "MY", "AE", "SA", "GB", "US", "BD", "NP", "CN", "TR", "DE", "FR", "EG", "NG" };

        public ProfileSetupScreen()
        {
            string guess = GuessCountry();
            _country = System.Array.IndexOf(Countries, guess);
            if (_country < 0) _country = 0;
            _region = System.Array.IndexOf(MatchmakingRules.Regions, MatchmakingRules.RegionFromCountry(Countries[_country]));
            if (_region < 0) _region = 0;
            _name = Game.Profile != null ? Game.Profile.displayName : "";
        }

        private static string GuessCountry()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Japanese: return "JP";
                case SystemLanguage.Thai: return "TH";
                case SystemLanguage.Portuguese: return "BR";
                case SystemLanguage.Indonesian: return "ID";
                case SystemLanguage.Arabic: return "AE";
                case SystemLanguage.Turkish: return "TR";
                case SystemLanguage.German: return "DE";
                case SystemLanguage.French: return "FR";
                case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: return "CN";
                default: return "PK";
            }
        }

        protected override void Build()
        {
            UiKit.Image(Root, new Color(0.04f, 0.07f, 0.12f, 0.9f), Sprites.White, "Bg", true).rectTransform.Stretch();
            Image panel = UiKit.Panel(Root, Theme.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 820));
            RectTransform col = UiKit.Column(panel.transform, 20, 40);
            col.Stretch();
            UiKit.Label(col, "Who's flying?", Theme.TitleSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Size(-1, 80);
            UiKit.Label(col, "Display name (3–16 characters)", Theme.BodySize, Theme.TextDim).Size(-1, 40);
            UiKit.Input(col, "Your name", _name, NameRules.MaxLength, v => _name = v).Size(-1, 84);
            _error = UiKit.Label(col, "", Theme.SmallSize, Theme.Bad);
            _error.Size(-1, 34);
            UiKit.Stepper(col, "Country", Countries[_country], () => { _country = (_country - 1 + Countries.Length) % Countries.Length; Refresh(); },
                () => { _country = (_country + 1) % Countries.Length; Refresh(); });
            UiKit.Choice(col, "Region", MatchmakingRules.Regions, _region, i => _region = i);
            UiKit.Spacer(col, -1, 10);
            UiKit.Button(col, "Start flying", Submit, Theme.Good, 34).Size(-1, 100);
        }

        private void Submit()
        {
            string err = NameRules.Validate(_name);
            if (err != null) { _error.text = err; return; }
            _ = Game.Flow.FinishProfileSetupAsync(_name, MatchmakingRules.Regions[_region], Countries[_country]);
        }

        public override bool OnBack() => false;
    }

    /// <summary>Spec 3.3: no connection – only tutorial and hangar viewing.</summary>
    public sealed class NoConnectionScreen : UIScreen
    {
        protected override void Build()
        {
            UiKit.Image(Root, new Color(0.04f, 0.07f, 0.12f, 0.92f), Sprites.White, "Bg", true).rectTransform.Stretch();
            Text t = UiKit.Label(Root, "No connection", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 120));
            RectTransform row = UiKit.Row(Root, 24, 0, TextAnchor.MiddleCenter);
            row.Place(new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 110));
            UiKit.Button(row, "Retry", () => _ = Game.Flow.BootAsync(), Theme.Accent).Size(360, 100);
            UiKit.Button(row, "Tutorial", () => Game.Flow.StartTutorial(), Theme.PanelLight).Size(360, 100);
            UiKit.Button(row, "Hangar", () => UI.Push(new HangarScreen()), Theme.PanelLight).Size(360, 100);
        }
    }
}
