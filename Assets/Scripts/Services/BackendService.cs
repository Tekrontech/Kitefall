using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.Core;

namespace Kitefall.Services
{
    public enum SignInMethod { Guest, Google, Apple }

    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string playerId;
        public string name;
        public string country;
        public int points;
        public bool isMe;
    }

    public enum LeaderboardScope { Global, Country, Friends }

    /// <summary>
    /// The game's only door to player data (spec 14: server-authoritative inventory).
    /// LocalBackendService = offline / development. UgsBackendService (Kitefall.Online) = Unity Gaming Services.
    /// </summary>
    public interface IBackendService
    {
        string Name { get; }
        bool IsOnline { get; }
        bool IsSignedIn { get; }
        PlayerProfile Profile { get; }
        event Action ProfileChanged;

        Task<bool> InitializeAsync();
        Task<OpResult> SignInAsync(SignInMethod method);
        Task<OpResult> LinkAccountAsync(SignInMethod method);
        Task SignOutAsync();

        /// <summary>Runs an economy operation (see Core.EconomyCommands) on the server.</summary>
        Task<OpResult> RunAsync(string op, params string[] args);

        /// <summary>Reports a finished match. Offline the local rules apply it; online the match server does.</summary>
        Task<OpResult> SubmitMatchAsync(MatchResult result, MatchRewardContext context);

        Task<List<LeaderboardEntry>> GetLeaderboardAsync(LeaderboardScope scope, int count);

        /// <summary>Re-reads the profile from the server (after a match the server rewarded).</summary>
        Task RefreshProfileAsync();

        /// <summary>Server time (anti-cheat for daily resets). Offline: device time.</summary>
        long NowUtc { get; }
    }

    /// <summary>Online modules register a factory here at start-up (keeps Runtime free of service packages).</summary>
    public static class BackendRegistry
    {
        public static Func<IBackendService> OnlineFactory;
    }
}

namespace Kitefall.Services
{
    /// <summary>Dedicated server builds (UNITY_SERVER): Kitefall.Online registers the server entry point here.</summary>
    public static class ServerRegistry
    {
        public static System.Action<Kitefall.App.GameDatabase> Start;
    }
}
