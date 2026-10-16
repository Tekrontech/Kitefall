using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.Core;

namespace Kitefall.Services
{
    /// <summary>One squad member as shown in the lobby (visual data only).</summary>
    [Serializable]
    public sealed class PartyMember
    {
        public string playerId;
        public string name;
        public string characterId = CharacterCatalog.DefaultCharacter;
        public string outfitId = "";
        public string[] accessories = new string[4];
        public int rankPoints;
        public bool ready;
        public bool leader;
    }

    /// <summary>
    /// Squad / party (upgrade F2): max 4, leader = creator, join by code / invite / share link (kitefall://party/CODE).
    /// The party stays together after a match. Online: a Multiplayer Services session without Relay (lobby data only).
    /// </summary>
    public interface IPartyService
    {
        bool InParty { get; }
        bool IsLeader { get; }
        string PartyId { get; }
        string Code { get; }
        GameMode Mode { get; }
        IReadOnlyList<PartyMember> Members { get; }
        bool Online { get; }
        event Action Changed;
        Task<OpResult> CreateAsync();
        Task<OpResult> JoinAsync(string code);
        Task LeaveAsync();
        Task SetReadyAsync(bool ready);
        Task<OpResult> SetModeAsync(GameMode mode);
        /// <summary>Leader: queue the whole party for the chosen mode.</summary>
        Task<OpResult> PlayAsync();
    }

    /// <summary>Offline party: just you (friends can join when the online service is set up).</summary>
    public sealed class LocalPartyService : IPartyService
    {
        private readonly List<PartyMember> _members = new List<PartyMember>();
        public bool InParty => _members.Count > 0;
        public bool IsLeader => InParty;
        public string PartyId { get; private set; } = "";
        public string Code { get; private set; } = "";
        public GameMode Mode { get; private set; } = GameMode.TeamBattle;
        public IReadOnlyList<PartyMember> Members => _members;
        public bool Online => false;
        public event Action Changed;
        /// <summary>Starts the match for the party (set by the app flow).</summary>
        public Func<GameMode, Task<OpResult>> Play;

        public Task<OpResult> CreateAsync()
        {
            PlayerProfile p = Kitefall.App.Game.Profile;
            _members.Clear();
            Code = PartyRules.NewCode(new Random());
            PartyId = "party_" + Code;
            _members.Add(new PartyMember
            {
                playerId = p?.playerId, name = p?.displayName ?? "You", characterId = p?.characterId, outfitId = p?.outfitId,
                accessories = p?.accessories.ToArray(), rankPoints = p?.rankPoints ?? 0, ready = true, leader = true
            });
            Changed?.Invoke();
            return Task.FromResult(OpResult.Success());
        }

        public Task<OpResult> JoinAsync(string code) =>
            Task.FromResult(OpResult.Fail("Joining a friend's party needs the online service (docs/ONLINE_SETUP.md)."));

        public Task LeaveAsync()
        {
            _members.Clear();
            PartyId = Code = "";
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task SetReadyAsync(bool ready)
        {
            if (_members.Count > 0) _members[0].ready = ready;
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task<OpResult> SetModeAsync(GameMode mode)
        {
            Mode = mode;
            Changed?.Invoke();
            return Task.FromResult(OpResult.Success());
        }

        public Task<OpResult> PlayAsync()
        {
            string err = PartyRules.CanQueue(Mode, _members.Count);
            if (err != null) return Task.FromResult(OpResult.Fail(err));
            return Play != null ? Play(Mode) : Task.FromResult(OpResult.Fail("Not ready."));
        }
    }

    public static class PartyRegistry
    {
        /// <summary>Set by Kitefall.Online when Multiplayer Services is installed.</summary>
        public static Func<IPartyService> OnlineFactory;
    }
}
