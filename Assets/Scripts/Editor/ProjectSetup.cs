using UnityEditor;
using UnityEngine;

namespace Kitefall.EditorTools
{
    /// <summary>Project-wide settings from spec section 0 (landscape mobile game, Android first).</summary>
    public static class ProjectSetup
    {
        [MenuItem("Kitefall/Setup/Apply Mobile Player Settings", priority = 30)]
        public static void ApplyMobilePlayerSettings()
        {
            PlayerSettings.productName = "Kitefall";

            // Landscape only (spec 4.1), both directions so left-handed players can flip the phone.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[Kitefall] Player settings applied: product name 'Kitefall', landscape only.");
        }

        [MenuItem("Kitefall/Help/Open Game Spec", priority = 100)]
        public static void OpenSpec() => OpenDoc("docs/KITEFALL_GAME_SPEC.md");

        [MenuItem("Kitefall/Help/Open Milestone 1 Test Checklist", priority = 101)]
        public static void OpenChecklist() => OpenDoc("docs/MILESTONE_1.md");

        private static void OpenDoc(string relativePath)
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", relativePath));
            if (System.IO.File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("[Kitefall] Not found: " + path);
        }
    }
}
