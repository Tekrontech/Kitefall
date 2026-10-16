using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using Kitefall.UI;
using UnityEngine;

namespace Kitefall.App
{
    /// <summary>
    /// Entry point of the game. Put one in the Main scene (Kitefall ▸ Build Game Scene does it).
    /// Creates every service, the UI and the match director, then starts the player flow.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameApp : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;
        [SerializeField] private Camera mainCamera;
        [Tooltip("Ignore an installed online backend and use the local save (testing).")]
        [SerializeField] private bool forceOffline;

        private void Awake()
        {
            if (Game.App != null && Game.App != this) { Destroy(gameObject); return; }
            if (database == null) { Debug.LogError("[Kitefall] GameApp has no GameDatabase. Run Kitefall ▸ Build Game Scene."); enabled = false; return; }

            Game.App = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_SERVER
            // Dedicated server: no UI, audio or camera – just data + the server match loop.
            Game.Db = database;
            database.ApplyToCore();
            Application.targetFrameRate = 30;
            if (ServerRegistry.Start != null) ServerRegistry.Start(database);
            else Debug.LogError("[Kitefall] Server build without Kitefall.Online (Netcode) – nothing to run.");
            return;
#else
            Application.runInBackground = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Game.Db = database;
            database.ApplyToCore();
            Game.Settings = new SettingsService();
            Game.Settings.Load();

            Game.Audio = gameObject.AddComponent<AudioService>();
            Game.Audio.Init(Game.Settings);
            Game.Haptics = new HapticsService(Game.Settings);

            EconomyBundle bundle = database.balance != null ? database.balance.Bundle() : new EconomyBundle();
            Game.Backend = !forceOffline && BackendRegistry.OnlineFactory != null ? BackendRegistry.OnlineFactory() : new LocalBackendService(bundle);
            Debug.Log("[Kitefall] Backend: " + Game.Backend.Name);
            Game.Ads = new AdsService(Game.Backend, bundle.ads);
            Game.Purchases = new PurchaseService(Game.Backend);
            Game.Analytics = new AnalyticsService();
            Game.Remote = new RemoteConfigService();
            Game.Share = gameObject.AddComponent<ShareService>();

            var uiGo = new GameObject("UI", typeof(RectTransform));
            DontDestroyOnLoad(uiGo);
            Game.UI = uiGo.AddComponent<UIManager>();
            Game.UI.Init();
            UiKit.ClickSound = () => Game.Audio.Play(Sfx.Click, 0.6f);
            DebugAdsProvider.ShowOverlay = AdOverlay.Show;

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            DontDestroyOnLoad(mainCamera.gameObject);
            var matches = new GameObject("Matches");
            DontDestroyOnLoad(matches);
            Game.Matches = matches.AddComponent<MatchDirector>();
            Game.Matches.Init(mainCamera);

            Game.Flow = new AppFlow();
#endif
        }

        private async void Start()
        {
            if (Game.Flow == null) return;
            try
            {
                await Game.Flow.BootAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Game.UI.Message("Something went wrong", e.Message);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Game.Settings?.Save();
        }
    }
}
