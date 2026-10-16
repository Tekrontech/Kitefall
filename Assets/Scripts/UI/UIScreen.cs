using UnityEngine;

namespace Kitefall.UI
{
    /// <summary>Base class for a full screen. Build() creates the UI under Root; Refresh() rebuilds it.</summary>
    public abstract class UIScreen
    {
        public RectTransform Root { get; private set; }
        protected UIManager UI { get; private set; }

        /// <summary>Hides the 3D background (used by in-match overlays, results…).</summary>
        public virtual bool Opaque => true;
        public virtual string Title => GetType().Name.Replace("Screen", "");

        internal void Create(UIManager ui, RectTransform parent)
        {
            UI = ui;
            Root = UiKit.Rect(GetType().Name, parent).Stretch();
            Build();
        }

        protected abstract void Build();

        /// <summary>Destroys and rebuilds the content (cheap enough for menus).</summary>
        public virtual void Refresh()
        {
            if (Root == null) return;
            UiKit.Clear(Root);
            Build();
        }

        public virtual void OnShow() { }
        public virtual void OnHide() { }
        /// <summary>Return false to block the Android back button.</summary>
        public virtual bool OnBack() => true;
        public virtual void Tick() { }

        internal void Destroy()
        {
            OnHide();
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null;
        }

        public bool IsAlive => Root != null;
    }
}
