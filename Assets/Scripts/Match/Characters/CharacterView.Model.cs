using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Real 3D model support for <see cref="CharacterView"/>: when Kitefall ▸ Characters ▸ Setup Models has found a model for
    /// the selected character, the generated body is hidden and the model is shown instead. The generated rig keeps running
    /// invisibly – it still works out where the hands, charkhi and kite should be – and the model's clips plus hand IK follow
    /// it, so every game system (line anchor, ritual, traversal, emotes) works the same with or without models.
    /// </summary>
    public sealed partial class CharacterView
    {
        private CharacterModel _model;

        /// <summary>True when the character is shown with the artist's 3D model.</summary>
        public bool HasModel => _model != null;

        private void InitModel(CharacterBuilder.Look look)
        {
            if (string.IsNullOrEmpty(look.CharacterId)) return;
            _model = CharacterModel.TryCreate(look.CharacterId, transform, Rig.Scale);
            if (_model == null) return;
            foreach (Renderer r in Rig.Root.GetComponentsInChildren<Renderer>(true))
            {
                bool prop = r.transform.IsChildOf(Rig.CharkhiSocket) || r.transform.IsChildOf(Rig.HeldKiteSocket);
                if (!prop) r.enabled = false;
            }
            _details.Clear();
            // Name tag and chat bubble above the model's own head.
            float h = _model.Height;
            _nameRoot.localPosition = new Vector3(0f, h + 0.4f, 0f);
            _bubbleRoot.localPosition = new Vector3(0f, h + 0.8f, 0f);
        }

        private void DriveModel(in CharacterFrame f, in Pose p, float dt)
        {
            bool flying = f.State == FlyerState.Flying || f.State == FlyerState.PackingUp || f.State == FlyerState.Prep;
            string clip = "idle";
            float speed = 1f;
            bool ik = false;
            float look = 0f;
            Vector3 lookAt = transform.position + transform.forward * 10f + Vector3.up * 3f;

            switch (f.State)
            {
                case FlyerState.Flying:
                case FlyerState.PackingUp:
                    clip = "fly_hold";
                    ik = true;
                    if (f.KiteValid) { lookAt = f.KitePosition; look = 1f; }
                    break;
                case FlyerState.Prep:
                    bool kiteUp = f.PrepStep == PrepStep.Tunke || f.PrepStep == PrepStep.Climb || f.PrepStep == PrepStep.Untangle;
                    clip = kiteUp ? "fly_hold" : "idle";
                    ik = true;
                    if (kiteUp && _prepKite != null) { lookAt = _prepKite.position; look = 1f; }
                    break;
                case FlyerState.Grounded:
                    clip = p.WalkSpeed > 0.15f ? (p.Run ? "run" : "walk") : "idle";
                    ik = p.WalkSpeed <= 0.15f;
                    break;
                case FlyerState.Traversing:
                    clip = p.ClimbPose ? "climb" : f.LinkKind == RoofLinkKind.Jump && p.WalkSpeed <= 0f ? "jump" : p.Run ? "run" : "walk";
                    break;
                default:
                    clip = p.WalkSpeed > 0.15f ? (p.Run ? "run" : "walk") : "idle";
                    break;
            }
            if (clip == "walk") speed = Mathf.Clamp(p.WalkSpeed / 1.4f, 0.6f, 1.7f);
            else if (clip == "run") speed = Mathf.Clamp(p.WalkSpeed / 4f, 0.7f, 1.5f);

            // Emotes and win / lose reactions use the model's own clips when it has one (never while flying: the hands keep the line).
            if (!flying && f.State != FlyerState.Traversing)
            {
                string emoteClip = EmoteClip(_emote);
                if (!string.IsNullOrEmpty(_emote))
                {
                    if (emoteClip != null && _model.HasClip(emoteClip)) { clip = emoteClip; ik = false; }
                    else ik = true; // no matching clip: the generated pose's hands lead
                }
                if (_reaction == 1 && _model.HasClip("victory")) { clip = "victory"; ik = false; }
                else if (_reaction == -1 && _model.HasClip("sad")) { clip = "sad"; ik = false; }
            }

            _model.Play(clip);
            _model.Tick(dt, speed,
                Rig.HandL.position, ik ? 1f : 0f, transform.TransformDirection(_poleL),
                Rig.HandR.position, ik ? 1f : 0f, transform.TransformDirection(_poleR),
                lookAt, look);

            // The line and the charkhi follow the model's real hands.
            Rig.LineAnchor.position = _model.Palm(true);
            Rig.CharkhiSocket.position = _model.Palm(false);
        }

        private static string EmoteClip(string emoteId)
        {
            switch (emoteId)
            {
                case "taunt_wave":
                case "salute": return "wave";
                case "clap":
                case "laugh": return "clap";
                case "victory_dance":
                case "dhol_beat":
                case "spin": return "dance";
                case "bo_kata":
                case "thumbs_up": return "victory";
                case "facepalm":
                case "shrug": return "sad";
                default: return null;
            }
        }

        private void OnDestroy()
        {
            _model?.Destroy();
            _model = null;
        }
    }
}
