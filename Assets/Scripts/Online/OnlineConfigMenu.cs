#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Kitefall.Online
{
    internal static class OnlineConfigMenu
    {
        private const string Path = "Assets/Resources/KitefallOnlineConfig.asset";

        [MenuItem("Kitefall/Online/Create or Select Online Config", priority = 200)]
        private static void CreateOrSelect()
        {
            var config = AssetDatabase.LoadAssetAtPath<OnlineConfig>(Path);
            if (config == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                config = ScriptableObject.CreateInstance<OnlineConfig>();
                AssetDatabase.CreateAsset(config, Path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Kitefall/Online/Show Installed Online Packages", priority = 201)]
        private static void Report()
        {
            string s = "Kitefall online features compiled in:\n";
#if KITEFALL_UGS_AUTH
            s += "✓ Authentication\n";
#else
            s += "✗ Authentication (com.unity.services.authentication)\n";
#endif
#if KITEFALL_UGS_CLOUDCODE
            s += "✓ Cloud Code\n";
#else
            s += "✗ Cloud Code (com.unity.services.cloudcode)\n";
#endif
#if KITEFALL_UGS_LEADERBOARDS
            s += "✓ Leaderboards\n";
#else
            s += "✗ Leaderboards (com.unity.services.leaderboards)\n";
#endif
#if KITEFALL_UGS_MULTIPLAYER
            s += "✓ Multiplayer Services\n";
#else
            s += "✗ Multiplayer Services (com.unity.services.multiplayer)\n";
#endif
#if KITEFALL_NGO
            s += "✓ Netcode for GameObjects\n";
#else
            s += "✗ Netcode for GameObjects (com.unity.netcode.gameobjects)\n";
#endif
#if KITEFALL_UGS_REMOTECONFIG
            s += "✓ Remote Config\n";
#else
            s += "✗ Remote Config (com.unity.remote-config)\n";
#endif
#if KITEFALL_UGS_ANALYTICS
            s += "✓ Analytics\n";
#else
            s += "✗ Analytics (com.unity.services.analytics)\n";
#endif
#if KITEFALL_LEVELPLAY
            s += "✓ LevelPlay ads\n";
#else
            s += "✗ LevelPlay ads (com.unity.services.levelplay)\n";
#endif
#if KITEFALL_IAP
            s += "✓ In-App Purchasing\n";
#else
            s += "✗ In-App Purchasing (com.unity.purchasing)\n";
#endif
            EditorUtility.DisplayDialog("Kitefall online", s, "OK");
        }
    }
}
#endif
