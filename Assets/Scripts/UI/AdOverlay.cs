using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>Full-screen fake ad used by the debug ad provider (editor / offline).</summary>
    public static class AdOverlay
    {
        public static void Show(string title, float seconds, Action<bool> done)
        {
            UIManager ui = App.Game.UI;
            if (ui == null) { done?.Invoke(true); return; }
            RectTransform body = ui.Popup("", new Vector2(1600, 900), false);
            Text t = UiKit.Label(body, title + "\n\n<size=26>Test ad – no network configured</size>", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
            ui.StartCoroutine(Run(ui, body, t, title, seconds, done));
        }

        private static IEnumerator Run(UIManager ui, RectTransform body, Text t, string title, float seconds, Action<bool> done)
        {
            float left = seconds;
            while (left > 0f)
            {
                t.text = $"{title}\n\n<size=26>Test ad – no network configured · {left:0}s</size>";
                left -= Time.unscaledDeltaTime;
                yield return null;
            }
            ui.ClosePopupOf(body);
            done?.Invoke(true);
        }
    }
}
