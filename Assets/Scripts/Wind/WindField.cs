using Kitefall.Core;
using Kitefall.Data;
using UnityEngine;

namespace Kitefall.Wind
{
    /// <summary>
    /// Scene wrapper around the pure-C# <see cref="WindModel"/>. Put one in every arena.
    /// Ground level is world y = 0 in milestone 1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindField : MonoBehaviour
    {
        [SerializeField] private WindConfig config;
        [Tooltip("Use a fixed seed instead of the one in the config (handy for repeatable tests).")]
        [SerializeField] private bool overrideSeed;
        [SerializeField] private int seed = 1234;

        private WindModel _model;

        public WindModel Model
        {
            get
            {
                if (_model == null) Build();
                return _model;
            }
        }

        /// <summary>Wind at the flyer's height right now (for HUD / compass).</summary>
        public WindSample Current => Sample(Time.time, transform.position.y);

        public WindSample Sample(float time, float heightAboveGround) => Model.Sample(time, heightAboveGround);

        /// <summary>Rebuilds the model, e.g. after editing the config in play mode.</summary>
        [ContextMenu("Rebuild Wind")]
        public void Build()
        {
            WindSettings source = config != null ? config.settings : new WindSettings();
            if (config == null) Debug.LogWarning("[WindField] No WindConfig assigned – using default wind.", this);

            // Copy so a seed override never edits the asset on disk.
            var settings = JsonUtility.FromJson<WindSettings>(JsonUtility.ToJson(source));
            if (overrideSeed) settings.seed = seed;
            _model = new WindModel(settings);
        }

        public static Vector3 ToWorldDirection(in WindSample sample) => new Vector3(sample.DirX, 0f, sample.DirZ);

        private void OnValidate()
        {
            if (Application.isPlaying && _model != null) Build();
        }

        private void OnDrawGizmos()
        {
            WindSample w = Application.isPlaying ? Current : Model.Sample(0f, transform.position.y);
            Vector3 dir = ToWorldDirection(w);
            Vector3 start = transform.position + Vector3.up * 2f;
            Vector3 end = start + dir * (w.Speed * 2f);
            Gizmos.color = w.IsStrong ? Color.red : Color.cyan;
            Gizmos.DrawLine(start, end);
            Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.8f;
            Gizmos.DrawLine(end, end - dir * 1.5f + side);
            Gizmos.DrawLine(end, end - dir * 1.5f - side);
        }
    }
}
