using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Services
{
    public enum GraphicsQuality { Auto, Low, Medium, High }

    /// <summary>Upgrade W2: Auto (shoulder, kite cam in contests), shoulder only, kite only.</summary>
    public enum CameraStyle { Auto, ShoulderOnly, KiteOnly }

    [Serializable]
    public class ControlPosition
    {
        public string id;
        public Vector2 anchoredPosition;
    }

    /// <summary>Player settings (spec 4.4 Settings). Stored on the device only.</summary>
    [Serializable]
    public class SettingsData
    {
        // Controls
        public bool leftHanded;
        public bool oneThumb;
        public float joystickScale = 1f;
        public float buttonScale = 1f;
        public float steerSensitivity = 1f;
        public bool diveGesture = true;
        public List<ControlPosition> customLayout = new List<ControlPosition>();

        // Camera
        public float cameraDistance = 16f;
        public float cameraFollow = 0.5f;
        public bool cameraShake = true;
        public CameraStyle cameraStyle = CameraStyle.Auto;

        // Graphics
        public GraphicsQuality quality = GraphicsQuality.Auto;
        public int fps = 60;
        public bool batterySaver;

        // Audio
        public float masterVolume = 1f;
        public float musicVolume = 0.6f;
        public float sfxVolume = 1f;

        // Haptics
        public bool haptics = true;

        // Gameplay
        /// <summary>0 = bar, 1 = numbers.</summary>
        public int contestMeterStyle;
        public bool sweetZoneHighlight = true;
        public bool colorBlind;
        /// <summary>Plays the launch ritual automatically with "Good" results (upgrade 11.2).</summary>
        public bool autoPrep;
        /// <summary>0 = International words (Release / Pull / Line / Spool), 1 = South Asian (Dheel / Kheench / Dor / Charkhi).</summary>
        public int kiteWords;
        /// <summary>Home screen scene: 2 = painted design picture (animated, default), 0 = 3D world harbour, 1 = 3D old city rooftops.</summary>
        public int homeTheme = 2;
        /// <summary>Settings format version (2 = painted home picture became the default).</summary>
        public int version = 2;
        /// <summary>Win/lose animation on round end (upgrade F6).</summary>
        public bool autoEmotes = true;
        public bool quickChat = true;

        // Voice (upgrade F7)
        public bool voicePushToTalk = true;
        public bool voiceSpeaker = true;
        public bool muteStrangers = true;

        // Developer
        public bool devBotRanked;
        public bool devShowDebug;
    }

    public sealed class SettingsService
    {
        private const string Key = "kitefall.settings.v1";
        public SettingsData Data { get; private set; } = new SettingsData();
        public event Action Changed;

        public void Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    // Older saves have no "version" field: start them at 0 so the upgrade below runs once.
                    if (!json.Contains("\"version\"")) json = json.TrimEnd().TrimEnd('}') + ",\"version\":0}";
                    Data = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
                }
                if (Data.version < 2)
                {
                    Data.homeTheme = 2; // the owner's painted home design is the default home scene from now on
                    Data.version = 2;
                    PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] Could not read settings, using defaults. " + e.Message);
                Data = new SettingsData();
            }
            Apply();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        public GraphicsQuality EffectiveQuality
        {
            get
            {
                if (Data.quality != GraphicsQuality.Auto) return Data.quality;
                int mem = SystemInfo.systemMemorySize;
                if (mem > 0 && mem <= 3500) return GraphicsQuality.Low;      // 3 GB phones (spec 0)
                if (mem > 0 && mem <= 6500) return GraphicsQuality.Medium;
                return GraphicsQuality.High;
            }
        }

        /// <summary>Tail rope segments per quality (spec 8.2: 6 / 10 / 14).</summary>
        public int TailSegments => EffectiveQuality == GraphicsQuality.Low ? 6 : EffectiveQuality == GraphicsQuality.Medium ? 10 : 14;

        public void Apply()
        {
            Kitefall.Core.Terms.Words = (Kitefall.Core.KiteWords)Mathf.Clamp(Data.kiteWords, 0, 1);
            int fps = Data.batterySaver ? 30 : Data.fps;
            if (EffectiveQuality == GraphicsQuality.Low) fps = Mathf.Min(fps, 30);
            Application.targetFrameRate = fps;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = Mathf.Clamp01(Data.masterVolume);
            QualitySettings.shadows = EffectiveQuality == GraphicsQuality.Low ? ShadowQuality.Disable
                : EffectiveQuality == GraphicsQuality.Medium ? ShadowQuality.HardOnly : ShadowQuality.All;
            KitefallGraphics.Apply(EffectiveQuality);
        }
    }
}
