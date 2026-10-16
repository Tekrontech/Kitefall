using System.Collections.Generic;
using Kitefall.Core;

namespace Kitefall.Match
{
    /// <summary>Runs the authoritative MatchSimulation on this device (offline modes, tutorial, practice vs bots).</summary>
    public sealed class OfflineMatchSession : IMatchSession
    {
        private readonly MatchSimulation _sim;
        private readonly List<BotBrain> _brains;
        private readonly KiteInput[] _inputs;
        private readonly List<MatchEvent> _pending = new List<MatchEvent>();
        private MatchSnapshot _prev = new MatchSnapshot();
        private MatchSnapshot _curr = new MatchSnapshot();
        private float _accumulator;
        private KiteInput _localInput;
        private bool _diveLatch;

        public MatchSimulation Simulation => _sim;
        public MatchSetup Setup => _sim.Setup;
        public ArenaLayout Arena => _sim.Arena;
        public int LocalIndex { get; }
        public bool IsOnline => false;
        public MatchSnapshot Previous => _prev;
        public MatchSnapshot Current => _curr;
        public float Alpha => _accumulator / MatchSimulation.Dt;
        public bool IsOver => _sim.IsOver;
        public MatchResult Result => _sim.Result;
        public IReadOnlyList<RoundRecord> Rounds => _sim.Rounds;

        public OfflineMatchSession(MatchSetup setup, int localIndex)
        {
            _sim = new MatchSimulation(setup);
            _brains = MatchFactory.CreateBrains(setup);
            _inputs = new KiteInput[setup.Participants.Count];
            LocalIndex = localIndex;
            _sim.Capture(_curr);
            _sim.Capture(_prev);
        }

        public void SetLocalInput(KiteInput input)
        {
            // A dive tap between two ticks must not be lost.
            if (input.DiveHeld) _diveLatch = true;
            _localInput = input;
        }

        public void Update(float deltaTime)
        {
            _accumulator += deltaTime;
            int guard = 0;
            while (_accumulator >= MatchSimulation.Dt && guard++ < 8)
            {
                _accumulator -= MatchSimulation.Dt;
                StepOnce();
            }
            if (guard >= 8) _accumulator = 0f;
        }

        private void StepOnce()
        {
            foreach (BotBrain b in _brains) _inputs[b.Self] = b.Think(_sim);
            if (LocalIndex >= 0 && LocalIndex < _inputs.Length && !_sim.Participants[LocalIndex].IsBot)
            {
                KiteInput li = _localInput;
                li.DiveHeld = li.DiveHeld || _diveLatch;
                _diveLatch = false;
                _inputs[LocalIndex] = li;
            }
            _sim.Step(_inputs);

            MatchSnapshot swap = _prev;
            _prev = _curr;
            _curr = swap;
            _sim.Capture(_curr);
            _pending.AddRange(_sim.Events);
        }

        public List<MatchEvent> DrainEvents()
        {
            var list = new List<MatchEvent>(_pending);
            _pending.Clear();
            return list;
        }

        public bool RequestLoadoutChange(string tailId, string charkhiId) =>
            _sim.TryChangeLoadout(LocalIndex, SpecDefaults.Tail(tailId), tailId, SpecDefaults.Charkhi(charkhiId), charkhiId);

        public void Quit()
        {
            if (!_sim.IsOver && LocalIndex >= 0) _sim.Forfeit(LocalIndex);
            if (!_sim.IsOver) _sim.EndMatch();
            _sim.Capture(_curr);
            _pending.AddRange(_sim.Events);
        }

        public void Dispose() { }

        /// <summary>Tutorial hook: end the match from outside.</summary>
        public void End() => _sim.EndMatch();
    }
}
