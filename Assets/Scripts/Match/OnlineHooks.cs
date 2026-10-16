using System;
using Kitefall.App;

namespace Kitefall.Match
{
    /// <summary>
    /// Online matchmaking implemented by Kitefall.Online (Netcode for GameObjects + Multiplayer Services).
    /// The Runtime assembly only knows this interface, so the game builds without the online packages.
    /// </summary>
    public interface IOnlineMatchmaker
    {
        string Status { get; }
        /// <summary>Queue for a match; <paramref name="ready"/> receives the running session.</summary>
        void Find(MatchRequest request, Action<IMatchSession> ready, Action<string> failed);
        void CreatePrivate(MatchRequest request, Action<string> codeReady, Action<IMatchSession> ready, Action<string> failed);
        void JoinPrivate(string code, Action<IMatchSession> ready, Action<string> failed);
        /// <summary>Private room host: start now (empty seats → bots).</summary>
        void StartPrivateNow();
        void Cancel();
    }

    public static class OnlineHooks
    {
        public static IOnlineMatchmaker Matchmaker;
        public static bool Available => Matchmaker != null && Game.Backend != null && Game.Backend.IsOnline;
    }
}
