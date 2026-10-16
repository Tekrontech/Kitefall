using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>Canvases, the screen stack, popups, toasts and the loading veil.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform PopupLayer { get; private set; }
        public RectTransform ToastLayer { get; private set; }
        public Canvas Canvas { get; private set; }

        private readonly List<UIScreen> _stack = new List<UIScreen>();
        private readonly List<GameObject> _popups = new List<GameObject>();
        private GameObject _busy;
        private InputAction _back;

        public UIScreen Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public event Action<UIScreen> ScreenChanged;

        public void Init()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)transform;
            HudLayer = UiKit.Rect("HUD", root).Stretch();
            ScreenLayer = UiKit.Rect("Screens", root).Stretch();
            PopupLayer = UiKit.Rect("Popups", root).Stretch();
            ToastLayer = UiKit.Rect("Toasts", root).Stretch();
            SafeArea.Apply(HudLayer);
            SafeArea.Apply(ScreenLayer);

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
                DontDestroyOnLoad(es);
            }

            _back = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
            _back.Enable();
        }

        private void OnDestroy() => _back?.Dispose();

        // ---- Screens ---------------------------------------------------------

        public T Push<T>(T screen) where T : UIScreen
        {
            if (Current != null) Current.Root.gameObject.SetActive(false);
            _stack.Add(screen);
            screen.Create(this, ScreenLayer);
            screen.OnShow();
            ScreenChanged?.Invoke(screen);
            return screen;
        }

        /// <summary>Clears the stack and shows <paramref name="screen"/>.</summary>
        public T Show<T>(T screen) where T : UIScreen
        {
            ClearStack();
            return Push(screen);
        }

        /// <summary>Replaces the top screen.</summary>
        public T Replace<T>(T screen) where T : UIScreen
        {
            if (_stack.Count > 0)
            {
                UIScreen top = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                top.Destroy();
            }
            return Push(screen);
        }

        public void Pop()
        {
            if (_stack.Count <= 1) return;
            UIScreen top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Destroy();
            UIScreen now = Current;
            if (now != null)
            {
                now.Root.gameObject.SetActive(true);
                now.Refresh();
                now.OnShow();
                ScreenChanged?.Invoke(now);
            }
        }

        public void ClearStack()
        {
            for (int i = _stack.Count - 1; i >= 0; i--) _stack[i].Destroy();
            _stack.Clear();
        }

        public void RefreshCurrent() => Current?.Refresh();

        private void Update()
        {
            Current?.Tick();
            if (_back != null && _back.WasPressedThisFrame())
            {
                if (_popups.Count > 0) { ClosePopup(_popups[_popups.Count - 1]); return; }
                if (Current != null && Current.OnBack()) Pop();
            }
        }

        // ---- Popups ----------------------------------------------------------

        /// <summary>Modal panel. Build the content into the returned panel's RectTransform.</summary>
        public RectTransform Popup(string title, Vector2 size, bool closeButton = true)
        {
            Image veil = UiKit.Image(PopupLayer, Theme.Overlay, Sprites.White, "Popup", true);
            veil.rectTransform.Stretch();
            Image panel = UiKit.Panel(veil.transform, Theme.Panel, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            if (!string.IsNullOrEmpty(title))
            {
                Text t = UiKit.Label(panel.transform, title, Theme.HeaderSize, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
                t.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -90), new Vector2(-20, -16));
            }
            if (closeButton)
            {
                GameObject go = veil.gameObject;
                UiKit.Button(panel.transform, "X", () => ClosePopup(go), Theme.PanelLight, 32).GetComponent<RectTransform>()
                    .Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(64, 64));
            }
            RectTransform body = UiKit.Rect("Body", panel.transform).Stretch(28, string.IsNullOrEmpty(title) ? 28 : 100, 28, 28);
            _popups.Add(veil.gameObject);
            return body;
        }

        public void ClosePopup(GameObject popup)
        {
            _popups.Remove(popup);
            if (popup != null) Destroy(popup);
        }

        /// <summary>Closes the popup that owns <paramref name="anyChild"/>.</summary>
        public void ClosePopupOf(Transform anyChild)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
                if (_popups[i] != null && anyChild.IsChildOf(_popups[i].transform)) { ClosePopup(_popups[i]); return; }
        }

        public void CloseAllPopups()
        {
            foreach (GameObject p in _popups) if (p != null) Destroy(p);
            _popups.Clear();
        }

        public void Message(string title, string text, Action ok = null)
        {
            RectTransform body = Popup(title, new Vector2(900, 480), false);
            Text t = UiKit.Label(body, text, Theme.BodySize, Theme.Text, TextAnchor.UpperCenter);
            t.rectTransform.Stretch(0, 0, 0, 100);
            UiKit.Button(body, "OK", () => { ClosePopupOf(body); ok?.Invoke(); }).GetComponent<RectTransform>()
                .Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(300, 84));
        }

        public void Confirm(string title, string text, string yes, Action onYes, string no = "Cancel", Action onNo = null)
        {
            RectTransform body = Popup(title, new Vector2(960, 500), false);
            Text t = UiKit.Label(body, text, Theme.BodySize, Theme.Text, TextAnchor.UpperCenter);
            t.rectTransform.Stretch(0, 0, 0, 110);
            RectTransform row = UiKit.Row(body, 24f, 0, TextAnchor.MiddleCenter);
            row.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(760, 90));
            UiKit.Button(row, no, () => { ClosePopupOf(body); onNo?.Invoke(); }, Theme.PanelLight).Size(340, 84);
            UiKit.Button(row, yes, () => { ClosePopupOf(body); onYes?.Invoke(); }).Size(340, 84);
        }

        // ---- Toasts & busy ---------------------------------------------------

        public void Toast(string text, Color? color = null, float seconds = 2.2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            Image bg = UiKit.Panel(ToastLayer, color ?? new Color(0.1f, 0.12f, 0.18f, 0.95f), "Toast", false);
            int index = ToastLayer.childCount - 1;
            bg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140 - index * 90), new Vector2(1100, 80));
            Text t = UiKit.Label(bg.transform, text, Theme.BodySize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch(16, 4, 16, 4);
            Destroy(bg.gameObject, seconds);
        }

        public void ToastResult(Kitefall.Core.OpResult r, string successText = null)
        {
            if (r == null) return;
            if (!r.Ok) Toast(r.Error, Theme.Bad);
            else if (!string.IsNullOrEmpty(successText)) Toast(successText, Theme.Good);
            else if (r.Ledger.Count > 0) Toast(string.Join("   ", r.Ledger), Theme.Good, 3f);
        }

        public void SetBusy(bool busy, string text = "Please wait…")
        {
            if (busy && _busy == null)
            {
                Image veil = UiKit.Image(ToastLayer, new Color(0, 0, 0, 0.45f), Sprites.White, "Busy", true);
                veil.rectTransform.Stretch();
                Text t = UiKit.Label(veil.transform, text, Theme.HeaderSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.Stretch();
                _busy = veil.gameObject;
            }
            else if (!busy && _busy != null)
            {
                Destroy(_busy);
                _busy = null;
            }
        }
    }

    /// <summary>Keeps UI inside the phone's safe area (notches, rounded corners).</summary>
    public static class SafeArea
    {
        public static void Apply(RectTransform rt)
        {
            Rect safe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = safe.position, max = safe.position + safe.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
