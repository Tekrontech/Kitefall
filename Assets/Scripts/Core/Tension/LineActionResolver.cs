namespace Kitefall.Core
{
    /// <summary>
    /// Turns raw button states (PULL held, REEL held) into a <see cref="LineAction"/>.
    /// Spec 10: "PULL – hold = pull, release = dheel" and "REEL – reel in; cancels Dheel".
    /// </summary>
    public sealed class LineActionResolver
    {
        private bool _wasPulling;
        private bool _dheelLatched;

        /// <summary>
        /// If true (spec default), letting go of PULL starts dheel which continues until
        /// REEL or PULL is pressed or the charkhi is empty. If false, letting go = Idle.
        /// </summary>
        public bool DheelOnPullRelease { get; set; }

        public LineActionResolver(bool dheelOnPullRelease = true)
        {
            DheelOnPullRelease = dheelOnPullRelease;
        }

        public void Reset()
        {
            _wasPulling = false;
            _dheelLatched = false;
        }

        /// <param name="pullHeld">PULL button is down.</param>
        /// <param name="reelHeld">REEL button is down.</param>
        /// <param name="lineAtMax">Charkhi has no more line to give.</param>
        public LineAction Resolve(bool pullHeld, bool reelHeld, bool lineAtMax)
        {
            LineAction result;

            if (pullHeld)
            {
                _dheelLatched = false;
                result = LineAction.Pull;
            }
            else
            {
                if (_wasPulling && DheelOnPullRelease) _dheelLatched = true;
                if (reelHeld) _dheelLatched = false;
                if (lineAtMax) _dheelLatched = false;

                if (reelHeld) result = LineAction.Reel;
                else if (_dheelLatched) result = LineAction.Dheel;
                else result = LineAction.Idle;
            }

            _wasPulling = pullHeld;
            return result;
        }
    }
}
