using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>
    /// "Share" on the results screen: saves a screenshot of the moment and opens the system share sheet with text.
    /// (A 10-second video clip needs a native recorder plugin – see docs/DECISIONS.md.)
    /// </summary>
    public sealed class ShareService : MonoBehaviour
    {
        public string LastScreenshotPath { get; private set; }

        public void ShareResult(string text, string storeUrl)
        {
            StartCoroutine(Capture(text + " " + storeUrl));
        }

        private IEnumerator Capture(string text)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "shots");
                Directory.CreateDirectory(dir);
                LastScreenshotPath = Path.Combine(dir, $"kitefall_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                File.WriteAllBytes(LastScreenshotPath, tex.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] " + e.Message);
            }
            finally
            {
                Destroy(tex);
            }
            ShareText(text);
        }

        public static void ShareText(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Kitefall"))
                        activity.Call("startActivity", chooser);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Share] " + e.Message); }
#else
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("[Share] Copied to clipboard: " + text);
#endif
        }
    }
}
