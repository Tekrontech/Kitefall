using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Settings (spec 4.4): controls, camera, graphics, audio, haptics, gameplay, account, developer.</summary>
    public sealed class SettingsScreen : MenuScreen
    {
        public override string Title => "Settings";
        private static int _tab;

        protected override void BuildContent(RectTransform content)
        {
            SettingsData d = Game.Settings.Data;
            RectTransform tabs = UiKit.Rect("Tabs", content);
            tabs.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), Vector2.zero);
            UiKit.Tabs(tabs, new[] { "Controls", "Camera", "Graphics", "Audio", "Gameplay", "Account" }, _tab, i => { _tab = i; Refresh(); });
            RectTransform host = UiKit.Rect("List", content);
            host.Band(Vector2.zero, Vector2.one, new Vector2(240, 0), new Vector2(-240, -90));
            UiKit.Scroll(host, out RectTransform col, true, 10).GetComponent<RectTransform>().Stretch();

            switch (_tab)
            {
                case 0:
                    UiKit.Choice(col, "Layout", new[] { "Two thumbs", "One thumb" }, d.oneThumb ? 1 : 0, i => { d.oneThumb = i == 1; Save(); });
                    UiKit.Toggle(col, "Left-handed (mirror controls)", d.leftHanded, v => { d.leftHanded = v; Save(); });
                    Step(col, "Joystick size", d.joystickScale, 0.7f, 1.4f, 0.1f, v => d.joystickScale = v, "×0.0");
                    Step(col, "Button size", d.buttonScale, 0.7f, 1.4f, 0.1f, v => d.buttonScale = v, "×0.0");
                    Step(col, "Steering sensitivity", d.steerSensitivity, 0.5f, 2f, 0.1f, v => d.steerSensitivity = v, "×0.0");
                    UiKit.Toggle(col, "Dive gesture (swipe down on the right)", d.diveGesture, v => { d.diveGesture = v; Save(); });
                    UiKit.Button(col, "Reset button positions", () => { d.customLayout.Clear(); Save(); UI.Toast("Layout reset"); }, Theme.PanelLight, 24).Size(-1, 70);
                    UiKit.Label(col, "Drag buttons: open the in-match menu ▸ Edit controls.", 22, Theme.TextDim).Size(-1, 40);
                    break;
                case 1:
                    Step(col, "Camera distance", d.cameraDistance, 8f, 30f, 1f, v => d.cameraDistance = v, "0 m");
                    Step(col, "Camera follow", d.cameraFollow, 0f, 1f, 0.1f, v => d.cameraFollow = v, "0.0");
                    UiKit.Toggle(col, "Camera shake", d.cameraShake, v => { d.cameraShake = v; Save(); });
                    break;
                case 2:
                    UiKit.Choice(col, "Quality", new[] { "Auto", "Low", "Medium", "High" }, (int)d.quality, i => { d.quality = (GraphicsQuality)i; Save(); });
                    UiKit.Choice(col, "Frame rate", new[] { "30", "60", "120" }, d.fps == 30 ? 0 : d.fps == 120 ? 2 : 1, i => { d.fps = i == 0 ? 30 : i == 2 ? 120 : 60; Save(); });
                    UiKit.Toggle(col, "Battery saver (30 FPS)", d.batterySaver, v => { d.batterySaver = v; Save(); });
                    UiKit.Label(col, $"Using: {Game.Settings.EffectiveQuality} · tail segments {Game.Settings.TailSegments} · device RAM {SystemInfo.systemMemorySize} MB", 22, Theme.TextDim).Size(-1, 40);
                    break;
                case 3:
                    Step(col, "Master volume", d.masterVolume, 0f, 1f, 0.1f, v => d.masterVolume = v, "0%", 100f);
                    Step(col, "Music", d.musicVolume, 0f, 1f, 0.1f, v => d.musicVolume = v, "0%", 100f);
                    Step(col, "Sound effects", d.sfxVolume, 0f, 1f, 0.1f, v => d.sfxVolume = v, "0%", 100f);
                    UiKit.Toggle(col, "Haptics", d.haptics, v => { d.haptics = v; Save(); });
                    break;
                case 4:
                    UiKit.Choice(col, "Contest meter", new[] { "Bar", "Bar + numbers" }, d.contestMeterStyle, i => { d.contestMeterStyle = i; Save(); });
                    UiKit.Toggle(col, "Sweet-zone highlight", d.sweetZoneHighlight, v => { d.sweetZoneHighlight = v; Save(); });
                    UiKit.Toggle(col, "Colour-blind mode", d.colorBlind, v => { d.colorBlind = v; Save(); });
                    UiKit.Button(col, "Replay tutorial", () => { UI.ClearStack(); Game.Flow.StartTutorial(); }, Theme.PanelLight, 24).Size(-1, 70);
                    break;
                default:
                    Account(col, d);
                    break;
            }
        }

        private void Account(RectTransform col, SettingsData d)
        {
            PlayerProfile p = Game.Profile;
            UiKit.Label(col, $"Backend: {Game.Backend.Name}\nAds: {Game.Ads.Provider?.Name}\nStore: {Game.Purchases.Provider?.Name}\nPlayer ID: {p?.playerId}", 24, Theme.TextDim).Size(-1, 140);
            UiKit.Toggle(col, "Personalised ads", p != null && p.personalizedAdsConsent, v => _ = Game.Backend.RunAsync(EconomyCommands.AcceptTerms, p.under13 ? "1" : "0", v ? "1" : "0"));
            UiKit.Button(col, "Profile & account linking", () => UI.Push(new ProfileScreen()), Theme.PanelLight, 24).Size(-1, 70);
            UiKit.Button(col, "Terms of Service", () => Application.OpenURL(Game.Balance.termsUrl), Theme.PanelLight, 24).Size(-1, 70);
            UiKit.Button(col, "Privacy Policy", () => Application.OpenURL(Game.Balance.privacyUrl), Theme.PanelLight, 24).Size(-1, 70);
            UiKit.Label(col, "Developer (testing only)", 28, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            UiKit.Toggle(col, "Bot Ranked (no rank points)", d.devBotRanked, v => { d.devBotRanked = v; Save(); });
            if (Game.Backend is LocalBackendService local)
                UiKit.Button(col, "Delete local save", () => UI.Confirm("Delete save?", "All offline progress on this device will be erased.", "Delete", () =>
                {
                    local.DeleteSave();
                    UI.ClearStack();
                    _ = Game.Flow.BootAsync();
                }), Theme.Bad, 24).Size(-1, 70);
        }

        private void Step(RectTransform col, string label, float value, float min, float max, float step, System.Action<float> set, string format, float displayMult = 1f)
        {
            UiKit.Stepper(col, label, (value * displayMult).ToString(format),
                () => { set(Mathf.Clamp(Mathf.Round((value - step) / step) * step, min, max)); Save(); Refresh(); },
                () => { set(Mathf.Clamp(Mathf.Round((value + step) / step) * step, min, max)); Save(); Refresh(); });
        }

        private static void Save()
        {
            Game.Settings.Save();
            Game.Matches?.Presenter?.ApplyCameraSettings();
            if (Game.Matches?.Input != null) Game.Matches.Input.Sensitivity = Game.Settings.Data.steerSensitivity;
        }
    }
}
