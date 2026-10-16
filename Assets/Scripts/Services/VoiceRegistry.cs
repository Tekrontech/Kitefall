using System;

namespace Kitefall.Services
{
    /// <summary>
    /// Voice chat hooks (upgrade F7). The Unity Vivox implementation lives in Kitefall.Online and registers itself when the
    /// Vivox package is installed; without it these stay null and the mic button is hidden.
    /// Channels: party (lobby + match) and team (Team Battle only) – never strangers. Players under 13: voice off.
    /// </summary>
    public static class VoiceRegistry
    {
        /// <summary>True when voice can be used right now (package installed, signed in, allowed for this player and channel).</summary>
        public static Func<bool> Available;
        /// <summary>Push-to-talk: start / stop sending the microphone.</summary>
        public static Action<bool> SetTransmit;
        /// <summary>Join a channel by name ("party_xxxx" / "team_matchid_0"); leave with null.</summary>
        public static Action<string> JoinChannel;
        public static Action LeaveAll;
        /// <summary>Mute / unmute one player (scoreboard).</summary>
        public static Action<string, bool> MutePlayer;
        public static Action<bool> SetSpeaker;
    }
}
