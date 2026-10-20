using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// The real 3D character models (artist files in Assets/Art/Characters: &lt;id&gt;.glb or &lt;id&gt;.fbx, Mixamo-style rig) and
    /// their animation clips. Written by Kitefall ▸ Characters ▸ Setup Models into Resources/KitefallCharacters/CharacterModels.
    /// A character without an entry keeps the generated (code-built) body, so the game always works.
    /// </summary>
    public sealed class CharacterModelLibrary : ScriptableObject
    {
        public const string ResourcePath = "KitefallCharacters/CharacterModels";

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public GameObject model;
            /// <summary>Clip names (idle, walk, run, fly_hold, jump, climb, victory, clap, wave, dance, sad) parallel to <see cref="clips"/>.</summary>
            public string[] names = new string[0];
            public AnimationClip[] clips = new AnimationClip[0];
        }

        public List<Entry> entries = new List<Entry>();

        private static CharacterModelLibrary _loaded;
        private static bool _tried;

        public static CharacterModelLibrary Instance
        {
            get
            {
                if (!_tried || _loaded == null && !Application.isPlaying)
                {
                    _tried = true;
                    _loaded = Resources.Load<CharacterModelLibrary>(ResourcePath);
                }
                return _loaded;
            }
        }

        public static Entry Find(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            CharacterModelLibrary lib = Instance;
            if (lib == null) return null;
            foreach (Entry e in lib.entries)
                if (e != null && e.model != null && string.Equals(e.id, characterId, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        public static bool Has(string characterId) => Find(characterId) != null;

        /// <summary>Forget the cached library (the setup tool calls this after rebuilding it).</summary>
        public static void Reload() { _tried = false; _loaded = null; }
    }
}
