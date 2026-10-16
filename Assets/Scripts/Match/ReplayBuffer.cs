using Kitefall.Core;

namespace Kitefall.Match
{
    /// <summary>Last few seconds of kite snapshots for the slow-motion replay of the cut (spec 6, 9.6).</summary>
    public sealed class ReplayBuffer
    {
        private readonly KiteSnap[][] _frames;
        private readonly int[] _ticks;
        private int _head;
        private int _count;

        public int Capacity => _frames.Length;
        public int Count => _count;

        public ReplayBuffer(int capacity, int kites)
        {
            _frames = new KiteSnap[capacity][];
            _ticks = new int[capacity];
            for (int i = 0; i < capacity; i++) _frames[i] = new KiteSnap[kites];
        }

        public void Record(MatchSnapshot s)
        {
            if (_count > 0 && _ticks[(_head - 1 + Capacity) % Capacity] == s.Tick) return;
            System.Array.Copy(s.Kites, _frames[_head], System.Math.Min(s.Kites.Length, _frames[_head].Length));
            _ticks[_head] = s.Tick;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        /// <summary>Frame <paramref name="ageFromNewest"/> ticks before the newest one.</summary>
        public KiteSnap[] Get(int ageFromNewest)
        {
            if (_count == 0) return null;
            ageFromNewest = System.Math.Max(0, System.Math.Min(_count - 1, ageFromNewest));
            return _frames[(_head - 1 - ageFromNewest + Capacity * 2) % Capacity];
        }

        public void Clear() => _count = 0;
    }
}
