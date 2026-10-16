using System.Collections.Generic;
using Kitefall.Core;

namespace Kitefall.Match
{
    /// <summary>
    /// What the presenter and HUD read, whether the match runs locally (offline, vs bots) or on a server.
    /// </summary>
    public interface IMatchSession
    {
        MatchSetup Setup { get; }
        ArenaLayout Arena { get; }
        int LocalIndex { get; }
        bool IsOnline { get; }
        /// <summary>The two most recent ticks; views interpolate between them.</summary>
        MatchSnapshot Previous { get; }
        MatchSnapshot Current { get; }
        /// <summary>0–1 position between Previous and Current.</summary>
        float Alpha { get; }
        bool IsOver { get; }
        MatchResult Result { get; }
        /// <summary>Last round record (duels) for the results / round-end text.</summary>
        IReadOnlyList<RoundRecord> Rounds { get; }

        void SetLocalInput(KiteInput input);
        /// <summary>One-shot command (roof claim, pack up, traverse, launch here, emote, quick chat, help launch).
        /// Sent inside the next inputs with a sequence number; the server checks and runs it once.</summary>
        void SendCommand(PlayerCommand command, int arg);
        void Update(float deltaTime);
        /// <summary>Events since the last call.</summary>
        List<MatchEvent> DrainEvents();
        bool RequestLoadoutChange(string tailId, string charkhiId);
        void Quit();
        void Dispose();
    }
}
